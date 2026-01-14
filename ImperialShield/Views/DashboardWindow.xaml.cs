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
        InitializeComponent();
        _defenderMonitor = new DefenderMonitor();
        _networkMonitor = new NetworkMonitor();
        
        // Establecer versión
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Imperial Shield v{version?.Major}.{version?.Minor}.{version?.Build}";

        Loaded += async (s, e) => await RefreshDashboardAsync();
        
        // Refrescar cada 30 segundos
        _refreshTimer = new Timer(async _ => 
        {
            await Dispatcher.InvokeAsync(async () => await RefreshDashboardAsync());
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
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
            var privacyTime = app.PrivacyMonitor?.LastChecked ?? DateTime.MinValue;
            var privacyCount = app.PrivacyMonitor?.ActiveRiskCount ?? 0;

            var connections = _networkMonitor.GetTcpConnections();
            var suspiciousConnections = connections.Where(c => c.ThreatLevel >= ConnectionThreatLevel.High).ToList();

            Dispatcher.Invoke(() =>
            {
                UpdateOverallStatus(defenderInfo, suspiciousConnections.Count);
                
                // Update Last Checks List
                UpdateCheckItem(CheckDefenseTime, CheckDefenseStatus, defenseTime, 0, "", false);
                UpdateCheckItem(CheckHostsTime, CheckHostsCount, hostsTime, hostsCount, "hosts", false); 
                UpdateCheckItem(CheckExclusionsTime, CheckExclusionsCount, exclusionsTime, exclusionsCount, "items");
                UpdateCheckItem(CheckConnectionsTime, CheckConnectionsCount, netTime, suspiciousConnections.Count, "items");
                UpdateCheckItem(CheckStartupTime, CheckStartupCount, startupTime, startupCount, "items");
                UpdateCheckItem(CheckTasksTime, CheckTasksCount, tasksTime, tasksCount, "active");
                UpdateCheckItem(CheckPrivacyTime, CheckPrivacyCount, privacyTime, privacyCount, "riesgos", true);

                // Update Pause Button State
                UpdatePauseButtonState();
            });
        });
    }



    private void UpdateCheckItem(System.Windows.Controls.TextBlock timeBlock, System.Windows.Controls.TextBlock? countBlock, DateTime time, int count, string unit = "", bool isRisk = false)
    {
        if (time == DateTime.MinValue)
        {
            timeBlock.Text = "--:--";
            if (countBlock != null) countBlock.Text = "...";
            return;
        }

        // Format time: "14:30" with AM/PM style
        timeBlock.Text = time.ToString("hh:mm tt").Replace(".", "").ToUpper();

        if (countBlock != null)
        {
            // For status monitors (Defender = "Active", Privacy with 0 risks = "Blocked")
            if (unit == "")
            {
                countBlock.Text = isRisk ? "Blocked" : "Active";
            }
            else if (unit == "hosts")
            {
                countBlock.Text = count > 0 ? $"Active ({count})" : "Active";
            }
            else if (unit == "riesgos" && count == 0)
            {
                countBlock.Text = "Blocked";
            }
            else
            {
                countBlock.Text = $"{count} {unit}";
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
        if (App.CurrentApp.IsMonitoringPaused)
        {
            StatusText.Text = "⏸️ SISTEMA PAUSADO";
            StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")); // Grey
            StatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));
            StatusDot.Fill = new SolidColorBrush(Colors.Gray);
            StatusGlow.Color = Colors.Transparent;
            return;
        }

        bool isSecure = defenderInfo.RealTimeProtectionEnabled && suspiciousConnections == 0;

        if (isSecure)
        {
            StatusText.Text = "🟢 Sistema Protegido";
            StatusBadge.Background = FindResource("SuccessBrush") as SolidColorBrush;
        }
        else if (!defenderInfo.RealTimeProtectionEnabled)
        {
            StatusText.Text = "🔴 Defender Desactivado";
            StatusBadge.Background = FindResource("DangerBrush") as SolidColorBrush;
        }
        else
        {
            StatusText.Text = "🟡 Revisar Conexiones";
            StatusBadge.Background = FindResource("WarningBrush") as SolidColorBrush;
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
