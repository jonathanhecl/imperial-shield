using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ImperialShield.Services;

namespace ImperialShield.Views;

public partial class PrivacyManagerWindow : Window
{
    private PrivacyMonitor _monitor;

    // Trusted publishers list
    private static readonly string[] TrustedPublishers = new[]
    {
        "Microsoft", "Google", "Mozilla", "Apple", "Adobe", "NVIDIA", "Intel", "AMD",
        "Valve", "Steam", "Discord", "Spotify", "Zoom", "Logitech", "Razer", "Elgato",
        "OBS", "Corsair", "ASUS", "MSI", "Realtek", "Dell", "HP", "Lenovo", "Samsung"
    };

    public enum ThreatLevel { Trusted, Safe, Medium, High }

    public class PrivacyAppViewModel
    {
        public string AppId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsNonPackaged { get; set; }
        public DateTime LastUsedStart { get; set; }
        public DateTime LastUsedStop { get; set; }
        public bool IsActiveRisk { get; set; }
        public bool IsRunning { get; set; }
        public DeviceType DeviceType { get; set; }
        
        // Security properties
        public bool IsSigned { get; set; }
        public string Publisher { get; set; } = string.Empty;
        public ThreatLevel ThreatLevel { get; set; } = ThreatLevel.Safe;
        public bool IsImperialShield { get; set; }
        public bool IsStoreApp { get; set; }
        
        // For UI Binding
        public string LastActivity => LastUsedStop == DateTime.MinValue 
            ? (IsActiveRisk ? "Ahora mismo" : "Nunca/Desconocido") 
            : LastUsedStop.ToString("g");

        public string StatusText => IsActiveRisk ? "🔴 En Uso" : (IsRunning ? "🔵 Ejecutando" : "⚫ Detenido");
        
        // Security UI Bindings
        public string ThreatIcon => ThreatLevel switch
        {
            ThreatLevel.Trusted => "🛡️",
            ThreatLevel.Safe => "✅",
            ThreatLevel.Medium => "🟡",
            ThreatLevel.High => "🔴",
            _ => "❓"
        };

        public string ThreatText => ThreatLevel switch
        {
            ThreatLevel.Trusted => "Confiable",
            ThreatLevel.Safe => "Seguro",
            ThreatLevel.Medium => "Sin Firma",
            ThreatLevel.High => "Sospechoso",
            _ => "Desconocido"
        };

        public Brush ThreatColor => ThreatLevel switch
        {
            ThreatLevel.Trusted => new SolidColorBrush(Color.FromRgb(77, 168, 218)),
            ThreatLevel.Safe => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
            ThreatLevel.Medium => new SolidColorBrush(Color.FromRgb(234, 179, 8)),
            ThreatLevel.High => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
        };

        public string SignatureText => IsImperialShield ? "🛡️ Sistema (Verificado)" 
            : IsStoreApp ? "📦 Microsoft Store" 
            : IsSigned ? $"✓ {Publisher}" 
            : "⚠️ Sin Firma";

        public Brush SignatureColor => IsImperialShield || IsStoreApp ? new SolidColorBrush(Color.FromRgb(77, 168, 218))
            : IsSigned ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
            : new SolidColorBrush(Color.FromRgb(234, 179, 8));
        
        public PrivacyMonitor.PrivacyAppHistory OriginalData { get; set; } = new();
    }

    public PrivacyManagerWindow()
    {
        InitializeComponent();
        _monitor = new PrivacyMonitor();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshData();
    }

    private void RefreshData()
    {
        // Get Camera Apps
        var camApps = _monitor.GetAllAppsWithPermission(DeviceType.Camera);
        var camViewModels = MapToViewModel(camApps, DeviceType.Camera);
        CameraGrid.ItemsSource = camViewModels;

        // Get Mic Apps
        var micApps = _monitor.GetAllAppsWithPermission(DeviceType.Microphone);
        var micViewModels = MapToViewModel(micApps, DeviceType.Microphone);
        MicGrid.ItemsSource = micViewModels;

        LastUpdatedText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
    }

