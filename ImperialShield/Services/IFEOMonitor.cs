using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace ImperialShield.Services;

public class IFEORiskEventArgs : EventArgs
{
    public string ExecutableName { get; }
    public string DebuggerPath { get; }
    public bool IsRogue { get; }

    public IFEORiskEventArgs(string exeName, string debugger, bool isRogue)
    {
        ExecutableName = exeName;
        DebuggerPath = debugger;
        IsRogue = isRogue;
    }
}

public class IFEOMonitor : IDisposable
{
    private const string IFEO_PATH = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
    private Timer? _timer;
    private HashSet<string> _knownIFEOs = new();
    private bool _initialScanDone = false;
    private bool _isDisposed = false;

    public event EventHandler<IFEORiskEventArgs>? RogueIFEODetected;

    public void Start(int intervalMs = 10000)
    {
        _timer = new Timer(CheckIFEOStatus, null, 0, intervalMs);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void CheckIFEOStatus(object? state)
    {
        if (_isDisposed) return;

        try
        {
            using var ifeoKey = Registry.LocalMachine.OpenSubKey(IFEO_PATH);
            if (ifeoKey == null) return;

            var currentSubKeys = ifeoKey.GetSubKeyNames();

            foreach (var subKeyName in currentSubKeys)
            {
                // Ignorar subclaves de sistema conocidas que suelen estar ahí
                if (IsInternalWindowsKey(subKeyName)) continue;

                using var appKey = ifeoKey.OpenSubKey(subKeyName);
                if (appKey == null) continue;

                var debugger = appKey.GetValue("Debugger")?.ToString();
                var quarantinedBy = appKey.GetValue("QuarantinedBy")?.ToString();

                bool hasDebugger = !string.IsNullOrEmpty(debugger);
                bool isOurs = quarantinedBy == "ImperialShield";

                string uniqueId = $"{subKeyName}:{debugger}";

                if (!_knownIFEOs.Contains(uniqueId))
                {
                    // Nueva entrada Detectada
                    if (_initialScanDone && hasDebugger && !isOurs)
                    {
                        Logger.Log($"ROGUE IFEO DETECTED: {subKeyName} redirected to {debugger}");
                        RogueIFEODetected?.Invoke(this, new IFEORiskEventArgs(subKeyName, debugger!, true));
                    }
                    
                    _knownIFEOs.Add(uniqueId);
                }
            }

            _initialScanDone = true;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "IFEOMonitor.CheckIFEOStatus");
        }
    }

    private bool IsInternalWindowsKey(string key)
    {
        // Algunas claves que suelen estar presentes por defecto o por herramientas de sistema
        string[] common = { "DllEntryPoint", "Internal", "IEInstal.exe" };
        return common.Any(c => key.Equals(c, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _isDisposed = true;
        _timer?.Dispose();
    }
}
