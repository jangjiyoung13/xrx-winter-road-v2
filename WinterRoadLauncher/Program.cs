namespace WinterRoadLauncher;

static class Program
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "launcher_debug.log");

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        try
        {
            Log("Application starting...");

            // Global exception handlers
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                Log($"ThreadException: {e.Exception}");
                MessageBox.Show($"UI 스레드 오류:\n{e.Exception.Message}\n\n{e.Exception.StackTrace}", 
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Log($"UnhandledException: {ex}");
                MessageBox.Show($"치명적 오류:\n{ex?.Message}\n\n{ex?.StackTrace}", 
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Log("Initializing application...");
            ApplicationConfiguration.Initialize();
            
            Log("Creating MainForm...");
            var form = new MainForm();
            
            Log("Running application...");
            Application.Run(form);
            
            Log("Application ended normally.");
        }
        catch (Exception ex)
        {
            Log($"Fatal error in Main: {ex}");
            MessageBox.Show($"시작 오류:\n{ex.Message}\n\n{ex.StackTrace}", 
                "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch { }
    }
}

