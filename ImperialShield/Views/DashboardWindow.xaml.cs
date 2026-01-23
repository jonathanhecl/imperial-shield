using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using ImperialShield.Services;

namespace ImperialShield.Views;

/// <summary>
/// Dashboard principal de Imperial Shield
/// </summary>
public partial class DashboardWindow : Window
{
    private readonly DefenderMonitor _defenderMonitor;
    private readonly NetworkMonitor _networkMonitor;
    private Timer? _refreshTimer;

    public DashboardWindow()
    {
        try
        {
            InitializeComponent();
            _defenderMonitor = new DefenderMonitor();
            _networkMonitor = new NetworkMonitor();
            
            // Establecer versión
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Imperial Shield v{version?.Major}.{version?.Minor}.{version?.Build}";

            Loaded += async (s, e) => 
            {
                try
                {
                    // Forzar carga inicial inmediata de todos los monitores
                    await ForceInitialDataLoad();
                    await RefreshDashboardAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "DashboardWindow_Loaded");
                }
            };
            
            // Refrescar cada 30 segundos
            _refreshTimer = new Timer(async _ => 
            {
                try
                {
                    await Dispatcher.InvokeAsync(async () => await RefreshDashboardAsync());
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "DashboardWindow_RefreshTimer");
                }
            }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            Logger.LogCrash(ex);
            MessageBox.Show($"Error al inicializar el Dashboard: {ex.Message}", "Error de Dashboard", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Fuerza una actualización inmediata del Dashboard
    /// </summary>
    public async Task ForceRefreshAsync()
    {
        await RefreshDashboardAsync();
    }

    /// <summary>
    /// Fuerza la carga inicial inmediata de todos los monitores
    /// </summary>
    private async Task ForceInitialDataLoad()
    {
        await Task.Run(() =>
        {
            try
            {
                var app = App.CurrentApp;
                
                // Forzar carga inicial inmediata de todos los monitores
                app.HostsMonitor?.ForceInitialLoad();
                app.DefenderMonitor?.ForceInitialLoad();
                app.StartupMonitor?.ForceInitialLoad();
                app.TasksMonitor?.ForceInitialLoad();
                app.BrowserMonitor?.ForceInitialLoad();
                app.PrivacyMonitor?.ForceInitialLoad();
                
                Logger.Log("Initial data load forced for all monitors");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "ForceInitialDataLoad");
            }
        });
    }

    private async Task RefreshDashboardAsync()
    {
        await Task.Run(() =>
        {
            var defenderInfo = _defenderMonitor.GetDefenderInfo();
            // Get references to app monitors for extra data
            var app = App.CurrentApp;
            
            // Collect data for Last Checks list
            var defenseTime = app.DefenderMonitor?.LastChecked ?? DateTime.MinValue;
            var hostsTime = app.HostsMonitor?.LastChecked ?? DateTime.MinValue;
            var hostsCount = app.HostsMonitor?.EntryCount ?? 0;
            var exclusionsTime = app.DefenderMonitor?.LastChecked ?? DateTime.MinValue; // Defender monitor handles exclusions too
            var exclusionsCount = app.DefenderMonitor?.GetCurrentExclusions().Count ?? 0;
            var netTime = DateTime.Now; // Network is real-time, but we can use current time
            var startupTime = app.StartupMonitor?.LastChecked ?? DateTime.MinValue;
            var startupCount = app.StartupMonitor?.ItemCount ?? 0;
            var tasksTime = app.TasksMonitor?.LastChecked ?? DateTime.MinValue;
            var tasksCount = app.TasksMonitor?.ItemCount ?? 0;
            var browserTime = app.BrowserMonitor?.LastChecked ?? DateTime.MinValue;
            var browserName = app.BrowserMonitor?.CurrentBrowserName ?? "...";
            var privacyTime = app.PrivacyMonitor?.LastChecked ?? DateTime.MinValue;
            var privacyCount = app.PrivacyMonitor?.ActiveRiskCount ?? 0;

            var connections = _networkMonitor.GetTcpConnections();
            var suspiciousConnections = connections.Where(c => c.ThreatLevel >= ConnectionThreatLevel.High).ToList();

            Dispatcher.Invoke(() =>
            {
                UpdateOverallStatus(defenderInfo, suspiciousConnections.Count);
                
                // Update Last Checks List
                UpdateCheckItem(CheckDefenseTime, CheckDefenseStatus, defenseTime, 0, "active", false, CheckDefenseDot, CheckDefenseBadge);
                UpdateCheckItem(CheckHostsTime, CheckHostsCount, hostsTime, hostsCount, "hosts", false, CheckHostsDot, CheckHostsBadge); 
                UpdateCheckItem(CheckExclusionsTime, CheckExclusionsCount, exclusionsTime, exclusionsCount, "active", false, CheckExclusionsDot, CheckExclusionsBadge);
                UpdateCheckItem(CheckConnectionsTime, CheckConnectionsCount, netTime, suspiciousConnections.Count, "ddos", false, CheckConnectionsDot, CheckConnectionsBadge);
                UpdateCheckItem(CheckStartupTime, CheckStartupCount, startupTime, startupCount, "active", false, CheckStartupDot, CheckStartupBadge);
                UpdateCheckItem(CheckBrowserTime, CheckBrowserStatus, browserTime, 0, "active", false, CheckBrowserDot, CheckBrowserBadge);
                UpdateCheckItem(CheckTasksTime, CheckTasksCount, tasksTime, tasksCount, "active", false, CheckTasksDot, CheckTasksBadge);
                UpdateCheckItem(CheckWshTime, CheckWshStatus, DateTime.Now, QuarantineService.IsVBSEnabled() ? 1 : 0, "wsh", false, CheckWshDot, CheckWshBadge);
                UpdateCheckItem(CheckPrivacyTime, CheckPrivacyStatus, privacyTime, privacyCount, "riesgos", true, CheckPrivacyDot, CheckPrivacyBadge);

                // Update dynamic titles with counts
                StartupTitleText.Text = $"Programas al Inicio ({startupCount} elementos)";
                BrowserNameText.Text = browserName;
                TasksTitleText.Text = $"Tareas Programadas ({tasksCount} elementos)";
                ExclusionsTitleText.Text = $"Exclusiones de Defender ({exclusionsCount} elementos)";

                // Update Arsenal Counters
                ArsenalProcessCount.Text = $"{Process.GetProcesses().Length} en ejecución";
                ArsenalNetCount.Text = $"{connections.Count} conexiones";
                ArsenalStartupCount.Text = $"{startupCount} elementos";
                ArsenalTasksCount.Text = $"{tasksCount} tareas activas";
                ArsenalPrivacyCount.Text = privacyCount > 0 ? $"{privacyCount} riesgos" : "Protegido";
                ArsenalQuarantineCount.Text = $"{QuarantineService.GetQuarantinedApps().Count} elementos";

                // Update Pause Button State
                UpdatePauseButtonState();
            });
        });
    }



    private void UpdateCheckItem(System.Windows.Controls.TextBlock timeBlock, 
                                 System.Windows.Controls.TextBlock? countBlock, 
                                 DateTime time, int count, string unit = "", 
                                 bool isRisk = false,
                                 System.Windows.Shapes.Ellipse? dot = null,
                                 System.Windows.Controls.Border? badge = null)
    {
        if (time == DateTime.MinValue)
        {
            timeBlock.Text = "--:--";
            if (countBlock != null) countBlock.Text = "Cargando";
            return;
        }

        timeBlock.Text = time.ToString("hh:mm tt").Replace(".", "").ToUpper();

        if (countBlock != null)
        {
            var isPaused = App.CurrentApp.IsMonitoringPaused;
            var grayBadge = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"));
            var grayDot = new SolidColorBrush(Colors.Gray);
            
            if (isPaused)
            {
                countBlock.Text = "Pausado";
                if (dot != null) dot.Fill = grayDot;
                if (badge != null) badge.Background = grayBadge;
                return;
            }

            var greenBadge = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            var greenDot = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5CFF5C"));
            var blueBadge = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A5F"));
            var blueDot = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DA8DA"));
            var redBadge = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
            var redDot = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4C4C"));

            if (unit == "active") // Defender, Tasks, etc.
            {
                countBlock.Text = "Activo";
                if (dot != null) dot.Fill = greenDot;
                if (badge != null) badge.Background = greenBadge;
            }
            else if (unit == "hosts")
            {
                countBlock.Text = count > 0 ? $"Activo ({count})" : "Activo";
                if (dot != null) dot.Fill = greenDot;
                if (badge != null) badge.Background = greenBadge;
            }
            else if (unit == "ddos")
            {
                if (count == 0)
                {
                    countBlock.Text = "Activo";
                    if (dot != null) dot.Fill = greenDot;
                    if (badge != null) badge.Background = greenBadge;
                }
                else
                {
                    countBlock.Text = $"{count} elem.";
                    if (dot != null) dot.Fill = blueDot;
                    if (badge != null) badge.Background = blueBadge;
                }
            }
            else if (unit == "wsh")
            {
                // count 0 = Disabled (Secure), count 1 = Enabled (At risk)
                if (count == 0)
                {
                    countBlock.Text = "Seguro";
                    if (dot != null) dot.Fill = greenDot;
                    if (badge != null) badge.Background = greenBadge;
                }
                else
                {
                    countBlock.Text = "Riesgo";
                    if (dot != null) dot.Fill = redDot;
                    if (badge != null) badge.Background = redBadge;
                }
            }
            else if (unit == "loading")
            {
                countBlock.Text = "Cargando";
                if (dot != null) dot.Fill = blueDot;
                if (badge != null) badge.Background = blueBadge;
            }
            else if (isRisk && count > 0)
            {
                countBlock.Text = $"{count} {unit}";
                if (dot != null) dot.Fill = redDot;
                if (badge != null) badge.Background = redBadge;
            }
            else if (unit == "riesgos" && count == 0)
            {
                countBlock.Text = "Activo";
                if (dot != null) dot.Fill = greenDot;
                if (badge != null) badge.Background = greenBadge;
            }
            else
            {
                string label = unit == "items" ? "elem." : unit;
                countBlock.Text = $"{count} {label}";
                if (dot != null) dot.Fill = blueDot;
                if (badge != null) badge.Background = blueBadge;
            }
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        var app = App.CurrentApp;
        if (app.IsMonitoringPaused)
        {
            app.ResumeMonitoring();
        }
        else
        {
            app.PauseMonitoring();
        }
        UpdatePauseButtonState();
        // Force immediate refresh to update status badge
        _ = RefreshDashboardAsync(); 
    }

    private void UpdatePauseButtonState()
    {
        var isPaused = App.CurrentApp.IsMonitoringPaused;
        
        if (isPaused)
        {
            PauseText.Text = "REANUDAR";
            PauseIcon.Text = "▶️"; // Play icon
            PauseButtonBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E2723")); // Dark Red/Brown
            PauseButtonBorder.BorderBrush = FindResource("WarningBrush") as SolidColorBrush;
            PauseButton.ToolTip = "Reanudar protección en tiempo real";
        }
        else
        {
            PauseText.Text = "PAUSAR";
            PauseIcon.Text = "⏸️"; // Pause icon
            PauseButtonBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A5F")); // Default Blue
            PauseButtonBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DA8DA"));
            PauseButton.ToolTip = "Detener temporalmente todos los monitores";
        }
    }

    private void UpdateOverallStatus(DefenderInfo defenderInfo, int suspiciousConnections)
    {
        bool isPaused = App.CurrentApp.IsMonitoringPaused;

        // Footer status sync
        if (isPaused)
        {
            FooterStatusText.Text = "Monitoreo Pausado";
            FooterStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")); // Gray
            FooterStatusDot.Fill = new SolidColorBrush(Colors.DimGray);
            if (FooterStatusGlow != null) FooterStatusGlow.Color = Colors.Transparent;
        }
        else
        {
            FooterStatusText.Text = "Monitoreo Activo";
            FooterStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")); // Green
            FooterStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            if (FooterStatusGlow != null) FooterStatusGlow.Color = (Color)ColorConverter.ConvertFromString("#27AE60");
        }

        if (isPaused)
        {
            StatusText.Text = "SISTEMA PAUSADO";
            StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")); // Grey
            StatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));
            StatusDot.Fill = new SolidColorBrush(Colors.Gray);
            StatusGlow.Color = Colors.Transparent;
            return;
        }

        bool isSecure = defenderInfo.RealTimeProtectionEnabled && suspiciousConnections == 0;

        if (isSecure)
        {
            StatusText.Text = "PROTEGIDO";
            StatusBadge.Background = FindResource("SuccessBrush") as SolidColorBrush;
            StatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5CFF5C"));
            StatusGlow.Color = (Color)ColorConverter.ConvertFromString("#27AE60");
        }
        else if (!defenderInfo.RealTimeProtectionEnabled)
        {
            StatusText.Text = "DEFENDER DESACTIVADO";
            StatusBadge.Background = FindResource("DangerBrush") as SolidColorBrush;
            StatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
            StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4C4C"));
            StatusGlow.Color = (Color)ColorConverter.ConvertFromString("#E74C3C");
        }
        else
        {
            StatusText.Text = "REVISAR CONEXIONES";
            StatusBadge.Background = FindResource("WarningBrush") as SolidColorBrush;
            StatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12"));
            StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD166"));
            StatusGlow.Color = (Color)ColorConverter.ConvertFromString("#F39C12");
        }
    }