    private List<PrivacyAppViewModel> MapToViewModel(List<PrivacyMonitor.PrivacyAppHistory> history, DeviceType type)
    {
        var runningProcs = Process.GetProcesses();
        
        var groupedHistory = history.GroupBy(h => h.AppId).Select(g => new PrivacyMonitor.PrivacyAppHistory
        {
            AppId = g.Key,
            DisplayName = g.First().DisplayName,
            IsNonPackaged = g.First().IsNonPackaged,
            LastUsedStart = g.Max(x => x.LastUsedStart),
            LastUsedStop = g.Max(x => x.LastUsedStop),
            IsActive = g.Any(x => x.IsActive),
            PermissionStatus = g.First().PermissionStatus
        });

        var result = new List<PrivacyAppViewModel>();

        foreach (var h in groupedHistory)
        {
            bool isRunning = false;
            
            if (h.IsNonPackaged)
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(h.AppId);
                isRunning = runningProcs.Any(p => p.ProcessName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                isRunning = runningProcs.Any(p => p.ProcessName.Contains(h.DisplayName, StringComparison.OrdinalIgnoreCase));
            }

            var vm = new PrivacyAppViewModel
            {
                AppId = h.AppId,
                DisplayName = h.DisplayName,
                IsNonPackaged = h.IsNonPackaged,
                LastUsedStart = h.LastUsedStart,
                LastUsedStop = h.LastUsedStop,
                IsActiveRisk = h.IsActive,
                IsRunning = isRunning,
                DeviceType = type,
                OriginalData = h,
                IsStoreApp = !h.IsNonPackaged
            };

            // Analyze security
            AnalyzeSecurity(vm);

            result.Add(vm);
        }
        
        return result
            .OrderByDescending(x => x.IsActiveRisk || x.IsRunning) // Primero todo lo que está activo (Rojo o Azul)
            .ThenByDescending(x => x.IsActiveRisk)               // De lo activo, primero lo que usa hardware (Rojo)
            .ThenByDescending(x => x.ThreatLevel)                // Luego por nivel de riesgo
            .ThenByDescending(x => x.LastUsedStart)              // Finalmente por actividad reciente
            .ToList();
    }

    private void AnalyzeSecurity(PrivacyAppViewModel vm)
    {
        // Check if it's Imperial Shield
        if (vm.DisplayName.Contains("ImperialShield", StringComparison.OrdinalIgnoreCase) ||
            vm.AppId.Contains("ImperialShield", StringComparison.OrdinalIgnoreCase))
        {
            vm.IsImperialShield = true;
            vm.IsSigned = true;
            vm.Publisher = "Imperial Shield";
            vm.ThreatLevel = ThreatLevel.Trusted;
            return;
        }

        // Store apps are considered trusted (signed by Microsoft)
        if (!vm.IsNonPackaged)
        {
            vm.IsSigned = true;
            vm.Publisher = "Microsoft Store";
            vm.ThreatLevel = ThreatLevel.Trusted;
            return;
        }

        // For non-packaged apps, verify signature
        if (System.IO.File.Exists(vm.AppId))
        {
            vm.IsSigned = VerifySignature(vm.AppId);
            vm.Publisher = GetPublisher(vm.AppId);
            vm.ThreatLevel = AnalyzeThreat(vm);
        }
        else
        {
            vm.IsSigned = false;
            vm.Publisher = "Desconocido";
            vm.ThreatLevel = ThreatLevel.Medium;
        }
    }

    private bool VerifySignature(string filePath)
    {
        try
        {
            var cert = X509Certificate.CreateFromSignedFile(filePath);
            return cert != null;
        }
        catch
        {
            return false;
        }
    }

