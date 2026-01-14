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
    private IFEOMonitor? _ifeoMonitor;
    private DDoSMonitor? _ddosMonitor;
    private TasksMonitor? _tasksMonitor;
    private bool _isMonitoringPaused;

    public bool IsMonitoringPaused => _isMonitoringPaused;
    private DashboardWindow? _dashboardWindow;
    private SplashWindow? _splashWindow;

    public static App CurrentApp => (App)Application.Current;
    public HostsFileMonitor? HostsMonitor => _hostsMonitor;
    public DefenderMonitor? DefenderMonitor => _defenderMonitor;
    public PrivacyMonitor? PrivacyMonitor => _privacyMonitor;
    public StartupMonitor? StartupMonitor => _startupMonitor;
    public TasksMonitor? TasksMonitor => _tasksMonitor;
    public IFEOMonitor? IFEOMonitor => _ifeoMonitor;
    public DDoSMonitor? DDoSMonitor => _ddosMonitor;

    public App()
    {
        // Configurar manejadores de excepciones globales
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Asegurar modo de cierre explícito para evitar cierres prematuros
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        
        Logger.Log("=== Imperial Shield Starting ===");
        Logger.Log($"Arguments: {string.Join(" ", e.Args)}");
        
        try
        {
            Logger.Log("Checking single instance lock...");
            // Verificar si ya hay una instancia corriendo
            if (!SingleInstanceManager.TryAcquireLock())
            {
                Logger.Log("Another instance is already running");
                MessageBox.Show("Imperial Shield ya está en ejecución.", "Imperial Shield",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            Logger.Log("Showing splash screen...");
            // Mostrar splash screen
            _splashWindow = new SplashWindow();
            _splashWindow.Show();
            _splashWindow.UpdateStatus("Inicializando...");

            Logger.Log("Queuing initialization...");
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
            // args[1] holds the full path of the blocked executable passed by IFEO
            string? blockedExePath = args.Length >= 2 && args[0] == "--blocked" ? args[1] : null;
            if (!string.IsNullOrEmpty(blockedExePath))
            {
                _splashWindow?.Close();
                BlockedExecutionWindow.ShowBlocked(blockedExePath);
                Shutdown();
                return;
            }
        }
        // Paso 1: Configurar el icono del systray
        _splashWindow?.UpdateStatus("Configurando systray...");
        try
        {
            Logger.Log("Initializing TaskbarIcon...");
            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
            if (_notifyIcon == null) Logger.Log("WARNING: NotifyIcon resource not found!");
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
            Logger.Log("Starting DefenderMonitor...");
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

        // Paso 5: Inicializar monitor de Tareas Programadas
        _splashWindow?.UpdateStatus("Monitoreando tareas programadas...");
        try 
        {
            _tasksMonitor = new TasksMonitor();
            _tasksMonitor.NewTaskDetected += OnNewTaskDetected;
            _tasksMonitor.Start();
            Logger.Log("TasksMonitor started");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "TasksMonitor Setup");
        }

        // Paso 6: Inicializar monitor de IFEO (Redirecciones)
        _splashWindow?.UpdateStatus("Iniciando monitor de Redirecciones...");
        try
        {
            _ifeoMonitor = new IFEOMonitor();
            _ifeoMonitor.RogueIFEODetected += OnRogueIFEODetected;
            _ifeoMonitor.Start();
            Logger.Log("IFEOMonitor started");
        }
        // Brace removed here
        catch (Exception ex)
        {
            Logger.LogException(ex, "IFEOMonitor Setup");
        }

        // Paso 7: Inicializar monitor DDoS (Watchdog)
         _splashWindow?.UpdateStatus("Activando DDoS Watchdog...");
        try
        {
            _ddosMonitor = new DDoSMonitor();
            _ddosMonitor.DDoSAttackDetected += OnDDoSDetected;
            _ddosMonitor.Start();
            Logger.Log("DDoSMonitor started");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "DDoSMonitor Setup");
        }

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
        Views.HostsAlertWindow.Show(
            $"Se han detectado cambios en el archivo HOSTS del sistema.\n\n" +
            $"{e.ChangeDescription}\n\n" +
            "El archivo HOSTS puede ser usado por malware para redirigir sitios web legítimos a páginas falsas.");
    }

    private void OnDefenderStatusChanged(object? sender, DefenderStatusEventArgs e)
    {
        Logger.Log($"Defender status changed: {(e.IsEnabled ? "Enabled" : "DISABLED")}");
        
        if (!e.IsEnabled)
        {
            Views.DefenderAlertWindow.Show(
                "La Protección en Tiempo Real de Windows Defender ha sido DESACTIVADA.\n\n" +
                "Tu equipo se encuentra vulnerable a virus, malware y ransomware.\n\n" +
                "Se recomienda encarecidamente reactivar la protección inmediatamente.");
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

            if (alert.Result == PrivacyAlertResult.RevokeAndKill)
            {
                _privacyMonitor?.RevokePermission(e.App.ApplicationPath, e.App.IsNonPackaged, e.App.Device);
                
                string deviceName = e.App.Device == DeviceType.Camera ? "cámara" : "micrófono";
                ShowToastNotification("Permiso Revocado y App Terminada", 
                    $"Se bloqueó el acceso a {deviceName} para '{e.App.ApplicationName}' y se terminó el proceso.",
                    ToastNotificationType.Success);
            }
            else if (alert.Result == PrivacyAlertResult.KillOnly)
            {
                ShowToastNotification("Aplicación Terminada", 
                    $"Se terminó '{e.App.ApplicationName}'. El permiso de acceso no fue revocado.",
                    ToastNotificationType.Info);
            }
        });
    }

    private void OnRogueIFEODetected(object? sender, IFEORiskEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var result = SecurityWarningWindow.ShowWarning(e.ExecutableName, e.DebuggerPath);

            if (result == SecurityAction.Delete)
            {
                if (QuarantineService.UnquarantineExecutable(e.ExecutableName))
                {
                    ShowToastNotification("Amenaza Eliminada", 
                        $"La redirección maliciosa de '{e.ExecutableName}' ha sido eliminada.",
                        ToastNotificationType.Success);
                }
                else
                {
                    MessageBox.Show("No se pudo eliminar la entrada del registro. Verifica los permisos de Administrador.", "Error");
                }
            }
        });
        // Duplicate removed here
    }

    private void OnDDoSDetected(object? sender, DDoSEventArgs e)
    {
        DDoSTrackerWindow.ShowAlert(e.ProcessName, e.RemoteIP, e.ConnectionCount, e.WarningMessage);
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

    private void ShowScheduledTasks_Click(object sender, RoutedEventArgs e)
    {
        var window = new ScheduledTasksWindow();
        window.Show();
    }

    private void ShowPrivacy_Click(object sender, RoutedEventArgs e)
    {
        var window = new PrivacyManagerWindow();
        window.Show();
    }

    private void ShowQuarantine_Click(object sender, RoutedEventArgs e)
    {
        var window = new QuarantineWindow();
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

    private void OnNewTaskDetected(object? sender, NewTaskEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var alert = new NewTaskAlertWindow(e.TaskName, e.TaskPath);
            alert.Show();
        });
    }

    #endregion

    #region Pause/Resume Logic

    public void PauseMonitoring()
    {
        if (_isMonitoringPaused) return;

        Logger.Log("Pausing all monitoring services...");
        _isMonitoringPaused = true;

        _hostsMonitor?.Stop();
        _defenderMonitor?.Stop();
        _privacyMonitor?.Stop();
        _startupMonitor?.Stop();
        _tasksMonitor?.Stop();
        _ifeoMonitor?.Stop();
        _ddosMonitor?.Stop();

        ShowToastNotification("Protección Pausada", 
            "Todos los monitores de seguridad han sido detenidos.", 
            ToastNotificationType.Warning);
    }

    public void ResumeMonitoring()
    {
        if (!_isMonitoringPaused) return;

        Logger.Log("Resuming all monitoring services...");
        _isMonitoringPaused = false;

        _hostsMonitor?.Start();
        _defenderMonitor?.Start();
        _privacyMonitor?.Start();
        _startupMonitor?.Start();
        _tasksMonitor?.Start();
        _ifeoMonitor?.Start();
        _ddosMonitor?.Start();

        ShowToastNotification("Protección Reactivada", 
            "Imperial Shield está protegiendo tu sistema nuevamente.", 
            ToastNotificationType.Success);
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
        _tasksMonitor?.Stop();
        _tasksMonitor?.Dispose();
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
