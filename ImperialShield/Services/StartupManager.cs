using Microsoft.Win32;

namespace ImperialShield.Services;

/// <summary>
/// Gestor para configurar el inicio automático con Windows
/// </summary>
public static class StartupManager
{
    private const string AppName = "ImperialShield";
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Verifica si la aplicación está configurada para iniciar con Windows
    /// </summary>
    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Habilita el inicio automático con Windows
    /// </summary>
    public static bool EnableStartup()
    {
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return false;

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.SetValue(AppName, $"\"{exePath}\" --silent");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Deshabilita el inicio automático con Windows
    /// </summary>
    public static bool DisableStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.DeleteValue(AppName, false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Alterna el estado del inicio automático
    /// </summary>
    public static bool ToggleStartup()
    {
        if (IsStartupEnabled())
        {
            return DisableStartup();
        }
        else
        {
            return EnableStartup();
        }
    }
}
