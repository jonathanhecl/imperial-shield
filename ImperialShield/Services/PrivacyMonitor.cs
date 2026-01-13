using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace ImperialShield.Services;

public enum DeviceType
{
    Camera,
    Microphone
}

public class PrivacyRisk
{
    public string ApplicationPath { get; }
    public string ApplicationName { get; }
    public DeviceType Device { get; }

    public PrivacyRisk(string appPath, DeviceType device)
    {
        ApplicationPath = appPath;
        ApplicationName = Path.GetFileName(appPath);
        Device = device;
    }
}

public class PrivacyRiskEventArgs : EventArgs
{
    public List<PrivacyRisk> Risks { get; }
    public PrivacyRiskEventArgs(List<PrivacyRisk> risks)
    {
        Risks = risks;
    }
}

public class PrivacyMonitor : IDisposable
{
    private Timer? _timer;
    private readonly int _pollingIntervalMs = 2000; // 2 seconds
    private bool _isDisposed;

    // Track state to avoid spamming alerts, but we want to alert continuously if distinct? 
    // Or just alert when status changes.
    // For the icon, we want it Red as long as there is a risk.
    
    private readonly HashSet<string> _currentRisks = new();

    public event EventHandler<PrivacyRiskEventArgs>? PrivacyRiskDetected;
    public event EventHandler? SafeStateRestored;

    public void Start()
    {
        _timer = new Timer(CheckPrivacyStatus, null, 1000, _pollingIntervalMs);
        Logger.Log("PrivacyMonitor started.");
    }

    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void CheckPrivacyStatus(object? state)
    {
        if (_isDisposed) return;

        try
        {
            var risks = new List<PrivacyRisk>();

            // Check Camera
            var camApps = GetActiveApps("webcam");
            foreach (var app in camApps)
            {
                if (!IsWhitelisted(app, DeviceType.Camera))
                {
                    risks.Add(new PrivacyRisk(app, DeviceType.Camera));
                }
            }

            // Check Microphone
            var micApps = GetActiveApps("microphone");
            foreach (var app in micApps)
            {
                if (!IsWhitelisted(app, DeviceType.Microphone))
                {
                    risks.Add(new PrivacyRisk(app, DeviceType.Microphone));
                }
            }

            if (risks.Any())
            {
                // Logic to debounce or only fire if new?
                // The user says: "Lanzas una notificación... Cambias el icono".
                // We should fire the event consistently or at least let the main app handle the persistent state.
                // Let's fire it always when risks are present, the UI can decide to ignore duplicates.
                // Or better: fire if the set of risks changed OR if it's just periodic heartbeats.
                // Ideally, we fire once when a risk appears.
                
                // Let's create a risk identifier string
                var detectionIds = risks.Select(r => $"{r.ApplicationPath}:{r.Device}").ToHashSet();
                
                // Check if this is exactly the same as last time
                bool isSame = detectionIds.SetEquals(_currentRisks);
                
                _currentRisks.Clear();
                foreach (var id in detectionIds) _currentRisks.Add(id);

                // Always invoke so UI can refresh icon if needed, but maybe UI handles "New" alerts vs "Ongoing".
                PrivacyRiskDetected?.Invoke(this, new PrivacyRiskEventArgs(risks));
            }
            else
            {
                if (_currentRisks.Count > 0)
                {
                    _currentRisks.Clear();
                    SafeStateRestored?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "PrivacyMonitor.CheckPrivacyStatus");
        }
    }

    private List<string> GetActiveApps(string deviceType)
    {
        var activeApps = new List<string>();
        // Registry path
        string basePath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{deviceType}";

        using var key = Registry.CurrentUser.OpenSubKey(basePath);
        if (key == null) return activeApps;

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            // 1. NonPackaged (Classic Exes)
            if (subKeyName.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
            {
                using var nonPackagedKey = key.OpenSubKey(subKeyName);
                if (nonPackagedKey != null)
                {
                    foreach (var appSubKey in nonPackagedKey.GetSubKeyNames())
                    {
                        using var appKey = nonPackagedKey.OpenSubKey(appSubKey);
                        if (CheckAppKey(appKey))
                        {
                             // Key name uses # for \, replace it
                             activeApps.Add(appSubKey.Replace("#", "\\"));
                        }
                    }
                }
            }
            // 2. Packaged Apps (Store Apps) - usually peers of NonPackaged
            else if (!subKeyName.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
            {
                using var packagedKey = key.OpenSubKey(subKeyName);
                // Packaged keys might work differently, but typically follow same structure (LastUsedTimeStop)
                if (CheckAppKey(packagedKey))
                {
                    activeApps.Add(subKeyName); // This will be the Package Family Name
                }
            }
        }

        return activeApps;
    }

    private bool CheckAppKey(RegistryKey? key)
    {
        if (key == null) return false;

        try
        {
            object? stopVal = key.GetValue("LastUsedTimeStop");
            object? startVal = key.GetValue("LastUsedTimeStart");

            if (stopVal is long stopTime)
            {
                // Rule 1: StopTime is 0 (Active)
                if (stopTime == 0) return true;

                // Rule 2: StartTime > StopTime (Active)
                 if (startVal is long startTime)
                 {
                     if (startTime > stopTime) return true;
                 }
            }
        }
        catch 
        {
            // Ignore parsing errors
        }

        return false;
    }

    private bool IsWhitelisted(string appPath, DeviceType type)
    {
        var list = type == DeviceType.Camera 
            ? SettingsManager.Current.WhitelistedCameraApps 
            : SettingsManager.Current.WhitelistedMicrophoneApps;

        // Try to match by full path or just filename
        // Usually registry gives full path for NonPackaged: C:\Program Files\Zoom\bin\zoom.exe
        // Whitelist has: "zoom.exe"
        
        string fileName = Path.GetFileName(appPath);
        
        // Check exact match (for full paths in whitelist) or filename match
        return list.Contains(appPath, StringComparer.OrdinalIgnoreCase) || 
               list.Contains(fileName, StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Stop();
            _timer?.Dispose();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
