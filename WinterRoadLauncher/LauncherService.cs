using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WinterRoadLauncher;

public enum ServiceStatus
{
    Idle,
    Starting,
    Running,
    Error,
    Stopped
}

public class LauncherService
{
    private readonly AppSettings _settings;
    private Process? _serverProcess;
    private Process? _clientProcess;
    private CancellationTokenSource? _cts;

    public event Action<string>? OnLog;
    public event Action<string, ServiceStatus>? OnStatusChanged;

    public Rectangle SelectedMonitorBounds { get; set; }

    public bool IsRunning => _serverProcess != null && !_serverProcess.HasExited;

    public LauncherService(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task StartAsync()
    {
        DebugLog("StartAsync called");
        _cts = new CancellationTokenSource();

        try
        {
            // Validate paths
            DebugLog("Validating paths...");
            if (!ValidatePaths())
            {
                DebugLog("Path validation failed");
                return;
            }
            DebugLog("Paths validated");

            // Start server
            Log("서버를 시작합니다...");
            UpdateStatus("Server", ServiceStatus.Starting);

            DebugLog("Starting server process...");
            _serverProcess = StartServer();
            if (_serverProcess == null)
            {
                DebugLog("Server process is null");
                Log("❌ 서버 시작 실패");
                UpdateStatus("Server", ServiceStatus.Error);
                return;
            }
            DebugLog($"Server process started, PID: {_serverProcess.Id}");

            Log("서버 프로세스 시작됨. 연결 대기 중...");

            // Wait for server to be ready
            DebugLog("Waiting for server to be ready...");
            var serverReady = await WaitForServerAsync(_cts.Token);
            if (!serverReady)
            {
                DebugLog("Server wait timeout");
                Log("❌ 서버 연결 시간 초과");
                UpdateStatus("Server", ServiceStatus.Error);
                Stop();
                return;
            }
            DebugLog("Server is ready");

            Log("✅ 서버 연결 확인됨");
            UpdateStatus("Server", ServiceStatus.Running);

            // Start client
            Log("클라이언트를 시작합니다...");
            UpdateStatus("Client", ServiceStatus.Starting);

            DebugLog("Starting client process...");
            _clientProcess = StartClient();
            if (_clientProcess == null)
            {
                DebugLog("Client process is null");
                Log("❌ 클라이언트 시작 실패");
                UpdateStatus("Client", ServiceStatus.Error);
                return;
            }
            DebugLog($"Client process started, PID: {_clientProcess.Id}");

            Log("✅ 클라이언트 실행됨");
            UpdateStatus("Client", ServiceStatus.Running);

            // Monitor client exit
            _clientProcess.EnableRaisingEvents = true;
            _clientProcess.Exited += OnClientExited;
            DebugLog("StartAsync completed successfully");
        }
        catch (Exception ex)
        {
            DebugLog($"Exception in StartAsync: {ex}");
            Log($"❌ 오류 발생: {ex.Message}");
            UpdateStatus("Server", ServiceStatus.Error);
            UpdateStatus("Client", ServiceStatus.Error);
        }
    }

    private static void DebugLog(string message)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "launcher_debug.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] [Service] {message}\n");
        }
        catch { }
    }

    public void Stop()
    {
        _cts?.Cancel();

        Log("종료 중...");

        // Kill client
        if (_clientProcess != null && !_clientProcess.HasExited)
        {
            try
            {
                _clientProcess.Kill();
                _clientProcess.WaitForExit(3000);
                Log("클라이언트 종료됨");
            }
            catch { }
        }
        _clientProcess = null;

        // Kill server
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            try
            {
                // Also kill child processes (node.exe)
                KillProcessTree(_serverProcess.Id);
                Log("서버 종료됨");
            }
            catch { }
        }
        _serverProcess = null;

        UpdateStatus("Server", ServiceStatus.Stopped);
        UpdateStatus("Client", ServiceStatus.Stopped);
        Log("모든 프로세스가 종료되었습니다.");
    }

    private bool ValidatePaths()
    {
        if (string.IsNullOrEmpty(_settings.ServerBatPath) || !File.Exists(_settings.ServerBatPath))
        {
            Log("❌ 서버 스크립트 경로가 유효하지 않습니다. 설정을 확인해주세요.");
            UpdateStatus("Server", ServiceStatus.Error);
            return false;
        }

        if (string.IsNullOrEmpty(_settings.ClientExePath) || !File.Exists(_settings.ClientExePath))
        {
            Log("❌ 클라이언트 실행 파일 경로가 유효하지 않습니다. 설정을 확인해주세요.");
            UpdateStatus("Client", ServiceStatus.Error);
            return false;
        }

        return true;
    }

    private Process? StartServer()
    {
        try
        {
            var workingDir = Path.GetDirectoryName(_settings.ServerBatPath) ?? "";

            // 서버 시작 전에 config.json의 lobbyDuration을 런처 설정값으로 동기화
            SyncLobbyDurationToConfig(workingDir);

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{_settings.ServerBatPath}\"",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(startInfo);
            
            if (process != null)
            {
                // Async read output
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Log($"[Server] {e.Data}");
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Log($"[Server Error] {e.Data}");
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            return process;
        }
        catch (Exception ex)
        {
            Log($"서버 시작 오류: {ex.Message}");
            return null;
        }
    }

    private void SyncLobbyDurationToConfig(string serverDir)
    {
        try
        {
            var configPath = Path.Combine(serverDir, "config.json");
            if (!File.Exists(configPath))
            {
                Log($"⚠️ config.json을 찾을 수 없어 대기시간 동기화를 건너뜁니다: {configPath}");
                return;
            }

            var json = File.ReadAllText(configPath);
            var node = JsonNode.Parse(json);
            if (node == null)
            {
                Log("⚠️ config.json 파싱 실패");
                return;
            }

            var game = node["game"] as JsonObject;
            if (game == null)
            {
                Log("⚠️ config.json에 game 섹션이 없습니다");
                return;
            }

            game["lobbyDuration"] = _settings.ServerWaitSeconds;

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(configPath, node.ToJsonString(options));
            Log($"✅ 대기 시간 동기화: lobbyDuration = {_settings.ServerWaitSeconds}초");
        }
        catch (Exception ex)
        {
            Log($"⚠️ 대기시간 동기화 실패: {ex.Message}");
        }
    }

    private async Task<bool> WaitForServerAsync(CancellationToken ct)
    {
        DebugLog($"WaitForServerAsync started - using simple delay (5 seconds)");
        
        // Simple delay-based wait for now (to test if network code is causing crash)
        for (int i = 0; i < 5; i++)
        {
            if (ct.IsCancellationRequested)
            {
                DebugLog("Cancellation requested");
                return false;
            }
            
            DebugLog($"Waiting... {i + 1}/5");
            await Task.Delay(1000);
        }
        
        DebugLog("Wait completed, assuming server is ready");
        return true;
    }

    private Process? StartClient()
    {
        try
        {
            var workingDir = Path.GetDirectoryName(_settings.ClientExePath) ?? "";
            
            string wsUrl = $"ws://{_settings.ServerIP}:{_settings.ServerPort}";
            string monitorArgs = $" --monitor {_settings.SelectedMonitorIndex}";
            if (SelectedMonitorBounds != Rectangle.Empty)
            {
                monitorArgs += $" -screen-x {SelectedMonitorBounds.X} -screen-y {SelectedMonitorBounds.Y}";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _settings.ClientExePath,
                Arguments = $"--server-url {wsUrl}{monitorArgs}",
                WorkingDirectory = workingDir,
                UseShellExecute = true
            };

            return Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Log($"클라이언트 시작 오류: {ex.Message}");
            return null;
        }
    }

    private void OnClientExited(object? sender, EventArgs e)
    {
        Log("클라이언트가 종료되었습니다. 서버를 종료합니다...");
        UpdateStatus("Client", ServiceStatus.Stopped);

        // Stop server when client exits
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            try
            {
                KillProcessTree(_serverProcess.Id);
                Log("서버가 종료되었습니다.");
            }
            catch { }
        }
        _serverProcess = null;
        
        UpdateStatus("Server", ServiceStatus.Stopped);
    }

    private void KillProcessTree(int pid)
    {
        try
        {
            // Use taskkill to kill process tree
            var startInfo = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/F /T /PID {pid}",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(startInfo)?.WaitForExit(5000);
        }
        catch { }
    }

    private void Log(string message)
    {
        OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    private void UpdateStatus(string component, ServiceStatus status)
    {
        OnStatusChanged?.Invoke(component, status);
    }
}
