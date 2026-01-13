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

    private async Task LoadTasksAsync()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        TasksGrid.ItemsSource = null;

        try
        {
            var tasks = await Task.Run(() => GetScheduledTasksViaSchtasks());
            
            if (tasks != null && tasks.Count > 0)
            {
                // Filter out invalid entries (no real task name, header remnants, etc.)
                var validTasks = tasks
                    .Where(t => !string.IsNullOrWhiteSpace(t.TaskName))
                    .Where(t => t.TaskName != "Nombre de tarea" && t.TaskName != "TaskName")
                    .Where(t => !t.TaskName.Contains("Hora próxima") && !t.TaskName.Contains("Next Run"))
                    .Where(t => t.State != "Estado" && t.State != "Status")
                    .ToList();

                // Sort: Enabled (Ready/Running) first, then by next execution date
                var sortedTasks = validTasks
                    .OrderByDescending(t => t.NormalizedState == "Running")
                    .ThenByDescending(t => t.NormalizedState == "Ready")
                    .ThenBy(t => t.NextRunDateTime ?? DateTime.MaxValue) // Earliest next run first
                    .ThenBy(t => t.TaskName)
                    .ToList();

                TasksGrid.ItemsSource = sortedTasks;
                TaskCountText.Text = sortedTasks.Count.ToString();
            }
            else
            {
                TaskCountText.Text = "0";
                MessageBox.Show("No se pudieron obtener las tareas programadas.\nAsegúrate de ejecutar como Administrador.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            
            LastUpdatedText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar tareas: {ex.Message}", "Error");
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private List<TaskItem> GetScheduledTasksViaSchtasks()
    {
        var tasks = new List<TaskItem>();
        
        try
        {
            // Use schtasks /query which is more reliable
            var info = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = "/query /fo CSV",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(info);
            if (proc == null) return tasks;

            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            // Check for errors
            if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.WriteLine($"schtasks error: {error}");
            }

            if (string.IsNullOrWhiteSpace(output)) 
            {
                Debug.WriteLine("schtasks returned no output");
                return tasks;
            }

            // Parse CSV output
            // Format: "TaskName","Next Run Time","Status"
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Skip header line
            bool isFirstLine = true;
            
            foreach (var line in lines)
            {
                try
                {
                    // Skip header
                    if (isFirstLine)
                    {
                        isFirstLine = false;
                        // Check if this looks like a header
                        if (line.Contains("TaskName") || line.Contains("Nombre de tarea") || 
                            line.Contains("\"Nombre de") || line.Contains("\"TaskName\""))
                        {
                            continue;
                        }
                    }
                    
                    // Parse CSV manually (simple parser for 3 fields)
                    var fields = ParseCsvLine(line);
                    if (fields.Count >= 3)
                    {
                        string fullPath = fields[0];
                        string nextRun = fields[1];
                        string status = fields[2];

                        // Extract task name from path
                        string taskName = fullPath;
                        string taskPath = "\\";
                        
                        int lastSlash = fullPath.LastIndexOf('\\');
                        if (lastSlash >= 0)
                        {
                            taskName = fullPath.Substring(lastSlash + 1);
                            taskPath = fullPath.Substring(0, lastSlash + 1);
                            if (string.IsNullOrEmpty(taskPath)) taskPath = "\\";
                        }

                        // Skip empty, INFO lines, or folder entries
                        if (string.IsNullOrWhiteSpace(taskName)) continue;
                        if (taskName.StartsWith("INFO:")) continue;
                        if (status == "N/D" || status == "N/A") continue; // Folder entries

                        tasks.Add(new TaskItem
                        {
                            TaskName = taskName,
                            TaskPath = taskPath,
                            State = status,
                            NextRunTimeRaw = nextRun
                        });
                    }
                }
                catch
                {
                    // Skip malformed lines
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting tasks: {ex.Message}");
        }

        return tasks;
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
