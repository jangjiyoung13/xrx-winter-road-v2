using System.Text.Json;

namespace WinterRoadLauncher;

public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        AppContext.BaseDirectory, "settings.json");

    public string ServerBatPath { get; set; } = "";
    public string ClientExePath { get; set; } = "";
    public string ServerIP { get; set; } = "localhost";
    public int ServerPort { get; set; } = 3000;
    public int ServerWaitSeconds { get; set; } = 30;
    public int SelectedMonitorIndex { get; set; } = 0;

    // DDNS Settings
    public bool DdnsEnabled { get; set; } = false;
    public string DdnsHostname { get; set; } = "";
    public string DdnsPassword { get; set; } = "";

    // TD (OSC) Settings — 서버 config.json의 osc.host / osc.port와 동기화
    public string OscHost { get; set; } = "";
    public int OscPort { get; set; } = 50013;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                settings.ServerPort = 3000; // 포트는 3000으로 고정
                return settings;
            }
        }
        catch
        {
            // Return default settings on error
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"설정 저장 실패: {ex.Message}", "오류", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
