using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ImperialShield.Views;

public partial class ScheduledTasksWindow : Window
{
    public ScheduledTasksWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadTasksAsync();
    }

    public class TaskItem
    {
        public string TaskName { get; set; } = string.Empty;
        public string TaskPath { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string NextRunTimeRaw { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        
        // Security Props
        public bool IsMicrosoft { get; set; }
        public bool IsTrustedPublisher { get; set; }
        public bool IsSuspicious { get; set; }
        public int ThreatLevel { get; set; } // 0=Safe, 1=Unknown, 2=Medium, 3=High

        public string ThreatIcon => ThreatLevel switch
        {
            3 => "⚠️", // High (Red in UI via logic or just icon)
            2 => "⚠️", // Medium
            0 => IsMicrosoft ? "📦" : "🛡️", // Trusted
            _ => "🛡️" // Unknown but low threat default
        };

        public string ThreatLevelText => ThreatLevel switch
        {
            3 => "Peligro Alto (Ubicación o tipo sospechoso)",
            2 => "Sospechoso (Script no firmado)",
            0 => "Confiable (Microsoft / Firmado)",
            _ => "Desconocido (Terceros)"
        };
        
        // UI Helpers
        public string TranslatedState => State switch
        {
            "Running" => "Ejecutando",
            "Ready" => "Listo",
            "Disabled" => "Deshabilitado",
            "Queued" => "En Cola",
            "Unknown" => "Desconocido",
            // Spanish states from schtasks
            "Listo" => "Listo",
            "Deshabilitado" => "Deshabilitado",
            "En ejecución" => "Ejecutando",
            _ => State
        };
        
        // Normalize state for comparisons
        public string NormalizedState => State switch
        {
            "Listo" or "Ready" => "Ready",
            "Deshabilitado" or "Disabled" => "Disabled",
            "En ejecución" or "Running" => "Running",
            _ => State
        };

        public Brush StateColor => NormalizedState switch
        {
            "Running" => new SolidColorBrush(Color.FromRgb(77, 168, 218)), // Blue
            "Ready" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),   // Green
            "Disabled" => new SolidColorBrush(Color.FromRgb(148, 163, 184)), // Gray
            "Queued" => new SolidColorBrush(Color.FromRgb(234, 179, 8)),  // Yellow
            _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
        };

        public string NextRunText => string.IsNullOrWhiteSpace(NextRunTimeRaw) || 
            NextRunTimeRaw == "N/D" || NextRunTimeRaw == "N/A" ||
            NextRunTimeRaw.Contains("Hora próxima") || NextRunTimeRaw.Contains("Next Run")
            ? "No programado" 
            : NextRunTimeRaw;

        // Parse date for sorting
        public DateTime? NextRunDateTime
        {
            get
            {
                if (string.IsNullOrWhiteSpace(NextRunTimeRaw) || 
                    NextRunTimeRaw == "N/D" || NextRunTimeRaw == "N/A" ||
                    NextRunTimeRaw.Contains("Hora") || NextRunTimeRaw.Contains("Next"))
                    return null;
                    
                if (DateTime.TryParse(NextRunTimeRaw, out var dt))
                    return dt;
                return null;
            }
        }
    }

    private List<TaskItem> _allTasks = new();
    private bool _showOnlySuspicious = false;

    private void FilterToggle_Click(object sender, RoutedEventArgs e)
    {
        _showOnlySuspicious = !_showOnlySuspicious;
        UpdateFilterButtonText();
        ApplyFilter();
    }

    private void UpdateFilterButtonText()
    {
        // Update button content dynamically
        if (FilterToggleBtn.Template.FindName("border", FilterToggleBtn) is System.Windows.Controls.Border border)
        {
            if (border.Child is StackPanel sp && sp.Children.Count >= 2)
            {
                if (sp.Children[0] is TextBlock iconTb && sp.Children[1] is TextBlock textTb)
                {
                    if (_showOnlySuspicious)
                    {
                        iconTb.Text = "⚠️";
                        textTb.Text = "Solo Sospechosas";
                        textTb.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                        border.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
                    else
                    {
                        iconTb.Text = "👁️";
                        textTb.Text = "Mostrar Todo";
                        textTb.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Gray
                        border.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                    }
                }
            }
        }
    }

    private void ApplyFilter()
    {
        if (_allTasks == null || !_allTasks.Any()) return;

        var filtered = _allTasks.ToList();

        if (_showOnlySuspicious)
        {
            // Show only Non-Microsoft or Suspicious
            filtered = filtered.Where(t => t.IsSuspicious || (!t.IsMicrosoft && !t.IsTrustedPublisher)).ToList();
        }

        // Sort: Suspicious (if high threat) -> Running -> Ready -> Date -> Name
        var sorted = filtered
            .OrderByDescending(t => t.ThreatLevel == 3) // High Threat (Always top attention)
            .ThenByDescending(t => t.ThreatLevel == 2) // Medium
            .ThenByDescending(t => t.NormalizedState == "Running") // 1. Running
            .ThenByDescending(t => t.NormalizedState == "Ready")   // 2. Ready
            .ThenBy(t => t.NextRunDateTime ?? DateTime.MaxValue)   // 3. Date (Earliest first)
            .ThenBy(t => t.TaskName)                               // 4. Name fallback
            .ToList();

        TasksGrid.ItemsSource = sorted;
        TaskCountText.Text = sorted.Count.ToString();
    }

    private async Task LoadTasksAsync()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        TasksGrid.ItemsSource = null;

        try
        {
            var tasks = await Task.Run(() => GetScheduledTasksViaSchtasksVerbose());
            
            if (tasks != null && tasks.Count > 0)
            {
                // Filter only exact header matches, not partial
                _allTasks = tasks
                    .Where(t => !string.IsNullOrWhiteSpace(t.TaskName))
                    .Where(t => t.TaskName != "TaskName" && t.TaskName != "Nombre de tarea")
                    .Where(t => t.State != "Estado" && t.State != "Status")
                    .ToList();

                ApplyFilter();
            }
            else
            {
                TaskCountText.Text = "0";
            }
            
            LastUpdatedText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar tareas: {ex.Message}");
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private List<TaskItem> GetScheduledTasksViaSchtasksVerbose()
    {
        var tasks = new List<TaskItem>();
        
        try
        {
            // Use advanced PS command to get State AND NextRunTime effectively
            // Get-ScheduledTask + Get-ScheduledTaskInfo combo
            // We use a calculated property for NextRunTime
            // Warning: This is slower than simple list but required for sorting
            string psCommand = "Get-ScheduledTask | Select-Object TaskName, TaskPath, State, @{N='NextRunTime';E={((Get-ScheduledTaskInfo -TaskName $_.TaskName -TaskPath $_.TaskPath).NextRunTime)}} | ConvertTo-Csv -NoTypeInformation";
            
            var info = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var proc = Process.Start(info);
            if (proc == null) return tasks;

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            if (string.IsNullOrWhiteSpace(output)) return tasks;

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            bool isFirst = true; 
            
            foreach (var line in lines)
            {
                if (isFirst) { isFirst = false; continue; } 

                var fields = ParseCsvLine(line);
                if (fields.Count < 3) continue; // Need nice robust parsing

                string taskName = fields[0];
                string taskPath = fields[1];
                string state = fields[2];
                string nextRun = fields.Count > 3 ? fields[3] : "";

                var item = new TaskItem
                {
                    TaskName = taskName,
                    TaskPath = taskPath,
                    State = state,
                    NextRunTimeRaw = nextRun // Keep raw string for now
                };
                
                AnalyzeSecurity(item);
                tasks.Add(item);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error PS: " + ex.Message);
        }
        return tasks;
    }

    private string GetField(List<string> fields, string[] headers, string enName, string esName, int defaultIndex)
    {
        int idx = defaultIndex;
        if (idx < fields.Count) return fields[idx];
        return "";
    }

    private void AnalyzeSecurity(TaskItem item)
    {
        // 1. Check if Microsoft task by TaskPath
        // Tasks in \Microsoft\ folder are Windows system tasks
        if (item.TaskPath.StartsWith("\\Microsoft\\", StringComparison.OrdinalIgnoreCase))
        {
            item.IsMicrosoft = true;
            item.IsTrustedPublisher = true;
            item.ThreatLevel = 0;
            return;
        }

        // 2. Known third-party trusted paths
        string[] trustedPaths = { "\\Google\\", "\\Adobe\\", "\\Intel\\", "\\NVIDIA\\", "\\AMD\\", "\\Mozilla\\" };
        foreach (var tp in trustedPaths)
        {
            if (item.TaskPath.Contains(tp, StringComparison.OrdinalIgnoreCase))
            {
                item.IsTrustedPublisher = true;
                item.ThreatLevel = 0;
                return;
            }
        }

        // 3. Root level tasks without known path - potentially suspicious
        if (item.TaskPath == "\\" || item.TaskPath.Length < 3)
        {
            // Root tasks from unknown sources
            item.ThreatLevel = 1; // Unknown
            return;
        }

        item.ThreatLevel = 1; // Default unknown
    }

    private List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        string currentField = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(currentField);
                currentField = "";
            }
            else
            {
                currentField += c;
            }
        }
        fields.Add(currentField);

        return fields;
    }

    private async void RunPowerShellAction(string action, string fullTaskPath)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        try
        {
            await Task.Run(() =>
            {
                // Use schtasks for actions (more reliable than PS cmdlets)
                string cmd = action switch
                {
                    "Start" => $"/run /tn \"{fullTaskPath}\"",
                    "End" => $"/end /tn \"{fullTaskPath}\"",
                    "Enable" => $"/change /tn \"{fullTaskPath}\" /enable",
                    "Disable" => $"/change /tn \"{fullTaskPath}\" /disable",
                    _ => ""
                };

                if (string.IsNullOrEmpty(cmd)) return;

                var info = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = cmd,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                
                var p = Process.Start(info);
                p?.WaitForExit(5000);
            });

            // Refresh after action
            await Task.Delay(500);
            await LoadTasksAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al ejecutar acción: {ex.Message}");
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private string GetFullTaskPath(TaskItem task)
    {
        // Combine path and name
        return task.TaskPath.TrimEnd('\\') + "\\" + task.TaskName;
    }

    private void RunOrStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaskItem task)
        {
            string fullPath = GetFullTaskPath(task);
            
            if (task.NormalizedState == "Running")
            {
                if (MessageBox.Show($"¿Detener la tarea '{task.TaskName}'?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    RunPowerShellAction("End", fullPath);
                }
            }
            else
            {
                RunPowerShellAction("Start", fullPath);
            }
        }
    }

    private void ToggleEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaskItem task)
        {
            string fullPath = GetFullTaskPath(task);
            
            if (task.NormalizedState == "Disabled")
            {
                RunPowerShellAction("Enable", fullPath);
            }
            else
            {
                if (MessageBox.Show($"¿Deshabilitar la tarea '{task.TaskName}'?\nLa tarea dejará de ejecutarse automáticamente.", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    RunPowerShellAction("Disable", fullPath);
                }
            }
        }
    }

    // Context Menu Handlers
    private void Run_Click(object sender, RoutedEventArgs e)
    {
        if (GetTaskFromMenu(sender) is TaskItem task) 
            RunPowerShellAction("Start", GetFullTaskPath(task));
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (GetTaskFromMenu(sender) is TaskItem task) 
            RunPowerShellAction("End", GetFullTaskPath(task));
    }

    private void Disable_Click(object sender, RoutedEventArgs e)
    {
        if (GetTaskFromMenu(sender) is TaskItem task) 
            RunPowerShellAction("Disable", GetFullTaskPath(task));
    }

    private void Enable_Click(object sender, RoutedEventArgs e)
    {
        if (GetTaskFromMenu(sender) is TaskItem task) 
            RunPowerShellAction("Enable", GetFullTaskPath(task));
    }

    private TaskItem? GetTaskFromMenu(object sender)
    {
        if (sender is MenuItem mi && mi.DataContext is TaskItem task) return task;
        return null;
    }

    private void OpenScheduler_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "taskschd.msc", UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo abrir el Programador de Tareas: " + ex.Message);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadTasksAsync();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
