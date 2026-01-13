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
    // private readonly int _pollingIntervalMs = 60000; // Unused
    private bool _isDisposed;

    public event EventHandler<DefenderStatusEventArgs>? DefenderStatusChanged;
    public event EventHandler<ExclusionAddedEventArgs>? ExclusionAdded;

    public void Start()
    {
        // Obtener estado inicial
        _lastKnownStatus = IsDefenderEnabled();
        _knownExclusions = GetCurrentExclusions();

        // Iniciar el timer de polling con intervalo configurado
        int interval = SettingsManager.Current.PollingIntervalMs;
        _timer = new Timer(CheckDefenderStatus, null, 5000, interval);
        Logger.Log($"DefenderMonitor started with interval: {interval}ms");
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
    /// Verifica si Windows Defender está activo usando PowerShell externo
    /// </summary>
    public bool IsDefenderEnabled()
    {
        try
        {
            // Usar proceso externo de PowerShell - más confiable que el SDK
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"(Get-MpComputerStatus).RealTimeProtectionEnabled\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null) return true;

            var output = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            Logger.Log($"Defender check output: '{output}', error: '{error}'");

            if (bool.TryParse(output, out bool enabled))
            {
                return enabled;
            }

            // Interpretar respuesta textual
            if (output.Equals("True", StringComparison.OrdinalIgnoreCase))
                return true;
            if (output.Equals("False", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "IsDefenderEnabled");
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
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"$p = Get-MpPreference; if($p.ExclusionPath){$p.ExclusionPath | ForEach-Object { Write-Output \\\"PATH:$_\\\" }}; if($p.ExclusionExtension){$p.ExclusionExtension | ForEach-Object { Write-Output \\\"EXT:$_\\\" }}; if($p.ExclusionProcess){$p.ExclusionProcess | ForEach-Object { Write-Output \\\"PROC:$_\\\" }}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit(10000);

                Logger.Log($"Exclusions output length: {output.Length}, error: '{error}'");

                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("PATH:"))
                        exclusions.Add(trimmed.Substring(5));
                    else if (trimmed.StartsWith("EXT:"))
                        exclusions.Add("*." + trimmed.Substring(4));
                    else if (trimmed.StartsWith("PROC:"))
                        exclusions.Add("[Proceso] " + trimmed.Substring(5));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "GetCurrentExclusions");
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
            var escapedPath = path.Replace("'", "''");
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Remove-MpPreference -ExclusionPath '{escapedPath}'\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit(10000);
                
                if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(error))
                {
                    Logger.Log($"Error removing exclusion: {error}");
                    return false;
                }
            }
            
            _knownExclusions.Remove(path);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "RemoveExclusion");
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
            var script = @"
$status = Get-MpComputerStatus
Write-Output ""RealTimeEnabled=$($status.RealTimeProtectionEnabled)""
Write-Output ""BehaviorMonitor=$($status.BehaviorMonitorEnabled)""
Write-Output ""SignatureVersion=$($status.AntivirusSignatureVersion)""
Write-Output ""SignatureAge=$($status.AntivirusSignatureAge)""
Write-Output ""QuickScanAge=$($status.QuickScanAge)""
Write-Output ""LastScan=$($status.FullScanEndTime)""
";
            
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"").Replace("\n", " ")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10000);

                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Trim().Split('=', 2);
                    if (parts.Length != 2) continue;

                    var key = parts[0];
                    var value = parts[1];

                    switch (key)
                    {
                        case "RealTimeEnabled":
                            info.RealTimeProtectionEnabled = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "BehaviorMonitor":
                            info.BehaviorMonitorEnabled = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "SignatureVersion":
                            info.SignatureVersion = value;
                            break;
                        case "SignatureAge":
                            if (int.TryParse(value, out int sigAge))
                                info.SignatureAgeDays = sigAge;
                            break;
                        case "QuickScanAge":
                            if (int.TryParse(value, out int scanAge))
                                info.QuickScanAgeDays = scanAge;
                            break;
                        case "LastScan":
                            if (DateTime.TryParse(value, out DateTime lastScan))
                                info.LastFullScan = lastScan;
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "GetDefenderInfo");
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
