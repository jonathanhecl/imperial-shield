using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace ImperialShield.Services;

public class QuarantineService
{
    private const string IFEO_PATH = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
    private const string WSH_PATH = @"Software\Microsoft\Windows Script Host\Settings";
    private const string VBS_CLASS_PATH = @".vbs";
    
    private static readonly string _imperialShieldPath = 
        System.Reflection.Assembly.GetEntryAssembly()?.Location.Replace(".dll", ".exe") 
        ?? "ImperialShield.exe";

    #region VBS Blocking

    /// <summary>
    /// Verifica si los scripts VBS están habilitados en el sistema.
    /// </summary>
    public static bool IsVBSEnabled()
    {
        try
        {
            // 1. Verificar configuración directa en registro de usuario
            using var key = Registry.CurrentUser.OpenSubKey(WSH_PATH);
            if (key != null)
            {
                var value = key.GetValue("Enabled");
                if (value != null)
                {
                    return Convert.ToInt32(value) != 0;
                }
            }

            // 2. Verificar políticas de grupo en registro local
            using var policyKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Script Host\Settings");
            if (policyKey != null)
            {
                var policyValue = policyKey.GetValue("Enabled");
                if (policyValue != null)
                {
                    return Convert.ToInt32(policyValue) != 0;
                }
            }

            // 3. Verificar si existe la clave de desactivación por IFEO (Image File Execution Options)
            // Este es un método común para desactivar wscript.exe y cscript.exe
            using var wscriptIfeo = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\wscript.exe");
            using var cscriptIfeo = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\cscript.exe");
            
            if (wscriptIfeo != null || cscriptIfeo != null)
            {
                return false; // Si están en IFEO, están deshabilitados
            }

            // 4. Verificar asociaciones de archivos (si están redirigidos)
            try
            {
                var vbsAssociation = GetVBSAssociation();
                if (vbsAssociation != "VBSFile")
                    return false; // Si la asociación fue modificada, está deshabilitado
            }
            catch
            {
                // Si no podemos verificar la asociación, continuamos
            }

            // 5. Si no encontramos ninguna evidencia de desactivación, asumimos que está habilitado
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "IsVBSEnabled");
            return true; // Asumir habilitado en caso de error
        }
    }

    /// <summary>
    /// Habilita o deshabilita la ejecución de scripts VBS/JS en el sistema.
    /// </summary>
    public static bool SetVBSEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(WSH_PATH);
            if (key == null) return false;
            
            key.SetValue("Enabled", enabled ? 1 : 0, RegistryValueKind.DWord);
            
            Logger.Log($"VBS/WSH scripts {(enabled ? "habilitados" : "deshabilitados")}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "SetVBSEnabled");
            return false;
        }
    }

    /// <summary>
    /// Obtiene el programa asociado actualmente a los archivos .vbs
    /// </summary>
    public static string GetVBSAssociation()
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(VBS_CLASS_PATH);
            return key?.GetValue("")?.ToString() ?? "VBSFile";
        }
        catch
        {
            return "VBSFile";
        }
    }

    #endregion

    #region Executable Quarantine (IFEO)

    /// <summary>
    /// Pone un ejecutable en cuarentena usando IFEO.
    /// Cuando se intente ejecutar, se abrirá Imperial Shield en su lugar.
    /// </summary>
    public static bool QuarantineExecutable(string executableName)
    {
        try
        {
            // Asegurarnos de que solo es el nombre del archivo, no la ruta completa
            string fileName = Path.GetFileName(executableName);
            
            string registryPath = $@"{IFEO_PATH}\{fileName}";
            
            using var key = Registry.LocalMachine.CreateSubKey(registryPath);
            if (key == null)
            {
                Logger.Log($"No se pudo crear clave de cuarentena para {fileName}");
                return false;
            }
            
            // El "Debugger" intercepta la ejecución y la redirige a nuestro programa
            key.SetValue("Debugger", $"\"{_imperialShieldPath}\" --blocked");
            
            // Guardar metadata adicional
            key.SetValue("QuarantinedBy", "ImperialShield");
            key.SetValue("QuarantineDate", DateTime.Now.ToString("o"));
            
            Logger.Log($"Ejecutable puesto en cuarentena: {fileName}");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            Logger.Log($"Permisos insuficientes para poner en cuarentena: {executableName}");
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, $"QuarantineExecutable({executableName})");
            return false;
        }
    }

    /// <summary>
    /// Quita un ejecutable de cuarentena, permitiendo su ejecución normal.
    /// </summary>
    public static bool UnquarantineExecutable(string executableName)
    {
        try
        {
            string fileName = Path.GetFileName(executableName);
            string registryPath = $@"{IFEO_PATH}\{fileName}";
            
            Registry.LocalMachine.DeleteSubKeyTree(registryPath, false);
            
            Logger.Log($"Ejecutable liberado de cuarentena: {fileName}");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            Logger.Log($"Permisos insuficientes para liberar: {executableName}");
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, $"UnquarantineExecutable({executableName})");
            return false;
        }
    }

    /// <summary>
    /// Verifica si un ejecutable está en cuarentena.
    /// </summary>
    public static bool IsQuarantined(string executableName)
    {
        try
        {
            string fileName = Path.GetFileName(executableName);
            string registryPath = $@"{IFEO_PATH}\{fileName}";
            
            using var key = Registry.LocalMachine.OpenSubKey(registryPath);
            if (key == null) return false;
            
            var debugger = key.GetValue("Debugger")?.ToString();
            var quarantinedBy = key.GetValue("QuarantinedBy")?.ToString();
            
            // Solo consideramos "en cuarentena" si fue bloqueado por Imperial Shield
            return quarantinedBy == "ImperialShield" || 
                   (debugger != null && debugger.Contains("ImperialShield"));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Obtiene la lista de todos los ejecutables en cuarentena por Imperial Shield.
    /// </summary>
    public static List<QuarantinedApp> GetQuarantinedApps()
    {
        var apps = new List<QuarantinedApp>();
        
        try
        {
            using var ifeoKey = Registry.LocalMachine.OpenSubKey(IFEO_PATH);
            if (ifeoKey == null) return apps;
            
            foreach (var subKeyName in ifeoKey.GetSubKeyNames())
            {
                using var appKey = ifeoKey.OpenSubKey(subKeyName);
                if (appKey == null) continue;
                
                var quarantinedBy = appKey.GetValue("QuarantinedBy")?.ToString();
                var debugger = appKey.GetValue("Debugger")?.ToString();
                
                // Solo mostrar los bloqueados por Imperial Shield
                if (quarantinedBy == "ImperialShield" || 
                    (debugger != null && debugger.Contains("ImperialShield")))
                {
                    var dateStr = appKey.GetValue("QuarantineDate")?.ToString();
                    DateTime.TryParse(dateStr, out var quarantineDate);
                    
                    apps.Add(new QuarantinedApp
                    {
                        FileName = subKeyName,
                        QuarantineDate = quarantineDate,
                        DebuggerPath = debugger ?? ""
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "GetQuarantinedApps");
        }
        
        return apps.OrderByDescending(a => a.QuarantineDate).ToList();
    }

    #endregion

    #region Startup Argument Handling

    /// <summary>
    /// Procesa argumentos de inicio para detectar si fuimos invocados por IFEO.
    /// </summary>
    public static bool HandleQuarantineInterception(string[] args)
    {
        if (args.Length < 2) return false;
        
        // Si el primer argumento es --blocked, significa que interceptamos un virus
        if (args[0] == "--blocked")
        {
            string blockedExe = args[1];
            Logger.Log($"IFEO INTERCEPTION: Blocked execution of {blockedExe}");
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Extrae el nombre del ejecutable bloqueado de los argumentos.
    /// </summary>
    public static string? GetBlockedExecutableName(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--blocked")
        {
            return Path.GetFileName(args[1]);
        }
        return null;
    }

    #endregion
}

public class QuarantinedApp
{
    public string FileName { get; set; } = string.Empty;
    public DateTime QuarantineDate { get; set; }
    public string DebuggerPath { get; set; } = string.Empty;
    
    public string QuarantineDateFormatted => QuarantineDate == DateTime.MinValue 
        ? "Fecha desconocida" 
        : QuarantineDate.ToString("g");
}
