using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ImperialShield.Services;

namespace ImperialShield.Views;

/// <summary>
/// Visor de procesos con análisis de seguridad
/// </summary>
public partial class ProcessViewerWindow : Window, INotifyPropertyChanged
{
    private readonly ProcessAnalyzer _processAnalyzer;
    private bool _showOnlySuspicious;
    private List<ProcessInfo> _allProcesses = new();

    public ObservableCollection<ProcessDisplayItem> Processes { get; } = new();
    public ProcessDisplayItem? SelectedProcess { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ProcessViewerWindow()
    {
        InitializeComponent();
        DataContext = this;
        _processAnalyzer = new ProcessAnalyzer();
        
        Loaded += (s, e) => RefreshProcesses();
    }

    private void RefreshProcesses()
    {
        StatusText.Text = "Analizando procesos...";
        
        Task.Run(() =>
        {
            _allProcesses = _processAnalyzer.GetAllProcesses();
            
            Dispatcher.Invoke(() =>
            {
                UpdateDisplay();
                StatusText.Text = "Listo";
                LastUpdateText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
            });
        });
    }

    private void UpdateDisplay()
    {
        Processes.Clear();

        var filtered = _showOnlySuspicious 
            ? _allProcesses.Where(p => p.ThreatLevel >= ThreatLevel.Medium)
            : _allProcesses;

        foreach (var process in filtered)
        {
            Processes.Add(new ProcessDisplayItem(process));
        }

        // Actualizar estadísticas
        TotalProcessesText.Text = _allProcesses.Count.ToString();
        CriticalText.Text = _allProcesses.Count(p => p.ThreatLevel == ThreatLevel.Critical).ToString();
        HighText.Text = _allProcesses.Count(p => p.ThreatLevel == ThreatLevel.High).ToString();
        MediumText.Text = _allProcesses.Count(p => p.ThreatLevel == ThreatLevel.Medium).ToString();
        SafeText.Text = _allProcesses.Count(p => p.ThreatLevel <= ThreatLevel.Low).ToString();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshProcesses();
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        _showOnlySuspicious = !_showOnlySuspicious;
        FilterButton.Content = _showOnlySuspicious ? "📋 Mostrar Todos" : "🔍 Solo Sospechosos";
        UpdateDisplay();
    }

    private void ProcessGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ViewDetails_Click(sender, e);
    }

    private void ViewDetails_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProcess == null) return;

        var details = new StringBuilder();
        details.AppendLine($"=== Detalles del Proceso ===");
        details.AppendLine($"PID: {SelectedProcess.Pid}");
        details.AppendLine($"Nombre: {SelectedProcess.Name}");
        details.AppendLine($"Ruta: {SelectedProcess.Path}");
        details.AppendLine($"");
        details.AppendLine($"=== Análisis de Seguridad ===");
        details.AppendLine($"Nivel de Amenaza: {SelectedProcess.ThreatLevel}");
        details.AppendLine($"Descripción: {SelectedProcess.ThreatDescription}");
        details.AppendLine($"");
        details.AppendLine($"=== Firma Digital ===");
        details.AppendLine($"Estado: {SelectedProcess.SignatureStatus}");

        if (SelectedProcess.SourceInfo?.SignatureInfo != null)
        {
            var sig = SelectedProcess.SourceInfo.SignatureInfo;
            details.AppendLine($"Firmante: {sig.Subject}");
            details.AppendLine($"Emisor: {sig.Issuer}");
            details.AppendLine($"Válido: {sig.ValidFrom:d} - {sig.ValidTo:d}");
        }

        details.AppendLine($"");
        details.AppendLine($"=== Recursos ===");
        details.AppendLine($"Memoria: {SelectedProcess.MemoryMB:N2} MB");
        details.AppendLine($"Tiempo CPU: {SelectedProcess.CpuTime}");
        details.AppendLine($"Inicio: {SelectedProcess.StartTime}");

        MessageBox.Show(details.ToString(), $"Detalles de {SelectedProcess.Name}", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProcess == null || string.IsNullOrEmpty(SelectedProcess.Path)) return;

        try
        {
            var directory = System.IO.Path.GetDirectoryName(SelectedProcess.Path);
            if (!string.IsNullOrEmpty(directory))
            {
                Process.Start("explorer.exe", $"/select,\"{SelectedProcess.Path}\"");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al abrir ubicación: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void KillProcess_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProcess == null) return;

        var result = MessageBox.Show(
            $"¿Estás seguro de que deseas terminar el proceso?\n\n" +
            $"Nombre: {SelectedProcess.Name}\n" +
            $"PID: {SelectedProcess.Pid}\n" +
            $"Ruta: {SelectedProcess.Path}\n\n" +
            $"⚠️ Terminar un proceso del sistema puede causar inestabilidad.",
            "Confirmar Terminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            if (_processAnalyzer.KillProcess(SelectedProcess.Pid))
            {
                StatusText.Text = $"Proceso {SelectedProcess.Name} terminado exitosamente.";
                RefreshProcesses();
            }
            else
            {
                MessageBox.Show("No se pudo terminar el proceso. Es posible que requiera privilegios elevados.", 
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
/// Item de visualización para el DataGrid de procesos
/// </summary>
public class ProcessDisplayItem
{
    public ProcessInfo SourceInfo { get; }

    public int Pid => SourceInfo.Pid;
    public string Name => SourceInfo.Name;
    public string Path => SourceInfo.Path;
    public ThreatLevel ThreatLevel => SourceInfo.ThreatLevel;
    public string ThreatDescription => SourceInfo.ThreatDescription;
    public double MemoryMB => SourceInfo.MemoryMB;
    public TimeSpan CpuTime => SourceInfo.CpuTime;
    public DateTime? StartTime => SourceInfo.StartTime;

    public string ThreatLevelIcon => ThreatLevel switch
    {
        ThreatLevel.Critical => "🔴",
        ThreatLevel.High => "🟠",
        ThreatLevel.Medium => "🟡",
        ThreatLevel.Low => "🟢",
        ThreatLevel.Safe => "✅",
        _ => "❓"
    };

    public string SignatureStatus
    {
        get
        {
            if (SourceInfo.SignatureInfo == null)
                return "No verificado";
            if (!SourceInfo.SignatureInfo.IsSigned)
                return "❌ Sin firma";
            if (!SourceInfo.SignatureInfo.IsTrustedIssuer)
                return "⚠️ Firma no confiable";
            return "✅ Firmado";
        }
    }

    public ProcessDisplayItem(ProcessInfo info)
    {
        SourceInfo = info;
    }
}

public class StringBuilder
{
    private readonly System.Text.StringBuilder _sb = new();
    public void AppendLine(string line) => _sb.AppendLine(line);
    public override string ToString() => _sb.ToString();
}
