using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace ImperialShield.Services;

public class StartupMonitor
{
    private Timer? _timer;
    private HashSet<string> _knownStartupApps = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _appName = "ImperialShield";

    public event EventHandler<string>? NewStartupAppDetected;

    public void Start()
    {
        _knownStartupApps = GetCurrentStartupApps();
        
        int interval = SettingsManager.Current.PollingIntervalMs;
        _timer = new Timer(CheckStartupApps, null, 10000, interval);
        Logger.Log($"StartupMonitor started with interval: {interval}ms");
    }

    private void CheckStartupApps(object? state)
    {
        try
        {
            var currentApps = GetCurrentStartupApps();
            var newApps = currentApps.Except(_knownStartupApps).ToList();

            foreach (var app in newApps)
            {
                // Ignorar a nosotros mismos
                if (app.Contains(_appName, StringComparison.OrdinalIgnoreCase)) continue;

                Logger.Log($"New startup app detected: {app}");
                NewStartupAppDetected?.Invoke(this, app);
            }

            _knownStartupApps = currentApps;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "CheckStartupApps");
        }
    }

    private HashSet<string> GetCurrentStartupApps()
    {
        var apps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            // Registro de usuario actual
            using var cuRun = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (cuRun != null)
            {
                foreach (var name in cuRun.GetValueNames())
                {
                    apps.Add($"{name} (User)");
                }
            }

            // Registro de máquina local
            using var lmRun = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (lmRun != null)
            {
                foreach (var name in lmRun.GetValueNames())
                {
                    apps.Add($"{name} (System)");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "GetCurrentStartupApps");
        }

        return apps;
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
