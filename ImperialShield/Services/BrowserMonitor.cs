using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace ImperialShield.Services;

public class BrowserMonitor
{
    private Timer? _timer;
    private string? _currentBrowserProgId;
    public string CurrentBrowserName { get; private set; } = "Desconocido";
    public string CurrentBrowserInfo { get; private set; } = "Cargando...";
    public DateTime LastChecked { get; private set; }

    public event EventHandler<BrowserChangedEventArgs>? BrowserChanged;

    public void Start()
    {
        (_currentBrowserProgId, CurrentBrowserName, CurrentBrowserInfo) = GetDefaultBrowserInfo();
        LastChecked = DateTime.Now;

        int interval = SettingsManager.Current.PollingIntervalMs;
        _timer = new Timer(CheckBrowser, null, 15000, interval);
        Logger.Log($"BrowserMonitor started. Current: {CurrentBrowserName} ({_currentBrowserProgId})");
    }

    public void ForceInitialLoad()
    {
        try
        {
            (_currentBrowserProgId, CurrentBrowserName, CurrentBrowserInfo) = GetDefaultBrowserInfo();
            LastChecked = DateTime.Now;
            Logger.Log($"BrowserMonitor force loaded: {CurrentBrowserName}");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "BrowserMonitor.ForceInitialLoad");
        }
    }

    private void CheckBrowser(object? state)
    {
        try
        {
            LastChecked = DateTime.Now;
            var (newProgId, newName, newInfo) = GetDefaultBrowserInfo();

            if (newProgId != _currentBrowserProgId)
            {
                Logger.Log($"Default browser change detected! From {_currentBrowserProgId} to {newProgId}");
                
                var oldProgId = _currentBrowserProgId;
                var oldName = CurrentBrowserName;
                
                _currentBrowserProgId = newProgId;
                CurrentBrowserName = newName;
                CurrentBrowserInfo = newInfo;

                BrowserChanged?.Invoke(this, new BrowserChangedEventArgs 
                { 
                    OldProgId = oldProgId, 
                    NewProgId = newProgId,
                    OldName = oldName,
                    NewName = newName
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "CheckBrowser");
        }
    }

    private (string? progId, string name, string info) GetDefaultBrowserInfo()
    {
        string? progId = null;
        string name = "Desconocido";
        string info = "No se pudo determinar";

        try
        {
            // Modern Windows default browser location
            using var userChoiceKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
            if (userChoiceKey != null)
            {
                progId = userChoiceKey.GetValue("ProgId")?.ToString();
            }

            if (string.IsNullOrEmpty(progId))
            {
                // Fallback to older method
                using var shellKey = Registry.ClassesRoot.OpenSubKey(@"http\shell\open\command");
                if (shellKey != null)
                {
                    string command = shellKey.GetValue("")?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(command))
                    {
                        info = command.Trim('"');
                        name = Path.GetFileNameWithoutExtension(info.Split(' ')[0].Trim('"'));
                    }
                }
                return (progId, name, info);
            }

            // Get friendly name and executable from ProgId
            using var progKey = Registry.ClassesRoot.OpenSubKey(progId);
            if (progKey != null)
            {
                name = progKey.GetValue("")?.ToString() ?? progId;
                
                // If it's something like "@C:\Program Files\Google\Chrome\Application\chrome.exe,-123"
                if (name.StartsWith("@"))
                {
                    // Try to get a better name or just use the progId if it fails
                    name = GetFriendlyNameFromProgId(progId) ?? progId;
                }

                using var commandKey = progKey.OpenSubKey(@"shell\open\command");
                if (commandKey != null)
                {
                    string command = commandKey.GetValue("")?.ToString() ?? "";
                    info = command.Trim('"').Split(' ')[0].Trim('"');
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "GetDefaultBrowserInfo");
        }

        return (progId, name, info);
    }

    private string? GetFriendlyNameFromProgId(string progId)
    {
        if (progId.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) return "Google Chrome";
        if (progId.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) return "Mozilla Firefox";
        if (progId.Contains("MSEdge", StringComparison.OrdinalIgnoreCase)) return "Microsoft Edge";
        if (progId.Contains("Brave", StringComparison.OrdinalIgnoreCase)) return "Brave Browser";
        if (progId.Contains("Opera", StringComparison.OrdinalIgnoreCase)) return "Opera";
        return null;
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }
}

public class BrowserChangedEventArgs : EventArgs
{
    public string? OldProgId { get; set; }
    public string? NewProgId { get; set; }
    public string? OldName { get; set; }
    public string? NewName { get; set; }
}
