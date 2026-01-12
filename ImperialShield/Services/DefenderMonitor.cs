using System.Management;
using System.Management.Automation;
using System.Collections.ObjectModel;

namespace ImperialShield.Services;

/// <summary>
/// Monitor de Windows Defender
/// Verifica estado del antivirus y detecta nuevas exclusiones
/// </summary>
public class DefenderMonitor : IDisposable
{
    private Timer? _timer;
    private bool _lastKnownStatus = true;
    private HashSet<string> _knownExclusions = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _pollingIntervalMs = 60000; // 60 segundos
    private bool _isDisposed;

    public event EventHandler<DefenderStatusEventArgs>? DefenderStatusChanged;
    public event EventHandler<ExclusionAddedEventArgs>? ExclusionAdded;

    public void Start()
    {
        // Obtener estado inicial
        _lastKnownStatus = IsDefenderEnabled();
        _knownExclusions = GetCurrentExclusions();

        // Iniciar el timer de polling (con delay inicial de 5 segundos para no interferir con UI)
        _timer = new Timer(CheckDefenderStatus, null, 5000, _pollingIntervalMs);
    }

    private void CheckDefenderStatus(object? state)
    {
        try
        {
            // Verificar si Defender está activo
            var currentStatus = IsDefenderEnabled();
            if (currentStatus != _lastKnownStatus)
            {
                _lastKnownStatus = currentStatus;
                DefenderStatusChanged?.Invoke(this, new DefenderStatusEventArgs
                {
                    IsEnabled = currentStatus,
                    Timestamp = DateTime.Now
                });
            }

            // Verificar nuevas exclusiones
            var currentExclusions = GetCurrentExclusions();
            var newExclusions = currentExclusions.Except(_knownExclusions, StringComparer.OrdinalIgnoreCase);

            foreach (var exclusion in newExclusions)
            {
                ExclusionAdded?.Invoke(this, new ExclusionAddedEventArgs
                {
                    ExclusionPath = exclusion,
                    Timestamp = DateTime.Now
                });
            }

            _knownExclusions = currentExclusions;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en monitoreo de Defender: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifica si Windows Defender está activo usando PowerShell
    /// </summary>
    public bool IsDefenderEnabled()
    {
        // Usar PowerShell directamente - es más confiable que WMI
        return IsDefenderEnabledViaPowerShell();
    }

    private bool IsDefenderEnabledViaPowerShell()
    {
        try
        {
            using var ps = PowerShell.Create();
            ps.AddScript("(Get-MpComputerStatus).RealTimeProtectionEnabled");
            var results = ps.Invoke();
            
            if (results.Count > 0 && results[0]?.BaseObject is bool enabled)
            {
                return enabled;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error PowerShell: {ex.Message}");
        }

        return true; // Asumir habilitado si no podemos verificar
    }

    /// <summary>
    /// Obtiene la lista actual de exclusiones de Windows Defender
    /// </summary>
    public HashSet<string> GetCurrentExclusions()
    {
        var exclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var ps = PowerShell.Create();
            ps.AddScript(@"
                $prefs = Get-MpPreference
                @{
                    Paths = $prefs.ExclusionPath
                    Extensions = $prefs.ExclusionExtension
                    Processes = $prefs.ExclusionProcess
                }
            ");
            
            var results = ps.Invoke();
            
            if (results.Count > 0 && results[0]?.BaseObject is System.Collections.Hashtable ht)
            {
                AddToSet(exclusions, ht["Paths"]);
                AddToSet(exclusions, ht["Extensions"], prefix: "*."); 
                AddToSet(exclusions, ht["Processes"], prefix: "[Proceso] ");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al obtener exclusiones: {ex.Message}");
        }

        return exclusions;
    }

    private static void AddToSet(HashSet<string> set, object? values, string prefix = "")
    {
        if (values is object[] array)
        {
            foreach (var item in array)
            {
                if (item != null)
                    set.Add(prefix + item.ToString());
            }
        }
        else if (values is string str)
        {
            set.Add(prefix + str);
        }
    }

    /// <summary>
    /// Elimina una exclusión de Windows Defender
    /// </summary>
    public bool RemoveExclusion(string path)
    {
        try
        {
            using var ps = PowerShell.Create();
            ps.AddScript($"Remove-MpPreference -ExclusionPath '{path}'");
            ps.Invoke();
            
            if (ps.HadErrors)
            {
                foreach (var error in ps.Streams.Error)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al eliminar exclusión: {error}");
                }
                return false;
            }
            
            _knownExclusions.Remove(path);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Obtiene información detallada del estado de Defender
    /// </summary>
    public DefenderInfo GetDefenderInfo()
    {
        var info = new DefenderInfo();

        try
        {
            using var ps = PowerShell.Create();
            ps.AddScript(@"
                $status = Get-MpComputerStatus
                @{
                    RealTimeEnabled = $status.RealTimeProtectionEnabled
                    BehaviorMonitor = $status.BehaviorMonitorEnabled
                    OnAccessProtection = $status.OnAccessProtectionEnabled
                    IoavProtection = $status.IoavProtectionEnabled
                    AntivirusEnabled = $status.AntivirusEnabled
                    SignatureVersion = $status.AntivirusSignatureVersion
                    SignatureAge = $status.AntivirusSignatureAge
                    LastScan = $status.FullScanEndTime
                    QuickScanAge = $status.QuickScanAge
                }
            ");
            
            var results = ps.Invoke();
            
            if (results.Count > 0 && results[0]?.BaseObject is System.Collections.Hashtable ht)
            {
                info.RealTimeProtectionEnabled = GetBool(ht["RealTimeEnabled"]);
                info.BehaviorMonitorEnabled = GetBool(ht["BehaviorMonitor"]);
                info.OnAccessProtectionEnabled = GetBool(ht["OnAccessProtection"]);
                info.IoavProtectionEnabled = GetBool(ht["IoavProtection"]);
                info.AntivirusEnabled = GetBool(ht["AntivirusEnabled"]);
                info.SignatureVersion = ht["SignatureVersion"]?.ToString() ?? "Unknown";
                info.SignatureAgeDays = GetInt(ht["SignatureAge"]);
                info.QuickScanAgeDays = GetInt(ht["QuickScanAge"]);
                
                if (ht["LastScan"] is DateTime lastScan)
                    info.LastFullScan = lastScan;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al obtener info de Defender: {ex.Message}");
        }

        info.ExclusionCount = _knownExclusions.Count;
        return info;
    }

    private static bool GetBool(object? obj) => obj is bool b && b;
    private static int GetInt(object? obj) => obj is int i ? i : 0;

    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Stop();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

public class DefenderStatusEventArgs : EventArgs
{
    public bool IsEnabled { get; init; }
    public DateTime Timestamp { get; init; }
}

public class ExclusionAddedEventArgs : EventArgs
{
    public required string ExclusionPath { get; init; }
    public DateTime Timestamp { get; init; }
}

public class DefenderInfo
{
    public bool RealTimeProtectionEnabled { get; set; }
    public bool BehaviorMonitorEnabled { get; set; }
    public bool OnAccessProtectionEnabled { get; set; }
    public bool IoavProtectionEnabled { get; set; }
    public bool AntivirusEnabled { get; set; }
    public string SignatureVersion { get; set; } = "Unknown";
    public int SignatureAgeDays { get; set; }
    public int QuickScanAgeDays { get; set; }
    public DateTime? LastFullScan { get; set; }
    public int ExclusionCount { get; set; }
}
