using System;
using System.Diagnostics;
using System.Windows;
using ImperialShield.Services;

namespace ImperialShield.Views;

public partial class DDoSTrackerWindow : Window
{
    private readonly string _processName;

    public DDoSTrackerWindow(string processName, string targetIp, int count, string message)
    {
        InitializeComponent();
        _processName = processName;

        ProcessNameText.Text = processName;
        TargetIpText.Text = targetIp;
        ConnectionCountText.Text = count.ToString();

        // Sonido de alarma nuclear/cienca ficción
        System.Media.SystemSounds.Exclamation.Play(); 
    }

    private void Kill_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var processes = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(_processName));
            foreach (var p in processes)
            {
                p.Kill();
            }
            
            MessageBox.Show($"Se han detenido los procesos de {_processName}.", "Amenaza Neutralizada", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo detener el proceso: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void KillAndBlock_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? exePath = null;
            
            // First, try to get the executable path from the running process
            var processes = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(_processName));
            foreach (var p in processes)
            {
                try
                {
                    exePath = p.MainModule?.FileName;
                    p.Kill();
                }
                catch { }
            }
            
            // Quarantine the executable
            string exeToBlock = !string.IsNullOrEmpty(exePath) ? exePath : _processName;
            string exeName = System.IO.Path.GetFileName(exeToBlock);
            
            if (QuarantineService.QuarantineExecutable(exeName))
            {
                Logger.Log($"DDoS Attacker quarantined: {exeName}");
                MessageBox.Show(
                    $"✅ Proceso terminado y bloqueado.\n\n" +
                    $"El ejecutable '{exeName}' ha sido puesto en cuarentena.\n" +
                    $"No podrá ejecutarse nuevamente hasta que lo liberes desde el Gestor de Cuarentena.",
                    "Amenaza Neutralizada y Bloqueada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"⚠️ Proceso terminado, pero no se pudo bloquear.\n\n" +
                    $"Imperial Shield necesita permisos de Administrador para poner ejecutables en cuarentena.",
                    "Bloqueo Fallido",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            
            this.Close();
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "KillAndBlock_Click");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Try to find the process to get the path if it's running
            var procs = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(_processName));
            if (procs.Length > 0)
            {
                var path = procs[0].MainModule?.FileName;
                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                    return;
                }
            }

            // Fallback: search in common paths or just show message
            // Since we only have the name in this context, we can't do much if process is gone.
            // But we can try to search if it looks like a full path (simulations pass full path sometimes or just name)
            if (System.IO.File.Exists(_processName))
            {
                Process.Start("explorer.exe", $"/select,\"{_processName}\"");
            }
        }
        catch { }
    }

    public static void ShowAlert(string processName, string ip, int count, string warning)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new DDoSTrackerWindow(processName, ip, count, warning);
            win.Show();
        });
    }
}
