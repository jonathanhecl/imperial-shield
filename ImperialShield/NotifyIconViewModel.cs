using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Hardcodet.NotifyIcon.Wpf;
using ImperialShield.Services;
using ImperialShield.Views;

namespace ImperialShield;

/// <summary>
/// Comandos globales para el systray
/// </summary>
public static class Commands
{
    public static readonly RoutedCommand ShowDashboard = new();
}

/// <summary>
/// Handler de eventos del icono del systray
/// </summary>
public class NotifyIconViewModel
{
    private DashboardWindow? _dashboardWindow;
    private readonly DefenderMonitor _defenderMonitor = new();

    public void ShowDashboard_Click(object sender, RoutedEventArgs e)
    {
        ShowDashboard();
    }

    public void ShowDashboard()
    {
        if (_dashboardWindow == null || !_dashboardWindow.IsLoaded)
        {
            _dashboardWindow = new DashboardWindow();
        }
        
        _dashboardWindow.Show();
        _dashboardWindow.WindowState = WindowState.Normal;
        _dashboardWindow.Activate();
    }

    public void ShowProcessViewer_Click(object sender, RoutedEventArgs e)
    {
        var window = new ProcessViewerWindow();
        window.Show();
    }

    public void ShowNetworkViewer_Click(object sender, RoutedEventArgs e)
    {
        var window = new NetworkViewerWindow();
        window.Show();
    }

    public void ViewHosts_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var hostsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"drivers\etc\hosts");

            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = hostsPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al abrir el archivo HOSTS: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void ViewDefender_Click(object sender, RoutedEventArgs e)
    {
        var info = _defenderMonitor.GetDefenderInfo();
        var exclusions = _defenderMonitor.GetCurrentExclusions();

        var message = $"=== Estado de Windows Defender ===\n\n" +
                     $"Protección en Tiempo Real: {(info.RealTimeProtectionEnabled ? "✅ Activo" : "❌ Inactivo")}\n" +
                     $"Monitor de Comportamiento: {(info.BehaviorMonitorEnabled ? "✅ Activo" : "❌ Inactivo")}\n" +
                     $"Versión de Firmas: {info.SignatureVersion}\n" +
                     $"Antigüedad de Firmas: {info.SignatureAgeDays} día(s)\n" +
                     $"Último Escaneo: {info.LastFullScan?.ToString("dd/MM/yyyy HH:mm") ?? "Nunca"}\n\n" +
                     $"=== Exclusiones ({exclusions.Count}) ===\n" +
                     (exclusions.Count > 0 
                         ? string.Join("\n", exclusions.Take(10).Select(e => $"• {e}"))
                         : "No hay exclusiones configuradas");

        if (exclusions.Count > 10)
            message += $"\n... y {exclusions.Count - 10} más";

        MessageBox.Show(message, "Estado de Windows Defender",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void Settings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Configuración próximamente disponible.\n\n" +
                       "Opciones planificadas:\n" +
                       "• Iniciar con Windows\n" +
                       "• Intervalo de escaneo\n" +
                       "• Notificaciones\n" +
                       "• Whitelist de exclusiones",
            "Configuración", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void Exit_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "¿Estás seguro de que deseas cerrar Imperial Shield?\n\n" +
            "El sistema dejará de monitorear cambios en:\n" +
            "• Archivo HOSTS\n" +
            "• Exclusiones de Windows Defender\n" +
            "• Estado del antivirus",
            "Confirmar Salida",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            Application.Current.Shutdown();
        }
    }
}
