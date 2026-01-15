using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace ImperialShield.Services;

/// <summary>
/// Gestor para configurar el inicio automático con Windows usando Task Scheduler
/// (Necesario porque la app requiere permisos de Administrador)
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
            // Verificación primaria: Task Scheduler
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/Query /TN \"{AppName}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            using var p = Process.Start(psi);
            if (p != null)
            {
                p.WaitForExit();
                if (p.ExitCode == 0) return true;
            }

            // Fallback: Verificar registro (método antiguo) para migración
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Habilita el inicio automático usando Task Scheduler con privilegios altos
    /// </summary>
    public static bool EnableStartup()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return false;

            // 1. Limpiar método antiguo (Registro) si existe
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                key?.DeleteValue(AppName, false);
            }
            catch { /* Ignorar si no existe */ }

            // 2. Crear Tarea Programada con Privilegios Altos
            // /SC ONLOGON -> Al iniciar sesión
            // /RL HIGHEST -> Con permisos de Admin (Salta el UAC al inicio)
            // /F -> Forzar sobreescritura
            string command = $"\"\\\"{exePath}\\\" --silent\""; // Escapado para argumentos de schtasks
            
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/Create /TN \"{AppName}\" /TR {command} /SC ONLOGON /RL HIGHEST /F",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true // Ocultar salida
            };

            using var p = Process.Start(psi);
            if (p != null) 
            {
                p.WaitForExit();
                Logger.Log($"Startup Task Create ExitCode: {p.ExitCode}");
                return p.ExitCode == 0;
            }
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "EnableStartup");
            return false;
        }
    }

    /// <summary>
    /// Deshabilita el inicio automático
    /// </summary>
    public static bool DisableStartup()
    {
        try
        {
            bool success = true;

            // 1. Eliminar Tarea Programada
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/Delete /TN \"{AppName}\" /F",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using (var p = Process.Start(psi))
            {
                p?.WaitForExit();
                if (p?.ExitCode != 0) success = false; // Puede fallar si no existía, está bien.
            }

            // 2. Limpiar método antiguo (Registro) por si acaso
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key?.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch { }

            return true; // Retornamos true porque el objetivo (no iniciar) se cumple
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "DisableStartup");
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
