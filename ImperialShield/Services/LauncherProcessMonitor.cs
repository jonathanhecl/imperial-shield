using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Collections.Generic;

namespace ImperialShield.Services;

public class LauncherProcessEventArgs : EventArgs
{
    public string LauncherName { get; }
    public string LauncherPath { get; }
    public string SpawnedName { get; }
    public string SpawnedPath { get; }
    public string Details { get; }

    public LauncherProcessEventArgs(string launcherName, string launcherPath, string spawnedName, string spawnedPath, string details)
    {
        LauncherName = launcherName;
        LauncherPath = launcherPath;
        SpawnedName = spawnedName;
        SpawnedPath = spawnedPath;
        Details = details;
    }
}

public class LauncherProcessMonitor : IDisposable
{
    private ManagementEventWatcher? _watcher;
    private bool _isDisposed = false;
    private bool _isRunning = false;

    public event EventHandler<LauncherProcessEventArgs>? SuspiciousProcessSpawned;

    public void Start()
    {
        if (_isRunning) return;

        try
        {
            var query = new WqlEventQuery("__InstanceCreationEvent", new TimeSpan(0, 0, 1), "TargetInstance isa 'Win32_Process'");
            _watcher = new ManagementEventWatcher(query);
            _watcher.EventArrived += OnProcessStarted;
            _watcher.Start();
            _isRunning = true;
            Logger.Log("LauncherProcessMonitor started");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "LauncherProcessMonitor.Start");
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;

        try
        {
            _watcher?.Stop();
            _watcher?.Dispose();
            _watcher = null;
            _isRunning = false;
            Logger.Log("LauncherProcessMonitor stopped");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "LauncherProcessMonitor.Stop");
        }
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        if (_isDisposed) return;
        if (!SettingsManager.Current.ArgentumModeEnabled) return;

        try
        {
            var targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            uint childPid = (uint)targetInstance["ProcessId"];
            uint parentPid = (uint)targetInstance["ParentProcessId"];
            string childName = targetInstance["Name"]?.ToString() ?? "";
            string childPath = targetInstance["ExecutablePath"]?.ToString() ?? "";

            // If path is missing, try to resolve it
            if (string.IsNullOrEmpty(childPath))
            {
                childPath = GetProcessPath((int)childPid) ?? "";
            }

            string parentPath = GetProcessPath((int)parentPid) ?? "";
            if (string.IsNullOrEmpty(parentPath)) return;

            string parentName = Path.GetFileName(parentPath);

            // Check if parent is Argentum Online related launcher/process
            if (DDoSMonitor.IsArgentumProcess(parentName))
            {
                // If launcher is whitelisted, bypass checks
                if (IsAppWhitelisted(parentPath))
                {
                    return;
                }

                if (string.IsNullOrEmpty(childPath)) return;

                // If child process is whitelisted, bypass checks
                if (IsAppWhitelisted(childPath))
                {
                    return;
                }

                string parentDir = Path.GetDirectoryName(parentPath) ?? "";
                string childDir = Path.GetDirectoryName(childPath) ?? "";

                // Rule 1: Allowed if launched in the same directory (standard updater/client execution)
                if (string.Equals(parentDir, childDir, StringComparison.OrdinalIgnoreCase))
                {
                    return; // Permitted!
                }

                // If they are in different directories, check if child is suspicious
                string childNameLower = childName.ToLowerInvariant();
                bool isShellOrSystemUtil = IsShellOrSystemUtility(childNameLower);
                bool isTempOrSystemPath = IsTemporaryOrSystemPath(childPath);

                if (isShellOrSystemUtil || isTempOrSystemPath)
                {
                    // Block immediately! Kill the child process
                    KillProcess((int)childPid);

                    string details = isShellOrSystemUtil 
                        ? $"Intento de ejecutar una consola o utilidad de sistema ({childName}) fuera de la carpeta del juego."
                        : $"Intento de ejecutar un binario en una ruta temporal o de descargas ({childPath}).";

                    Logger.Log($"[ARGENTUM BLOCK] Launcher '{parentName}' tried to spawn suspicious process '{childName}' in '{childPath}'. Blocked.");
                    
                    SuspiciousProcessSpawned?.Invoke(this, new LauncherProcessEventArgs(
                        parentName, parentPath, childName, childPath, details));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "LauncherProcessMonitor.OnProcessStarted");
        }
    }

    private bool IsAppWhitelisted(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var list = SettingsManager.Current.WhitelistedNetworkApps;
        if (list == null) return false;
        return list.Contains(path, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsShellOrSystemUtility(string filename)
    {
        string[] badExecutables = { 
            "cmd.exe", "powershell.exe", "wscript.exe", "cscript.exe", "mshta.exe", 
            "regsvr32.exe", "schtasks.exe", "vssadmin.exe", "certutil.exe", 
            "bash.exe", "curl.exe", "powershell_ise.exe", "wt.exe", "rundll32.exe" 
        };
        return badExecutables.Contains(filename, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsTemporaryOrSystemPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string lowerPath = path.ToLowerInvariant();
        return lowerPath.Contains(@"\appdata\local\temp") || 
               lowerPath.Contains(@"\temp\") || 
               lowerPath.Contains(@"\windows\temp") || 
               lowerPath.Contains(@"\downloads\");
    }

    private string? GetProcessPath(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {processId}");

            foreach (ManagementObject obj in searcher.Get())
            {
                return obj["ExecutablePath"]?.ToString();
            }
        }
        catch { }

        // Fallback: try Process.GetProcessById if WMI queries fail
        try
        {
            using var proc = Process.GetProcessById(processId);
            return proc.MainModule?.FileName;
        }
        catch { }

        return null;
    }

    private void KillProcess(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            proc.Kill(true); // Kill entire process tree
        }
        catch { }
    }

    public void Dispose()
    {
        _isDisposed = true;
        Stop();
    }
}
