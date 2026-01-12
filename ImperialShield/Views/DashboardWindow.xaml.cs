using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
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
            var exclusions = _defenderMonitor.GetCurrentExclusions();
            var connections = _networkMonitor.GetTcpConnections();
            var suspiciousConnections = connections.Where(c => c.ThreatLevel >= ConnectionThreatLevel.High).ToList();

            Dispatcher.Invoke(() =>
            {
                UpdateDefenderStatus(defenderInfo);
                UpdateExclusionsStatus(exclusions.Count);
                UpdateConnectionsStatus(connections.Count, suspiciousConnections.Count);
                UpdateOverallStatus(defenderInfo, suspiciousConnections.Count);
            });
        });
    }

    private void UpdateDefenderStatus(DefenderInfo info)
    {
        // Estado principal
        if (info.RealTimeProtectionEnabled)
        {
            DefenderStatus.Text = "Activo";
            DefenderStatus.Foreground = FindResource("SuccessBrush") as SolidColorBrush;
            DefenderIcon.Text = "🛡️";
        }
        else
        {
            DefenderStatus.Text = "⚠️ DESACTIVADO";
            DefenderStatus.Foreground = FindResource("DangerBrush") as SolidColorBrush;
            DefenderIcon.Text = "⚠️";
        }

        // Detalles
        RealTimeStatus.Text = info.RealTimeProtectionEnabled ? "✅ Activo" : "❌ Inactivo";
        RealTimeStatus.Foreground = info.RealTimeProtectionEnabled 
            ? FindResource("SuccessBrush") as SolidColorBrush 
            : FindResource("DangerBrush") as SolidColorBrush;

        BehaviorStatus.Text = info.BehaviorMonitorEnabled ? "✅ Activo" : "❌ Inactivo";
        BehaviorStatus.Foreground = info.BehaviorMonitorEnabled 
            ? FindResource("SuccessBrush") as SolidColorBrush 
            : FindResource("DangerBrush") as SolidColorBrush;

        SignatureVersion.Text = info.SignatureVersion.Length > 20 
            ? info.SignatureVersion.Substring(0, 20) + "..." 
            : info.SignatureVersion;

        SignatureAge.Text = info.SignatureAgeDays switch
        {
            0 => "✅ Actualizado hoy",
            1 => "✅ 1 día",
            <= 3 => $"🟡 {info.SignatureAgeDays} días",
            _ => $"🔴 {info.SignatureAgeDays} días"
        };
        SignatureAge.Foreground = info.SignatureAgeDays <= 3 
            ? FindResource("SuccessBrush") as SolidColorBrush 
            : FindResource("DangerBrush") as SolidColorBrush;

        LastScan.Text = info.LastFullScan?.ToString("dd/MM/yyyy HH:mm") ?? "Nunca";
    }

    private void UpdateExclusionsStatus(int count)
    {
        ExclusionsCount.Text = count.ToString();
        
        if (count == 0)
        {
            ExclusionsStatus.Text = "Sin exclusiones";
            ExclusionsStatus.Foreground = FindResource("SuccessBrush") as SolidColorBrush;
        }
        else if (count <= 5)
        {
            ExclusionsStatus.Text = "Normal";
            ExclusionsStatus.Foreground = FindResource("SuccessBrush") as SolidColorBrush;
        }
        else
        {
            ExclusionsStatus.Text = "⚠️ Revisar";
            ExclusionsStatus.Foreground = FindResource("WarningBrush") as SolidColorBrush;
        }
    }

    private void UpdateConnectionsStatus(int total, int suspicious)
    {
        ConnectionsCount.Text = total.ToString();
        
        if (suspicious == 0)
        {
            ConnectionsStatus.Text = "✅ Normal";
            ConnectionsStatus.Foreground = FindResource("SuccessBrush") as SolidColorBrush;
        }
        else
        {
            ConnectionsStatus.Text = $"⚠️ {suspicious} sospechosa(s)";
            ConnectionsStatus.Foreground = FindResource("DangerBrush") as SolidColorBrush;
        }
    }

    private void UpdateOverallStatus(DefenderInfo defenderInfo, int suspiciousConnections)
    {
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

    private void ProcessViewer_Click(object sender, RoutedEventArgs e)
    {
        var window = new ProcessViewerWindow();
        window.Show();
    }

    private void NetworkViewer_Click(object sender, RoutedEventArgs e)
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

    private void ViewExclusions_Click(object sender, RoutedEventArgs e)
    {
        var exclusions = _defenderMonitor.GetCurrentExclusions();
        
        if (exclusions.Count == 0)
        {
            MessageBox.Show("No hay exclusiones configuradas en Windows Defender.", 
                "Exclusiones", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var message = "=== Exclusiones de Windows Defender ===\n\n" +
                     string.Join("\n", exclusions.Select((e, i) => $"{i + 1}. {e}")) +
                     "\n\n¿Deseas abrir la configuración de Windows Defender?";

        var result = MessageBox.Show(message, "Exclusiones", 
            MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "windowsdefender://threatsettings",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Fallback a Windows Security
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:windowsdefender",
                    UseShellExecute = true
                });
            }
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Configuración próximamente...", 
            "Configuración", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
    {
        Hide();
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
