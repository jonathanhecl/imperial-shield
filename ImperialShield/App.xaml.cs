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
            // No es fatal, continuar
        }

        // Paso 4: Cerrar splash y mostrar systray
        _splashWindow?.UpdateStatus("¡Listo!");
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
        Dispatcher.Invoke(() =>
        {
            ShowToastNotification("⚠️ Alerta de Seguridad",
                $"El archivo HOSTS ha sido modificado.\n{e.ChangeDescription}",
                ToastNotificationType.Warning);
        });
    }

    private void OnDefenderStatusChanged(object? sender, DefenderStatusEventArgs e)
    {
        Logger.Log($"Defender status changed: {e.IsEnabled}");
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
        Logger.Log($"New Defender exclusion detected: {e.ExclusionPath}");
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
            Logger.LogException(ex, "ViewHosts");
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
        var logPath = Logger.GetLogDirectory();
        
        var result = MessageBox.Show(
            $"=== Configuración de Imperial Shield ===\n\n" +
            $"Inicio con Windows: {(startupEnabled ? "Activado" : "Desactivado")}\n\n" +
            $"Logs: {logPath}\n\n" +
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
        Logger.Log("Exit requested by user");
        
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
            Logger.Log("User confirmed exit");
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
            Logger.LogException(ex, "ShowToastNotification");
        }
    }

    #endregion

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Log("Application exiting...");
        
        _hostsMonitor?.Stop();
        _hostsMonitor?.Dispose();
        _defenderMonitor?.Stop();
        _defenderMonitor?.Dispose();
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
