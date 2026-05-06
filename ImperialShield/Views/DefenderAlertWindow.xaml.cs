using System;
using System.Diagnostics;
using System.Windows;
using ImperialShield.Services;

namespace ImperialShield.Views;

public partial class DefenderAlertWindow : Window
{
    private readonly bool _isDemoMode;

    public DefenderAlertWindow(string message, bool demoMode = false)
    {
        InitializeComponent();
        _isDemoMode = demoMode;
        MessageText.Text = message;
        EnableDefenderButton.IsEnabled = !demoMode;
        
        System.Media.SystemSounds.Hand.Play();
    }

    private void EnableDefender_Click(object sender, RoutedEventArgs e)
    {
        if (_isDemoMode)
            return;

        try
        {
            // Intenta abrir la configuración de seguridad de Windows
            // El usuario puede reactivar Defender desde ahí
            Process.Start(new ProcessStartInfo
            {
                FileName = "windowsdefender://threat",
                UseShellExecute = true
            });
            
            // También intentamos abrir directamente la configuración de protección
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:windowsdefender",
                UseShellExecute = true
            });
            
            Logger.Log("User requested to enable Defender - opened Windows Security");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "EnableDefender_Click");
            
            // Fallback: abrir el centro de seguridad clásico
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "wscui.cpl",
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show(
                    "No se pudo abrir la configuración de Windows Defender.\n\n" +
                    "Abre manualmente: Configuración > Privacidad y seguridad > Seguridad de Windows",
                    "Imperial Shield",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        
        this.Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    public static void Show(string message, bool demoMode = false)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var alert = new DefenderAlertWindow(message, demoMode);
            alert.Topmost = true;
            alert.ShowDialog();
        });
    }
}