    private void ProcessViewer_Click(object sender, EventArgs e)
    {
        var window = new ProcessViewerWindow();
        window.Show();
    }

    private void NetworkViewer_Click(object sender, EventArgs e)
    {
        var window = new NetworkViewerWindow();
        window.Show();
    }

    private void NetworkStat_MouseDown(object sender, MouseButtonEventArgs e)
    {
        NetworkViewer_Click(sender, e);
    }

    private void StartupManager_Click(object sender, EventArgs e)
    {
        var window = new StartupManagerWindow();
        window.Show();
    }

    private void ViewDefenderHome_Click(object sender, EventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "windowsdefender:",
                UseShellExecute = true
            });
        }
        catch
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:windowsdefender",
                UseShellExecute = true
            });
        }
    }

    private void ViewHosts_Click(object sender, EventArgs e)
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
                UseShellExecute = true,
                Verb = "runas" // Abrir como administrador
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al abrir el archivo HOSTS: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ViewExclusions_Click(object sender, EventArgs e)
    {
        try
        {
            // Intentar abrir directamente la sección de exclusiones si es posible, 
            // si no, ir a la configuración de protección contra amenazas
            Process.Start(new ProcessStartInfo
            {
                FileName = "windowsdefender://threatsettings",
                UseShellExecute = true
            });
        }
        catch
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:windowsdefender",
                UseShellExecute = true
            });
        }
    }

    private void ViewDefaultApps_Click(object sender, EventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:defaultapps",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "ViewDefaultApps_Click");
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWin = new SettingsWindow();
        settingsWin.ShowDialog();
        
        if (settingsWin.SettingsChanged)
        {
            _ = RefreshDashboardAsync();
        }
    }

    private void Privacy_Click(object sender, EventArgs e)
    {
        var win = new PrivacyManagerWindow();
        win.Show();
    }

    private void Quarantine_Click(object sender, EventArgs e) => new QuarantineWindow().Show();
    private void ScheduledTasks_Click(object sender, EventArgs e) => new ScheduledTasksWindow().Show();

    private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        var confirmWin = new ConfirmExitWindow();
        confirmWin.Owner = this;
        confirmWin.ShowDialog();
        
        if (confirmWin.Confirmed)
        {
            // Libear recursos locales e indicar al sistema el cierre total
            _refreshTimer?.Dispose();
            Application.Current.Shutdown();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // No cerrar, solo ocultar
        e.Cancel = true;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer?.Dispose();
        base.OnClosed(e);
    }
}
