using System.IO;

namespace ImperialShield.Services;

/// <summary>
/// Servicio de logging simple para depuración
/// </summary>
public static class Logger
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ImperialShield", "Logs");
    
    private static readonly string LogFile = Path.Combine(LogDirectory, "imperial_shield.log");
    private static readonly object _lock = new();

    static Logger()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }
        catch { }
    }

    public static void Log(string message, LogLevel level = LogLevel.Info)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logLine = $"[{timestamp}] [{level}] {message}";
            
            lock (_lock)
            {
                File.AppendAllText(LogFile, logLine + Environment.NewLine);
            }
            
            System.Diagnostics.Debug.WriteLine(logLine);
        }
        catch { }
    }

    public static void LogException(Exception ex, string context = "")
    {
        var message = string.IsNullOrEmpty(context) 
            ? $"Exception: {ex.Message}\nStackTrace: {ex.StackTrace}"
            : $"[{context}] Exception: {ex.Message}\nStackTrace: {ex.StackTrace}";
        
        if (ex.InnerException != null)
        {
            message += $"\nInner: {ex.InnerException.Message}";
        }
        
        Log(message, LogLevel.Error);
    }

    public static void LogCrash(Exception ex)
    {
        try
        {
            var crashFile = Path.Combine(LogDirectory, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            var content = $"=== IMPERIAL SHIELD CRASH REPORT ===\n" +
                         $"Time: {DateTime.Now}\n" +
                         $"OS: {Environment.OSVersion}\n" +
                         $".NET: {Environment.Version}\n" +
                         $"\n=== EXCEPTION ===\n" +
                         $"Type: {ex.GetType().FullName}\n" +
                         $"Message: {ex.Message}\n" +
                         $"Source: {ex.Source}\n" +
                         $"\n=== STACK TRACE ===\n" +
                         $"{ex.StackTrace}\n";
            
            if (ex.InnerException != null)
            {
                content += $"\n=== INNER EXCEPTION ===\n" +
                          $"Type: {ex.InnerException.GetType().FullName}\n" +
                          $"Message: {ex.InnerException.Message}\n" +
                          $"StackTrace: {ex.InnerException.StackTrace}\n";
            }
            
            File.WriteAllText(crashFile, content);
            Log($"Crash log saved to: {crashFile}", LogLevel.Error);
        }
        catch { }
    }

    public static string GetLogPath() => LogFile;
    public static string GetLogDirectory() => LogDirectory;
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}
