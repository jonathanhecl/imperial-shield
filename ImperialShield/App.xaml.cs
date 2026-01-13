using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
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
    private PrivacyMonitor? _privacyMonitor;
    private StartupMonitor? _startupMonitor;
    private DashboardWindow? _dashboardWindow;
    private SplashWindow? _splashWindow;

    public App()
    {
        // Configurar manejadores de excepciones globales
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        Logger.Log("=== Imperial Shield Starting ===");
        
        try
        {
            // Verificar si ya hay una instancia corriendo
            if (!SingleInstanceManager.TryAcquireLock())
            {
                Logger.Log("Another instance is already running");
                MessageBox.Show("Imperial Shield ya está en ejecución.", "Imperial Shield",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Mostrar splash screen
            _splashWindow = new SplashWindow();
            _splashWindow.Show();
            _splashWindow.UpdateStatus("Inicializando...");

            // Inicializar en background para no bloquear la splash
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    InitializeApplication(e.Args);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "InitializeApplication");
                    HandleFatalError(ex);
                }
            }), DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            Logger.LogCrash(ex);
            HandleFatalError(ex);
        }
    }

    private void InitializeApplication(string[] args)
    {
        Logger.Log("Initializing application...");
        
        // Check if we were invoked by IFEO (quarantine interception)
        if (QuarantineService.HandleQuarantineInterception(args))
        {
            string? blockedExe = QuarantineService.GetBlockedExecutableName(args);
            if (!string.IsNullOrEmpty(blockedExe))
            {
                _splashWindow?.Close();
                BlockedExecutionWindow.ShowBlocked(blockedExe);
                Shutdown();
                return;
            }
        }
        // Paso 1: Configurar el icono del systray
        _splashWindow?.UpdateStatus("Configurando systray...");
        try
        {
            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
            _notifyIcon.TrayMouseDoubleClick += (s, e) => ShowDashboard();
            
            // Cargar icono programáticamente
            try
            {
                var iconUri = new Uri("pack://application:,,,/Resources/shield.ico", UriKind.Absolute);
                _notifyIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(iconUri);
                Logger.Log("Icon loaded from resources");
            }
            catch (Exception iconEx)
            {
                Logger.LogException(iconEx, "Icon loading - using default");
                // Sin icono, pero la app sigue funcionando
            }
            
            Logger.Log("TaskbarIcon configured successfully");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "TaskbarIcon setup");
            throw;
        }

        // Paso 2: Inicializar monitor de HOSTS
        _splashWindow?.UpdateStatus("Iniciando monitor de HOSTS...");
        try
        {
            _hostsMonitor = new HostsFileMonitor();
            _hostsMonitor.HostsFileChanged += OnHostsFileChanged;
            _hostsMonitor.Start();
            Logger.Log("HostsFileMonitor started");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "HostsFileMonitor");
            // No es fatal, continuar
        }

        // Paso 3: Inicializar monitor de Defender
        _splashWindow?.UpdateStatus("Iniciando monitor de Defender...");
        try
        {
            _defenderMonitor = new DefenderMonitor();
            _defenderMonitor.DefenderStatusChanged += OnDefenderStatusChanged;
            _defenderMonitor.ExclusionAdded += OnExclusionAdded;
            _defenderMonitor.Start();
            Logger.Log("DefenderMonitor started");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "DefenderMonitor");
            Logger.LogException(ex, "DefenderMonitor");
            // No es fatal, continuar
        }

        // Paso 3.5: Inicializar monitor de Privacidad
        _splashWindow?.UpdateStatus("Iniciando monitor de Privacidad...");
        try
        {
            _privacyMonitor = new PrivacyMonitor();
            _privacyMonitor.PrivacyRiskDetected += OnPrivacyRiskDetected;
            _privacyMonitor.SafeStateRestored += OnSafeStateRestored;
            _privacyMonitor.NewPrivacyAppDetected += OnNewPrivacyAppDetected;
            _privacyMonitor.Start();
            Logger.Log("PrivacyMonitor started");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "PrivacyMonitor");
        }

        // Paso 4: Cerrar splash y mostrar systray
        _splashWindow?.UpdateStatus("Monitoreando inicio automático...");
        _startupMonitor = new StartupMonitor();
        _startupMonitor.NewStartupAppDetected += OnNewStartupAppDetected;
        _startupMonitor.Start();

        _splashWindow?.UpdateStatus("Listo.");
        Logger.Log("Initialization complete");

        // Esperar un momento para que se vea el mensaje "Listo!"
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            _splashWindow?.Close();
            _splashWindow = null;

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
        };
        timer.Start();
    }

    private void HandleFatalError(Exception ex)
    {
        Logger.LogCrash(ex);
        
        var logPath = Logger.GetLogDirectory();
        MessageBox.Show(
            $"Error fatal al iniciar Imperial Shield:\n\n{ex.Message}\n\n" +
            $"Los logs se encuentran en:\n{logPath}",
            "Error Fatal",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        
        Shutdown(1);
    }

    #region Exception Handlers

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.LogCrash(e.Exception);
        e.Handled = true;
        
        MessageBox.Show(
            $"Error inesperado:\n{e.Exception.Message}\n\nLa aplicación continuará funcionando.",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.LogCrash(ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.LogException(e.Exception, "UnobservedTaskException");
        e.SetObserved();
    }

    #endregion

    #region Event Handlers

    private void OnHostsFileChanged(object? sender, HostsFileChangedEventArgs e)
    {
        Logger.Log("HOSTS file changed detected");
        Views.AlertWindow.Show(
            "¡ATENCIÓN! Se han detectado cambios en el archivo HOSTS del sistema.\n\n" +
            $"{e.ChangeDescription}\n\n" +
            "¿Autorizas estos cambios?");
    }

    private void OnDefenderStatusChanged(object? sender, DefenderStatusEventArgs e)
    {
        Logger.Log($"Defender status changed: {(e.IsEnabled ? "Enabled" : "DISABLED")}");
        
        if (!e.IsEnabled)
        {
            Views.AlertWindow.Show(
                "¡ATENCIÓN! La Protección en Tiempo Real de Windows Defender ha sido DESACTIVADA.\n\n" +
                "Su equipo se encuentra vulnerable a amenazas externas.");
        }
    }

    private void OnExclusionAdded(object? sender, ExclusionAddedEventArgs e)
    {
        Logger.Log($"New exclusion detected: {e.ExclusionPath}");
        
        Views.AlertWindow.Show(
            "Se ha detectado una NUEVA EXCLUSIÓN en Windows Defender:\n\n" +
            $"• {e.ExclusionPath}\n\n" +
            "Esto podría permitir que archivos maliciosos se ejecuten sin ser detectados.");
    }

    private void OnPrivacyRiskDetected(object? sender, PrivacyRiskEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            Logger.Log("Privacy Risk Detected!");
            var apps = string.Join(", ", e.Risks.Select(r => r.ApplicationName).Distinct());
            ShowToastNotification("¡ALERTA DE ESPIONAJE!",
                $"Imperial Shield detectó acceso de hardware sin autorizar:\n{apps}\n¿Bloquear?",
                ToastNotificationType.Danger);
        });
    }

    private void OnSafeStateRestored(object? sender, EventArgs e)
    {
        Logger.Log("Privacy Safe State Restored");
    }

    private void OnNewPrivacyAppDetected(object? sender, NewPrivacyAppEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            Logger.Log($"NEW PRIVACY APP: {e.App.ApplicationName} accessed {e.App.Device}");
            
            var alert = new PrivacyAlertWindow(e.App);
            alert.ShowDialog();

            if (alert.Result == PrivacyAlertResult.Revoke)
            {
                _privacyMonitor?.RevokePermission(e.App.ApplicationPath, e.App.IsNonPackaged, e.App.Device);
                
                string deviceName = e.App.Device == DeviceType.Camera ? "cámara" : "micrófono";
                ShowToastNotification("Permiso Revocado", 
                    $"Se bloqueó el acceso a {deviceName} para '{e.App.ApplicationName}'.",
                    ToastNotificationType.Success);
            }
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

    private void ShowStartupManager_Click(object sender, RoutedEventArgs e)
    {
        var window = new StartupManagerWindow();
        window.Show();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        // Abrir ventana de configuración premium
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var settingsWin = new Views.SettingsWindow();
            settingsWin.ShowDialog();

            if (settingsWin.SettingsChanged)
            {
                Logger.Log("Settings changed, restarting monitors with new interval...");
                
                // Reiniciar DefenderMonitor con el nuevo tiempo
                _defenderMonitor?.Stop();
                _defenderMonitor?.Start();

                // Reiniciar HostsMonitor con el nuevo tiempo
                _hostsMonitor?.Stop();
                _hostsMonitor?.Start();

                // Reiniciar StartupMonitor con el nuevo tiempo
                _startupMonitor?.Stop();
                _startupMonitor?.Start();
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Logger.Log("Exit requested by user");
        
        // Usar ventana personalizada en lugar de MessageBox que desaparece
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var confirmWin = new Views.ConfirmExitWindow();
                confirmWin.ShowDialog();

                if (confirmWin.Confirmed)
                {
                    Logger.Log("User confirmed exit through custom dialog");
                    Shutdown();
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Custom exit dialog");
                // Fallback de emergencia si la ventana falla
                if (MessageBox.Show("¿Deseas cerrar Imperial Shield?", "Salir", 
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    Shutdown();
                }
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
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
            Logger.LogException(ex, "ShowToastNotification");
        }
    }

    private void OnNewStartupAppDetected(object? sender, string app)
    {
        Logger.Log($"NEW STARTUP APP: {app}");
        Views.AlertWindow.Show(
            "¡ADVERTENCIA DE SEGURIDAD!\n\n" +
            "Se ha detectado que una nueva aplicación intenta iniciarse automáticamente con Windows:\n\n" +
            $"• {app}\n\n" +
            "Muchos virus y troyanos usan este método para persistir en el sistema. ¿Reconoces esta aplicación?");
    }

    #endregion

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Log("Application exiting...");
        
        _hostsMonitor?.Stop();
        _hostsMonitor?.Dispose();
        _defenderMonitor?.Stop();
        _defenderMonitor?.Dispose();
        _privacyMonitor?.Stop();
        _privacyMonitor?.Dispose();
        _startupMonitor?.Stop();
        _notifyIcon?.Dispose();
        SingleInstanceManager.ReleaseLock();

        Logger.Log("=== Imperial Shield Stopped ===");
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
