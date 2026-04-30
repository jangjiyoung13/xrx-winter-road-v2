namespace WinterRoadLauncher;

public partial class MainForm : Form
{
    private readonly AppSettings _settings;
    private LauncherService? _launcher;
    private bool _autoStartCancelled = false;

    private Label lblServerStatus = null!;
    private Label lblClientStatus = null!;
    private Label lblTdStatus = null!;
    private TextBox txtLog = null!;
    private Button btnStart = null!;
    private Button btnStop = null!;
    private Button btnSettings = null!;

    // TD(LED) 상태 폴링용 — 1초 간격으로 GET /api/td-state 호출.
    private static readonly HttpClient _tdHttpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
    private System.Windows.Forms.Timer? _tdPollTimer;

    public MainForm()
    {
        _settings = AppSettings.Load();
        InitializeComponent();
        UpdateStatusLabels(ServiceStatus.Idle, ServiceStatus.Idle);

        // 자동 시작 모드: 폼이 표시되면 카운트다운 후 시작 자동 호출 (Esc로 취소 가능)
        this.KeyPreview = true;
        this.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) _autoStartCancelled = true;
        };
        this.Shown += MainForm_Shown;
    }

    private async void MainForm_Shown(object? sender, EventArgs e)
    {
        if (!_settings.AutoStartOnLaunch) return;

        AppendLog("자동 시작 모드 — 5초 후 시작합니다 (Esc로 취소).");
        for (int i = 5; i >= 1; i--)
        {
            if (_autoStartCancelled)
            {
                AppendLog("자동 시작이 취소되었습니다.");
                return;
            }
            AppendLog($"  ...{i}초");
            await Task.Delay(1000);
        }
        if (_autoStartCancelled)
        {
            AppendLog("자동 시작이 취소되었습니다.");
            return;
        }
        BtnStart_Click(this, EventArgs.Empty);
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Form settings
        this.Text = "Winter Road Launcher";
        this.Size = new Size(500, 450);
        this.MinimumSize = new Size(450, 400);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Font = new Font("Segoe UI", 10F);

        // 타이틀바 / 작업표시줄 아이콘 (exe에 임베드된 아이콘 사용)
        try
        {
            this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch { /* 아이콘 로드 실패 시 기본 아이콘 사용 */ }

        // Title
        var lblTitle = new Label
        {
            Text = "Winter Road Launcher",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 50
        };

        // Status Panel
        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 90,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(10, 0, 10, 0)
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var lblServerTitle = new Label { Text = "서버 상태:", Anchor = AnchorStyles.Left, AutoSize = true };
        lblServerStatus = new Label { Text = "● 대기 중", Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = Color.Gray };
        var lblClientTitle = new Label { Text = "클라이언트 상태:", Anchor = AnchorStyles.Left, AutoSize = true };
        lblClientStatus = new Label { Text = "● 대기 중", Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = Color.Gray };
        var lblTdTitle = new Label { Text = "LED(TD) 상태:", Anchor = AnchorStyles.Left, AutoSize = true };
        lblTdStatus = new Label { Text = "● --", Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = Color.DimGray };

        statusPanel.Controls.Add(lblServerTitle, 0, 0);
        statusPanel.Controls.Add(lblServerStatus, 1, 0);
        statusPanel.Controls.Add(lblClientTitle, 0, 1);
        statusPanel.Controls.Add(lblClientStatus, 1, 1);
        statusPanel.Controls.Add(lblTdTitle, 0, 2);
        statusPanel.Controls.Add(lblTdStatus, 1, 2);

        // Log TextBox
        txtLog = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.LightGreen,
            Font = new Font("Consolas", 9F)
        };

        var logPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        logPanel.Controls.Add(txtLog);

        // Button Panel
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10, 5, 10, 10)
        };

        btnStop = new Button
        {
            Text = "⏹ 종료",
            Size = new Size(100, 35),
            Enabled = false
        };
        btnStop.Click += BtnStop_Click;

        btnStart = new Button
        {
            Text = "🚀 시작",
            Size = new Size(100, 35)
        };
        btnStart.Click += BtnStart_Click;

        btnSettings = new Button
        {
            Text = "⚙ 설정",
            Size = new Size(100, 35)
        };
        btnSettings.Click += BtnSettings_Click;

        buttonPanel.Controls.Add(btnStop);
        buttonPanel.Controls.Add(btnStart);
        buttonPanel.Controls.Add(btnSettings);

        // Add controls to form
        this.Controls.Add(logPanel);
        this.Controls.Add(statusPanel);
        this.Controls.Add(lblTitle);
        this.Controls.Add(buttonPanel);

        this.ResumeLayout();

        AppendLog("시작을 눌러주세요...");

        // TD(LED) 상태 폴링 시작 — 1초 간격, 서버 미가동 시엔 자연스럽게 "--" 유지.
        StartTdStatePolling();
    }

    private void StartTdStatePolling()
    {
        if (_tdPollTimer != null) return;
        _tdPollTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _tdPollTimer.Tick += async (_, _) => await PollTdStateAsync();
        _tdPollTimer.Start();
    }

    private void StopTdStatePolling()
    {
        if (_tdPollTimer == null) return;
        _tdPollTimer.Stop();
        _tdPollTimer.Dispose();
        _tdPollTimer = null;
    }

    private async Task PollTdStateAsync()
    {
        string url = $"http://{_settings.ServerIP}:{_settings.ServerPort}/api/td-state";
        try
        {
            using var resp = await _tdHttpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                ApplyTdState(null);
                return;
            }
            var json = await resp.Content.ReadAsStringAsync();
            // 초경량 파싱 — System.Text.Json 사용 (이미 AppSettings에서 사용 중).
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? tdState = root.TryGetProperty("tdState", out var s) ? s.GetString() : null;
            ApplyTdState(tdState);
        }
        catch
        {
            // 서버 미가동 / 타임아웃 / 연결 거부 등 → "--" 표시
            ApplyTdState(null);
        }
    }

    private void ApplyTdState(string? tdState)
    {
        SafeInvoke(() =>
        {
            (lblTdStatus.Text, lblTdStatus.ForeColor) = tdState switch
            {
                "idle"                    => ("● PRESET", Color.Gray),
                "transitioning_to_live"   => ("● → LIVE", Color.Orange),
                "live"                    => ("● LIVE", Color.LimeGreen),
                "transitioning_to_preset" => ("● → PRESET", Color.Orange),
                _                         => ("● --", Color.DimGray)
            };
        });
    }

    private async void BtnStart_Click(object? sender, EventArgs e)
    {
        try
        {
            DebugLog("BtnStart_Click started");
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            btnSettings.Enabled = false;

            txtLog.Clear();
            AppendLog("런처를 시작합니다...");

            // Get selected monitor from settings
            var screens = Screen.AllScreens;
            int monitorIdx = _settings.SelectedMonitorIndex;
            if (monitorIdx < 0 || monitorIdx >= screens.Length)
                monitorIdx = 0;
            var selectedScreen = screens[monitorIdx];
            AppendLog($"대상 모니터: {selectedScreen.DeviceName.TrimStart('\\', '.')} ({selectedScreen.Bounds.Width}×{selectedScreen.Bounds.Height})");

            DebugLog("Creating LauncherService...");
            _launcher = new LauncherService(_settings);
            _launcher.SelectedMonitorBounds = selectedScreen.Bounds;
            _launcher.OnLog += (msg) => SafeInvoke(() => AppendLog(msg));
            _launcher.OnStatusChanged += (component, status) => SafeInvoke(() => UpdateStatus(component, status));

            DebugLog("Calling StartAsync...");
            await _launcher.StartAsync();
            DebugLog("StartAsync completed");

            // If launcher finished (client exited or error), re-enable buttons
            if (_launcher == null || !_launcher.IsRunning)
            {
                DebugLog("Resetting buttons (launcher not running)");
                ResetButtons();
            }
            DebugLog("BtnStart_Click completed normally");
        }
        catch (Exception ex)
        {
            DebugLog($"Exception in BtnStart_Click: {ex}");
            AppendLog($"❌ 오류 발생: {ex.Message}");
            MessageBox.Show($"오류 발생:\n{ex.Message}\n\n{ex.StackTrace}", "오류", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            ResetButtons();
        }
    }

    private static void DebugLog(string message)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "launcher_debug.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        _launcher?.Stop();
        ResetButtons();
    }

    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        using var settingsForm = new SettingsForm(_settings);
        if (settingsForm.ShowDialog() == DialogResult.OK)
        {
            _settings.Save();
            AppendLog("설정이 저장되었습니다.");
        }
    }

    private void ResetButtons()
    {
        btnStart.Enabled = true;
        btnStop.Enabled = false;
        btnSettings.Enabled = true;
        UpdateStatusLabels(ServiceStatus.Idle, ServiceStatus.Idle);
    }

    private void UpdateStatus(string component, ServiceStatus status)
    {
        if (component == "Server")
            UpdateStatusLabel(lblServerStatus, status);
        else if (component == "Client")
            UpdateStatusLabel(lblClientStatus, status);

        // Check if both stopped
        if (status == ServiceStatus.Stopped && _launcher != null && !_launcher.IsRunning)
        {
            ResetButtons();
        }
    }

    private void UpdateStatusLabels(ServiceStatus server, ServiceStatus client)
    {
        UpdateStatusLabel(lblServerStatus, server);
        UpdateStatusLabel(lblClientStatus, client);
    }

    private void UpdateStatusLabel(Label label, ServiceStatus status)
    {
        (label.Text, label.ForeColor) = status switch
        {
            ServiceStatus.Idle => ("● 대기 중", Color.Gray),
            ServiceStatus.Starting => ("● 시작 중...", Color.Orange),
            ServiceStatus.Running => ("● 실행 중", Color.LimeGreen),
            ServiceStatus.Error => ("● 오류", Color.Red),
            ServiceStatus.Stopped => ("● 종료됨", Color.Gray),
            _ => ("● 알 수 없음", Color.Gray)
        };
    }

    private void AppendLog(string message)
    {
        if (txtLog.IsDisposed) return;
        txtLog.AppendText(message + Environment.NewLine);
        txtLog.SelectionStart = txtLog.Text.Length;
        txtLog.ScrollToCaret();
    }

    private void SafeInvoke(Action action)
    {
        try
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            if (this.InvokeRequired)
                this.BeginInvoke(action);
            else
                action();
        }
        catch { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopTdStatePolling();
        _launcher?.Stop();
        base.OnFormClosing(e);
    }
}
