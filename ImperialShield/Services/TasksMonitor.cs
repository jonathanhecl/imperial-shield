using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ImperialShield.Services;

public class NewTaskEventArgs : EventArgs
{
    public string TaskName { get; }
    public string TaskPath { get; }
    
    public NewTaskEventArgs(string name, string path)
    {
        TaskName = name;
        TaskPath = path;
    }
}

public class TasksMonitor : IDisposable
{
    private Timer? _timer;
    private readonly HashSet<string> _knownTasks = new(StringComparer.OrdinalIgnoreCase);
    private bool _isInitialized = false;

    public event EventHandler<NewTaskEventArgs>? NewTaskDetected;
    public DateTime LastChecked { get; private set; }
    public int ItemCount => _knownTasks.Count;

    public void Start()
    {
        // Initial scan - don't alert, just learn
        ScanTasks(isInitial: true);
        
        // Check every 60 seconds (or configured interval)
        int interval = SettingsManager.Current.PollingIntervalMs;
        if (interval < 10000) interval = 10000; // Minimum 10s for tasks as PS is heavy

        _timer = new Timer(OnTimerTick, null, interval, interval);
        Logger.Log("TasksMonitor started.");
    }

    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void OnTimerTick(object? state)
    {
        ScanTasks(isInitial: false);
    }

    private void ScanTasks(bool isInitial)
    {
        try
        {
            LastChecked = DateTime.Now;
            var currentTasks = GetCurrentTasks();
            
            if (isInitial)
            {
                foreach (var t in currentTasks)
                {
                    _knownTasks.Add(GetKey(t));
                }
                _isInitialized = true;
                Logger.Log($"TasksMonitor initialized with {_knownTasks.Count} tasks.");
            }
            else
            {
                if (!_isInitialized) return; // Safety

                foreach (var t in currentTasks)
                {
                    string key = GetKey(t);
                    if (!_knownTasks.Contains(key))
                    {
                        // New task found!
                        Logger.Log($"New scheduled task detected: {t.Name} ({t.Path})");
                        _knownTasks.Add(key); // Add to known so we don't alert again
                        
                        NewTaskDetected?.Invoke(this, new NewTaskEventArgs(t.Name, t.Path));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error scanning tasks: {ex.Message}");
        }
    }

    private string GetKey(SimpleTask t) => $"{t.Path}\\{t.Name}";

    private List<SimpleTask> GetCurrentTasks()
    {
        var list = new List<SimpleTask>();
        try
        {
            // Lightweight PS command just for names
            string psCommand = "Get-ScheduledTask | Select-Object TaskName, TaskPath | ConvertTo-Csv -NoTypeInformation";
            
            var info = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var proc = Process.Start(info);
            if (proc == null) return list;

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            if (string.IsNullOrWhiteSpace(output)) return list;

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            bool isFirst = true;
            
            foreach (var line in lines)
            {
                if (isFirst) { isFirst = false; continue; }

                var parts = ParseCsv(line);
                if (parts.Count >= 2)
                {
                    list.Add(new SimpleTask { Name = parts[0], Path = parts[1] });
                }
            }
        }
        catch (Exception ex) { Debug.WriteLine("Task scan error: " + ex.Message); }
        
        return list;
    }

    private List<string> ParseCsv(string line)
    {
        var res = new List<string>();
        // Simple CSV parser for PS output (always quoted)
        var sb = new StringBuilder();
        bool inQuotes = false;
        foreach(char c in line)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes) {
                res.Add(sb.ToString());
                sb.Clear();
            }
            else sb.Append(c);
        }
        res.Add(sb.ToString());
        return res;
    }

    private class SimpleTask
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