    private string GetPublisher(string filePath)
    {
        try
        {
            var cert = X509Certificate.CreateFromSignedFile(filePath);
            if (cert != null)
            {
                string subject = cert.Subject;
                // Extract CN (Common Name)
                var cnStart = subject.IndexOf("CN=");
                if (cnStart >= 0)
                {
                    var cnEnd = subject.IndexOf(',', cnStart);
                    if (cnEnd < 0) cnEnd = subject.Length;
                    return subject.Substring(cnStart + 3, cnEnd - cnStart - 3).Trim('"');
                }
                return subject.Length > 50 ? subject.Substring(0, 47) + "..." : subject;
            }
        }
        catch { }
        return "Desconocido";
    }

    private ThreatLevel AnalyzeThreat(PrivacyAppViewModel vm)
    {
        // Unsigned + suspicious location = HIGH
        if (!vm.IsSigned)
        {
            string lowerPath = vm.AppId.ToLower();
            if (lowerPath.Contains("\\temp\\") || 
                lowerPath.Contains("\\downloads\\") || 
                lowerPath.Contains("\\desktop\\") ||
                lowerPath.Contains("appdata\\local\\temp"))
            {
                return ThreatLevel.High;
            }
            return ThreatLevel.Medium;
        }

        // Signed by trusted publisher
        if (TrustedPublishers.Any(p => vm.Publisher.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            return ThreatLevel.Trusted;
        }

        // Signed but unknown publisher
        return ThreatLevel.Safe;
    }

    private void Revoke_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PrivacyAppViewModel app)
        {
            if (MessageBox.Show($"¿Estás seguro de que quieres revocar el permiso de {app.DeviceType} para '{app.DisplayName}'?\n\nEsto modificará el registro de Windows.", 
                "Revocar Permiso", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _monitor.RevokePermission(app.AppId, app.IsNonPackaged, app.DeviceType);
                MessageBox.Show("Permiso revocado (establecido a 'Deny'). Reinicia la aplicación afectada para ver los cambios.");
                RefreshData();
            }
        }
    }

    private void Terminate_Click(object sender, RoutedEventArgs e)
    {
        PrivacyAppViewModel? app = null;
        if (sender is Button btn) app = btn.Tag as PrivacyAppViewModel;
        else if (sender is MenuItem mi) app = mi.DataContext as PrivacyAppViewModel;

        if (app != null)
        {
            try
            {
                TerminateProcessesForApp(app);
                RefreshData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al terminar proceso: {ex.Message}");
            }
        }
    }

    private void TerminateProcessesForApp(PrivacyAppViewModel app)
    {
        string procName = app.IsNonPackaged ? System.IO.Path.GetFileNameWithoutExtension(app.AppId) : app.DisplayName;
        
        var procs = Process.GetProcessesByName(procName);
        if (procs.Length == 0)
        {
            procs = Process.GetProcesses().Where(p => p.ProcessName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        if (procs.Length > 0)
        {
            int killed = 0;
            foreach (var p in procs)
            {
                try { p.Kill(); killed++; } catch { }
            }
        }
        else
        {
            MessageBox.Show("No se encontraron procesos activos para esta aplicación.");
        }
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        PrivacyAppViewModel? app = null;
        if (sender is MenuItem mi) app = mi.DataContext as PrivacyAppViewModel;
        else if (sender is FrameworkElement fe) app = fe.DataContext as PrivacyAppViewModel;

        if (app != null)
        {
            if (app.IsNonPackaged && System.IO.File.Exists(app.AppId))
            {
                Process.Start("explorer.exe", $"/select,\"{app.AppId}\"");
            }
            else if (!app.IsNonPackaged)
            {
                MessageBox.Show("Esta es una aplicación de Windows Store. No tiene una ubicación de archivo tradicional explorable.", "Aplicación de Sistema");
            }
            else
            {
                MessageBox.Show("No se pudo encontrar el archivo ejecutable en el disco.", "Error");
            }
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is PrivacyAppViewModel app)
        {
            Clipboard.SetText(app.AppId);
            MessageBox.Show("Ruta copiada al portapapeles.");
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshData();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
