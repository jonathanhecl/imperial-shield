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
    public bool IsNonPackaged { get; }

    public PrivacyRisk(string appPath, DeviceType device, bool isNonPackaged = true)
    {
        ApplicationPath = appPath;
        ApplicationName = Path.GetFileName(appPath);
        Device = device;
        IsNonPackaged = isNonPackaged;
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

public class NewPrivacyAppEventArgs : EventArgs
{
    public PrivacyRisk App { get; }
    public NewPrivacyAppEventArgs(PrivacyRisk app)
    {
        App = app;
    }
}

public class PrivacyMonitor : IDisposable
{
    private Timer? _timer;
    private bool _isDisposed;

    // Track known apps to detect NEW ones
    private readonly HashSet<string> _knownCameraApps = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownMicrophoneApps = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialScanDone = false;

    // Track current active risks
    private readonly HashSet<string> _currentRisks = new();

    public event EventHandler<PrivacyRiskEventArgs>? PrivacyRiskDetected;
    public DateTime LastChecked { get; private set; }
    public int ActiveRiskCount 
    {
        get
        {
            var camApps = GetActiveApps("webcam");
            var micApps = GetActiveApps("microphone");
            int count = 0;
            foreach (var app in camApps) if (!IsWhitelisted(app, DeviceType.Camera)) count++;
            foreach (var app in micApps) if (!IsWhitelisted(app, DeviceType.Microphone)) count++;
            return count;
        }
    }
    public event EventHandler? SafeStateRestored;
    public event EventHandler<NewPrivacyAppEventArgs>? NewPrivacyAppDetected;

    public void Start()
    {
        // Use configured polling interval
        int interval = SettingsManager.Current.PollingIntervalMs;
        
        // Do initial scan to populate known apps (don't alert on startup)
        ScanAndPopulateKnownApps();
        _initialScanDone = true;
        LastChecked = DateTime.Now;

        _timer = new Timer(CheckPrivacyStatus, null, interval, interval);
        Logger.Log($"PrivacyMonitor started with interval: {interval}ms");
    }

    /// <summary>
    /// Fuerza la carga inicial inmediata de datos
    /// </summary>
    public void ForceInitialLoad()
    {
        try
        {
            ScanAndPopulateKnownApps();
            _initialScanDone = true;
            LastChecked = DateTime.Now;
            Logger.Log($"PrivacyMonitor force loaded {_currentRisks.Count} risks");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "ForceInitialLoad");
        }
    }

    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void ScanAndPopulateKnownApps()
    {
        try
        {
            // Populate known camera apps
            var camApps = GetAllAppIds("webcam");
            foreach (var app in camApps)
                _knownCameraApps.Add(app);

            // Populate known mic apps
            var micApps = GetAllAppIds("microphone");
            foreach (var app in micApps)
                _knownMicrophoneApps.Add(app);

            Logger.Log($"Initial privacy scan: {_knownCameraApps.Count} camera apps, {_knownMicrophoneApps.Count} mic apps");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "ScanAndPopulateKnownApps");
        }
    }

    private void CheckPrivacyStatus(object? state)
    {
        if (_isDisposed) return;

        try
        {
            LastChecked = DateTime.Now;
            // Check for NEW apps added since last scan
            CheckForNewApps(DeviceType.Camera);
            CheckForNewApps(DeviceType.Microphone);

            // Check for active risks (apps currently using the device)
            var risks = new List<PrivacyRisk>();

            var camApps = GetActiveApps("webcam");
            foreach (var app in camApps)
            {
                if (!IsWhitelisted(app, DeviceType.Camera))
                {
                    risks.Add(new PrivacyRisk(app, DeviceType.Camera));
                }
            }

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
                var detectionIds = risks.Select(r => $"{r.ApplicationPath}:{r.Device}").ToHashSet();
                bool hasNewRisks = !detectionIds.SetEquals(_currentRisks);
                
                _currentRisks.Clear();
                foreach (var id in detectionIds) _currentRisks.Add(id);

                // Only fire if there are new risks (avoid spamming)
                if (hasNewRisks)
                {
                    PrivacyRiskDetected?.Invoke(this, new PrivacyRiskEventArgs(risks));
                }
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

    private void CheckForNewApps(DeviceType type)
    {
        var knownSet = type == DeviceType.Camera ? _knownCameraApps : _knownMicrophoneApps;
        string deviceStr = type == DeviceType.Camera ? "webcam" : "microphone";

        var currentApps = GetAllAppIds(deviceStr);

        foreach (var appId in currentApps)
        {
            if (!knownSet.Contains(appId))
            {
                // New app detected!
                knownSet.Add(appId);

                if (_initialScanDone)
                {
                    string displayName = Path.GetFileName(appId);
                    Logger.Log($"NEW PRIVACY APP: {displayName} ({type})");
                    
                    var risk = new PrivacyRisk(appId, type);
                    NewPrivacyAppDetected?.Invoke(this, new NewPrivacyAppEventArgs(risk));
                }
            }
        }
    }

    private List<string> GetAllAppIds(string deviceType)
    {
        var apps = new List<string>();
        string basePath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{deviceType}";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(basePath);
            if (key == null) return apps;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                if (subKeyName.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
                {
                    using var nonPackagedKey = key.OpenSubKey(subKeyName);
                    if (nonPackagedKey != null)
                    {
                        foreach (var appSubKey in nonPackagedKey.GetSubKeyNames())
                        {
                            apps.Add(appSubKey.Replace("#", "\\"));
                        }
                    }
                }
                else
                {
                    apps.Add(subKeyName);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "GetAllAppIds");
        }

        return apps;
    }

    private List<string> GetActiveApps(string deviceType)
    {
        var activeApps = new List<string>();
        string basePath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{deviceType}";

        using var key = Registry.CurrentUser.OpenSubKey(basePath);
        if (key == null) return activeApps;

        foreach (var subKeyName in key.GetSubKeyNames())
        {
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
                            activeApps.Add(appSubKey.Replace("#", "\\"));
                        }
                    }
                }
            }
            else if (!subKeyName.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
            {
                using var packagedKey = key.OpenSubKey(subKeyName);
                if (CheckAppKey(packagedKey))
                {
                    activeApps.Add(subKeyName);
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
                if (stopTime == 0) return true;
                if (startVal is long startTime && startTime > stopTime) return true;
            }
        }
        catch { }

        return false;
    }

    public class PrivacyAppHistory
    {
        public string AppId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsNonPackaged { get; set; }
        public DateTime LastUsedStart { get; set; }
        public DateTime LastUsedStop { get; set; }
        public bool IsActive { get; set; }
        public string PermissionStatus { get; set; } = "Allow";
    }

    public List<PrivacyAppHistory> GetAllAppsWithPermission(DeviceType type)
    {
        var list = new List<PrivacyAppHistory>();
        string deviceStr = type == DeviceType.Camera ? "webcam" : "microphone";
        string basePath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{deviceStr}";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(basePath);
            if (key == null) return list;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                if (subKeyName.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
                {
                    using var nonPackagedKey = key.OpenSubKey(subKeyName);
                    if (nonPackagedKey != null)
                    {
                        foreach (var appSubKey in nonPackagedKey.GetSubKeyNames())
                        {
                            using var appKey = nonPackagedKey.OpenSubKey(appSubKey);
                            if (appKey != null)
                            {
                                var app = ParseAppKey(appKey, appSubKey, true);
                                if (app != null) list.Add(app);
                            }
                        }
                    }
                }
                else
                {
                    using var packagedKey = key.OpenSubKey(subKeyName);
                    if (packagedKey != null)
                    {
                        var app = ParseAppKey(packagedKey, subKeyName, false);
                        if (app != null) list.Add(app);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "GetAllAppsWithPermission");
        }

        return list;
    }

    private PrivacyAppHistory? ParseAppKey(RegistryKey key, string keyName, bool isNonPackaged)
    {
        try
        {
            long start = (long)(key.GetValue("LastUsedTimeStart") ?? 0L);
            long stop = (long)(key.GetValue("LastUsedTimeStop") ?? 0L);
            string val = (string)(key.GetValue("Value") ?? "Allow");

            var app = new PrivacyAppHistory
            {
                AppId = isNonPackaged ? keyName.Replace("#", "\\") : keyName,
                IsNonPackaged = isNonPackaged,
                LastUsedStart = DateTime.FromFileTime(start),
                LastUsedStop = stop == 0 ? DateTime.MinValue : DateTime.FromFileTime(stop),
                IsActive = stop == 0 || start > stop,
                PermissionStatus = val
            };

            if (isNonPackaged)
                app.DisplayName = Path.GetFileName(app.AppId);
            else
                app.DisplayName = keyName.Split('_').FirstOrDefault() ?? keyName;

            return app;
        }
        catch
        {
            return null;
        }
    }

    public void RevokePermission(string appId, bool isNonPackaged, DeviceType type)
    {
        try
        {
            string deviceStr = type == DeviceType.Camera ? "webcam" : "microphone";
            string path;

            if (isNonPackaged)
            {
                string safeKey = appId.Replace("\\", "#");
                path = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{deviceStr}\NonPackaged\{safeKey}";
            }
            else
            {
                path = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{deviceStr}\{appId}";
            }

            using var key = Registry.CurrentUser.OpenSubKey(path, true);
            if (key != null)
            {
                key.SetValue("Value", "Deny");
                Logger.Log($"Revoked {type} permission for: {appId}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "RevokePermission");
        }
    }

    private bool IsWhitelisted(string appPath, DeviceType type)
    {
        var list = type == DeviceType.Camera
            ? SettingsManager.Current.WhitelistedCameraApps
            : SettingsManager.Current.WhitelistedMicrophoneApps;

        string fileName = Path.GetFileName(appPath);
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
