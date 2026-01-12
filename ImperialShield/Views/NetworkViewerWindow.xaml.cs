using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using ImperialShield.Services;

namespace ImperialShield.Views;

/// <summary>
/// Monitor de conexiones de red con detección de amenazas
/// </summary>
public partial class NetworkViewerWindow : Window, INotifyPropertyChanged
{
    private readonly NetworkMonitor _networkMonitor;
    private readonly ProcessAnalyzer _processAnalyzer;
    private bool _showOnlySuspicious;
    private List<ConnectionInfo> _allConnections = new();

    public ObservableCollection<ConnectionDisplayItem> Connections { get; } = new();
    public ConnectionDisplayItem? SelectedConnection { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public NetworkViewerWindow()
    {
        InitializeComponent();
        DataContext = this;
        _networkMonitor = new NetworkMonitor();
        _processAnalyzer = new ProcessAnalyzer();
        
        Loaded += (s, e) => RefreshConnections();
    }

    private void RefreshConnections()
    {
        StatusText.Text = "Analizando conexiones de red...";
        
        Task.Run(() =>
        {
            _allConnections = _networkMonitor.GetTcpConnections();
            
            Dispatcher.Invoke(() =>
            {
                UpdateDisplay();
                UpdateAlert();
                StatusText.Text = "Listo";
                LastUpdateText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
            });
        });
    }

    private void UpdateDisplay()
    {
        Connections.Clear();

        var filtered = _showOnlySuspicious 
            ? _allConnections.Where(c => c.ThreatLevel >= ConnectionThreatLevel.Medium)
            : _allConnections;

        foreach (var conn in filtered)
        {
            Connections.Add(new ConnectionDisplayItem(conn));
        }

        // Actualizar estadísticas
        TotalConnectionsText.Text = _allConnections.Count.ToString();
        EstablishedText.Text = _allConnections.Count(c => c.State == "ESTABLISHED").ToString();
        ListeningText.Text = _allConnections.Count(c => c.State == "LISTEN").ToString();
        SuspiciousText.Text = _allConnections.Count(c => c.ThreatLevel >= ConnectionThreatLevel.High).ToString();
        ExternalText.Text = _allConnections.Count(c => 
            c.RemoteAddress != "0.0.0.0" && 
            c.RemoteAddress != "127.0.0.1" && 
            !c.RemoteAddress.StartsWith("192.168.") &&
            !c.RemoteAddress.StartsWith("10.")).ToString();
    }

    private void UpdateAlert()
    {
        var criticalConnections = _allConnections
            .Where(c => c.ThreatLevel >= ConnectionThreatLevel.Critical)
            .ToList();

        if (criticalConnections.Count > 0)
        {
            AlertBox.Visibility = Visibility.Visible;
            StatusBar.Visibility = Visibility.Collapsed;
            
            var processes = string.Join(", ", criticalConnections
                .Select(c => c.ProcessName)
                .Distinct()
                .Take(5));
            
            AlertDescription.Text = $"Se detectaron {criticalConnections.Count} conexiones críticas desde: {processes}. " +
                                   "Estos procesos normalmente no deberían tener conexiones de red activas. " +
                                   "Verifica si es un comportamiento esperado.";
        }
        else
        {
            AlertBox.Visibility = Visibility.Collapsed;
            StatusBar.Visibility = Visibility.Visible;
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshConnections();
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        _showOnlySuspicious = !_showOnlySuspicious;
        FilterButton.Content = _showOnlySuspicious ? "📋 Mostrar Todas" : "🔍 Solo Sospechosas";
        UpdateDisplay();
    }

    private void ViewProcess_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedConnection == null) return;

        try
        {
            var process = Process.GetProcessById(SelectedConnection.ProcessId);
            var info = _processAnalyzer.AnalyzeProcess(process);
            
            if (info != null)
            {
                var details = $"=== Información del Proceso ===\n" +
                             $"PID: {info.Pid}\n" +
                             $"Nombre: {info.Name}\n" +
                             $"Ruta: {info.Path}\n\n" +
                             $"=== Análisis de Seguridad ===\n" +
                             $"Nivel: {info.ThreatLevel}\n" +
                             $"Descripción: {info.ThreatDescription}";
                
                MessageBox.Show(details, $"Proceso: {info.Name}", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo obtener información del proceso: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyRemoteIP_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedConnection != null)
        {
            Clipboard.SetText(SelectedConnection.SourceInfo.RemoteAddress);
            StatusText.Text = $"IP {SelectedConnection.SourceInfo.RemoteAddress} copiada al portapapeles.";
        }
    }

    private void SearchIP_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedConnection != null)
        {
            var ip = SelectedConnection.SourceInfo.RemoteAddress;
            var url = $"https://www.abuseipdb.com/check/{ip}";
            
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir navegador: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void KillProcess_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedConnection == null) return;

        var result = MessageBox.Show(
            $"¿Estás seguro de que deseas terminar el proceso?\n\n" +
            $"Nombre: {SelectedConnection.ProcessName}\n" +
            $"PID: {SelectedConnection.ProcessId}\n" +
            $"Conexión: {SelectedConnection.RemoteEndpoint}\n\n" +
            $"⚠️ Esto cerrará TODAS las conexiones de este proceso.",
            "Confirmar Terminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            if (_processAnalyzer.KillProcess(SelectedConnection.ProcessId))
            {
                StatusText.Text = $"Proceso {SelectedConnection.ProcessName} terminado exitosamente.";
                RefreshConnections();
            }
            else
            {
                MessageBox.Show("No se pudo terminar el proceso.", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// Item de visualización para el DataGrid de conexiones
/// </summary>
public class ConnectionDisplayItem
{
    public ConnectionInfo SourceInfo { get; }

    public int ProcessId => SourceInfo.ProcessId;
    public string ProcessName => SourceInfo.ProcessName;
    public string State => SourceInfo.State;
    public ConnectionThreatLevel ThreatLevel => SourceInfo.ThreatLevel;
    public string ThreatDescription => SourceInfo.ThreatDescription;

    public string LocalEndpoint => $"{SourceInfo.LocalAddress}:{SourceInfo.LocalPort}";
    public string RemoteEndpoint => $"{SourceInfo.RemoteAddress}:{SourceInfo.RemotePort}";

    public string ThreatLevelIcon => ThreatLevel switch
    {
        ConnectionThreatLevel.Critical => "🚨",
        ConnectionThreatLevel.High => "🔴",
        ConnectionThreatLevel.Medium => "🟠",
        ConnectionThreatLevel.Low => "🟡",
        ConnectionThreatLevel.Safe => "🟢",
        _ => "⚪"
    };

    public ConnectionDisplayItem(ConnectionInfo info)
    {
        SourceInfo = info;
    }
}
