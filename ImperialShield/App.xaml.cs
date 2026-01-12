using System.Diagnostics;
using System.IO;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using ImperialShield.Services;
using ImperialShield.Views;

namespace ImperialShield;

/// <summary>
/// Imperial Shield - Sistema de Monitoreo de Seguridad para Windows
/// Punto de entrada principal de la aplicación
/// </summary>
public partial class App : Application
{
    private TaskbarIcon? _notifyIcon;
    private HostsFileMonitor? _hostsMonitor;
    private DefenderMonitor? _defenderMonitor;
    private DashboardWindow? _dashboardWindow;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Verificar si ya hay una instancia corriendo
        if (!SingleInstanceManager.TryAcquireLock())
        {
            MessageBox.Show("Imperial Shield ya está en ejecución.", "Imperial Shield",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        InitializeApplication(e.Args);
    }

    private void InitializeApplication(string[] args)
    {
        // Configurar el icono del systray
        _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
        _notifyIcon.TrayMouseDoubleClick += (s, e) => ShowDashboard();

        // Inicializar los monitores de seguridad
        _hostsMonitor = new HostsFileMonitor();
        _hostsMonitor.HostsFileChanged += OnHostsFileChanged;
        _hostsMonitor.Start();

        _defenderMonitor = new DefenderMonitor();
        _defenderMonitor.DefenderStatusChanged += OnDefenderStatusChanged;
        _defenderMonitor.ExclusionAdded += OnExclusionAdded;
        _defenderMonitor.Start();

        // Verificar si se debe iniciar en modo silencioso
        bool startSilent = args.Contains("--silent") || args.Contains("-s");

        if (!startSilent)
        {
            ShowDashboard();
        }

        // Mostrar notificación de inicio
        ShowToastNotification("Imperial Shield Activo",
            "El sistema de seguridad está monitoreando tu equipo.",
            ToastNotificationType.Info);
    }

    #region Event Handlers

    private void OnHostsFileChanged(object? sender, HostsFileChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            ShowToastNotification("⚠️ Alerta de Seguridad",
                $"El archivo HOSTS ha sido modificado.\n{e.ChangeDescription}",
                ToastNotificationType.Warning);
        });
    }

    private void OnDefenderStatusChanged(object? sender, DefenderStatusEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var type = e.IsEnabled ? ToastNotificationType.Success : ToastNotificationType.Danger;
            var message = e.IsEnabled
                ? "Windows Defender está activo y protegiendo tu sistema."
                : "⚠️ Windows Defender ha sido DESACTIVADO. Tu sistema está en riesgo.";

            ShowToastNotification("Estado de Windows Defender", message, type);
        });
    }

    private void OnExclusionAdded(object? sender, ExclusionAddedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            ShowToastNotification("🚨 Nueva Exclusión Detectada",
                $"Se ha añadido una nueva exclusión al antivirus:\n{e.ExclusionPath}\n\n¿Autorizas este cambio?",
                ToastNotificationType.Danger);
        });
    }

    #endregion

    #region Menu Click Handlers

    private void ShowDashboard_Click(object sender, RoutedEventArgs e) => ShowDashboard();
    
    private void ShowProcessViewer_Click(object sender, RoutedEventArgs e)
    {
        var window = new ProcessViewerWindow();
        window.Show();
    }

    private void ShowNetworkViewer_Click(object sender, RoutedEventArgs e)
    {
        var window = new NetworkViewerWindow();
        window.Show();
    }

    private void ViewHosts_Click(object sender, RoutedEventArgs e)
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

    private void ViewDefender_Click(object sender, RoutedEventArgs e)
    {
        if (_defenderMonitor == null) return;

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
                         ? string.Join("\n", exclusions.Take(10).Select(ex => $"• {ex}"))
                         : "No hay exclusiones configuradas");

        if (exclusions.Count > 10)
            message += $"\n... y {exclusions.Count - 10} más";

        MessageBox.Show(message, "Estado de Windows Defender",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var startupEnabled = StartupManager.IsStartupEnabled();
        
        var result = MessageBox.Show(
            $"=== Configuración de Imperial Shield ===\n\n" +
            $"Inicio con Windows: {(startupEnabled ? "Activado" : "Desactivado")}\n\n" +
            $"¿Deseas {(startupEnabled ? "desactivar" : "activar")} el inicio automático con Windows?",
            "Configuración",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            if (StartupManager.ToggleStartup())
            {
                var newState = StartupManager.IsStartupEnabled();
                MessageBox.Show(
                    $"Inicio automático {(newState ? "activado" : "desactivado")} correctamente.",
                    "Configuración", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Error al modificar la configuración de inicio.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
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
            Shutdown();
        }
    }

    #endregion

    #region Helper Methods

    private void ShowDashboard()
    {
        if (_dashboardWindow == null || !_dashboardWindow.IsLoaded)
        {
            _dashboardWindow = new DashboardWindow();
        }

        _dashboardWindow.Show();
        _dashboardWindow.WindowState = WindowState.Normal;
        _dashboardWindow.Activate();
    }

    private void ShowToastNotification(string title, string message, ToastNotificationType type)
    {
        try
        {
            if (_notifyIcon != null)
            {
                var icon = type switch
                {
                    ToastNotificationType.Danger => BalloonIcon.Error,
                    ToastNotificationType.Warning => BalloonIcon.Warning,
                    ToastNotificationType.Success => BalloonIcon.Info,
                    _ => BalloonIcon.Info
                };

                _notifyIcon.ShowBalloonTip(title, message, icon);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error al mostrar notificación: {ex.Message}");
        }
    }

    #endregion

    protected override void OnExit(ExitEventArgs e)
    {
        _hostsMonitor?.Stop();
        _hostsMonitor?.Dispose();
        _defenderMonitor?.Stop();
        _defenderMonitor?.Dispose();
        _notifyIcon?.Dispose();
        SingleInstanceManager.ReleaseLock();

        base.OnExit(e);
    }
}

public enum ToastNotificationType
{
    Info,
    Success,
    Warning,
    Danger
}
