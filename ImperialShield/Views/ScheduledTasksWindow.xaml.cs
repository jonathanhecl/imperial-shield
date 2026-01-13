using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
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
        public DateTime? NextRunTime { get; set; }
        public DateTime? LastRunTime { get; set; }
        
        // UI Helpers
        public string TranslatedState => State switch
        {
            "Running" => "Ejecutando",
            "Ready" => "Listo",
            "Disabled" => "Deshabilitado",
            "Queued" => "En Cola",
            "Unknown" => "Desconocido",
            _ => State
        };

        public Brush StateColor => State switch
        {
            "Running" => new SolidColorBrush(Color.FromRgb(77, 168, 218)), // Blue
            "Ready" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),   // Green
            "Disabled" => new SolidColorBrush(Color.FromRgb(148, 163, 184)), // Gray
            "Queued" => new SolidColorBrush(Color.FromRgb(234, 179, 8)),  // Yellow
            _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
        };

        public string NextRunText => NextRunTime.HasValue && NextRunTime.Value > DateTime.MinValue 
            ? NextRunTime.Value.ToString("g") 
            : "No programado";
    }

    private async Task LoadTasksAsync()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        TasksGrid.ItemsSource = null;

        try
        {
            // Execute PowerShell to get tasks as JSON
            // We select TaskName, TaskPath, State, and NextRunTime from Get-ScheduledTaskInfo
            // Note: Getting Info for all tasks is slow, so we optimize.
            // Fast mode: Get-ScheduledTask | Select TaskName, TaskPath, State
            string psCommand = "Get-ScheduledTask | Select-Object TaskName, TaskPath, State | ConvertTo-Json -Depth 2";
            
            var tasks = await RunPowerShellAsync<List<TaskItem>>(psCommand);
            
            if (tasks != null)
            {
                // Sort: Running first, then custom paths (root \), then others, then Disabled last
                var sortedTasks = tasks
                    .OrderByDescending(t => t.State == "Running")
                    .ThenBy(t => t.State == "Disabled")
                    .ThenBy(t => t.TaskPath == "\\")
                    .ThenBy(t => t.TaskName)
                    .ToList();

                TasksGrid.ItemsSource = sortedTasks;
                TaskCountText.Text = sortedTasks.Count.ToString();
                LastUpdatedText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar tareas: {ex.Message}", "Error al obtener tareas");
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async Task<T?> RunPowerShellAsync<T>(string command)
    {
        return await Task.Run(() =>
        {
            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; {command.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8 // Ensure UTF8
                };

                using var proc = Process.Start(info);
                if (proc == null) return default;

                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                if (string.IsNullOrWhiteSpace(output)) return default;

                // Configure serializer to handle case insensitivity
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // Handle single object vs array return from PS
                if (output.Trim().StartsWith("{"))
                {
                    output = $"[{output}]";
                }

                return JsonSerializer.Deserialize<T>(output, options);
            }
            catch
            {
                return default;
            }
        });
    }

    private async void RunPowerShellAction(string action, string taskName, string taskPath)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        try
        {
            // Escape path and name
            string cmd = $"{action} -TaskName '{taskName}' -TaskPath '{taskPath}'";
            if (action == "Start-ScheduledTask" || action == "Stop-ScheduledTask")
            {
                // These don't need elevation usually if user owns them, but system ones do.
                // We rely on the app running as Admin.
            }

            await Task.Run(() =>
            {
                var info = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"{cmd}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(info)?.WaitForExit();
            });

            // Refresh after small delay
            await Task.Delay(1000);
            await LoadTasksAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al ejecutar acción: {ex.Message}");
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void RunOrStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaskItem task)
        {
            if (task.State == "Running")
            {
                if (MessageBox.Show($"¿Detener la tarea '{task.TaskName}'?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    RunPowerShellAction("Stop-ScheduledTask", task.TaskName, task.TaskPath);
                }
            }
            else
            {
                RunPowerShellAction("Start-ScheduledTask", task.TaskName, task.TaskPath);
            }
        }
    }

    private void ToggleEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TaskItem task)
        {
            if (task.State == "Disabled")
            {
                 RunPowerShellAction("Enable-ScheduledTask", task.TaskName, task.TaskPath);
            }
            else
            {
                if (MessageBox.Show($"¿Deshabilitar la tarea '{task.TaskName}'?\nLa tarea dejará de ejecutarse automáticamente.", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    RunPowerShellAction("Disable-ScheduledTask", task.TaskName, task.TaskPath);
                }
            }
        }
    }

    // Context Menu Handlers (Reuse logic)
    private void Run_Click(object sender, RoutedEventArgs e)
    {
        if (GetTaskFromMenu(sender) is TaskItem task) RunPowerShellAction("Start-ScheduledTask", task.TaskName, task.TaskPath);
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (GetTaskFromMenu(sender) is TaskItem task) RunPowerShellAction("Stop-ScheduledTask", task.TaskName, task.TaskPath);
    }

    private void Disable_Click(object sender, RoutedEventArgs e)
    {
        if (GetTaskFromMenu(sender) is TaskItem task) RunPowerShellAction("Disable-ScheduledTask", task.TaskName, task.TaskPath);
    }

    private void Enable_Click(object sender, RoutedEventArgs e)
    {
        if (GetTaskFromMenu(sender) is TaskItem task) RunPowerShellAction("Enable-ScheduledTask", task.TaskName, task.TaskPath);
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

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadTasksAsync();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
