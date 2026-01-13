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

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
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
