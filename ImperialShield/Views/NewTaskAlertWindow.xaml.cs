using System;
using System.Diagnostics;
using System.Windows;

namespace ImperialShield.Views;

public partial class NewTaskAlertWindow : Window
{
    private readonly string _taskName;
    private readonly string _taskPath;

    public NewTaskAlertWindow(string taskName, string taskPath)
    {
        InitializeComponent();
        _taskName = taskName;
        _taskPath = taskPath;

        TaskNameText.Text = taskName;
        TaskPathText.Text = string.IsNullOrEmpty(taskPath) ? "\\" : taskPath;

        // Play alert sound
        System.Media.SystemSounds.Exclamation.Play();
    }

    private void Ignore_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Block_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Execute Disable-ScheduledTask via PowerShell
            // Need -TaskPath to be specific to avoid ambiguity
            string pathArg = string.IsNullOrEmpty(_taskPath) ? "\\" : _taskPath;
            string psCommand = $"Disable-ScheduledTask -TaskName \"{_taskName}\" -TaskPath \"{pathArg}\" -ErrorAction Stop";
            
            var info = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            var proc = Process.Start(info);
            proc?.WaitForExit();

            if (proc != null && proc.ExitCode == 0)
            {
                 MessageBox.Show($"La tarea '{_taskName}' ha sido deshabilitada correctamente.", "Tarea Bloqueada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                string err = proc?.StandardError.ReadToEnd() ?? "Unknown error";
                MessageBox.Show($"No se pudo deshabilitar la tarea.\nError: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al intentar bloquear: {ex.Message}", "Error");
        }
        
        Close();
    }
}
