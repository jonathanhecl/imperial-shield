using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ImperialShield.Services;

namespace ImperialShield.Views;

public partial class PrivacyManagerWindow : Window
{
    private PrivacyMonitor _monitor;
    private List<UnifiedAppViewModel> _allApps = new();

    // Trusted publishers list
    private static readonly string[] TrustedPublishers = new[]
    {
        "Microsoft", "Google", "Mozilla", "Apple", "Adobe", "NVIDIA", "Intel", "AMD",
        "Valve", "Steam", "Discord", "Spotify", "Zoom", "Logitech", "Razer", "Elgato",
        "OBS", "Corsair", "ASUS", "MSI", "Realtek", "Dell", "HP", "Lenovo", "Samsung"
    };

    public enum SecurityLevel { Verified, Unverified, Suspicious }

    public class UnifiedAppViewModel
    {
        public string AppId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsNonPackaged { get; set; }
        
        // Permissions
        public bool HasCameraAccess { get; set; }
        public bool HasMicAccess { get; set; }
        public bool IsCameraInUse { get; set; }
        public bool IsMicInUse { get; set; }
        
        // State
        public bool IsRunning { get; set; }
        public bool IsInUse => IsCameraInUse || IsMicInUse;
        
        // Security
        public SecurityLevel Security { get; set; } = SecurityLevel.Verified;
        public string Publisher { get; set; } = string.Empty;
        
        // For sorting (higher = more priority)
        public int SortPriority => IsInUse ? 3 : (IsRunning ? 2 : 1);
        
        // UI Bindings
        public string AppType => IsNonPackaged ? "Aplicación de Escritorio" : "Microsoft Store";
        
        public Brush CameraBadgeBg => HasCameraAccess 
            ? new SolidColorBrush(Color.FromArgb(40, 77, 168, 218)) 
            : new SolidColorBrush(Color.FromArgb(20, 100, 116, 139));
        public Brush CameraBadgeFg => HasCameraAccess 
            ? Brushes.White 
            : new SolidColorBrush(Color.FromRgb(71, 85, 105));
            
        public Brush MicBadgeBg => HasMicAccess 
            ? new SolidColorBrush(Color.FromArgb(40, 239, 68, 68)) 
            : new SolidColorBrush(Color.FromArgb(20, 100, 116, 139));
        public Brush MicBadgeFg => HasMicAccess 
            ? Brushes.White 
            : new SolidColorBrush(Color.FromRgb(71, 85, 105));

        public string StatusText => IsInUse ? "En Uso" : (IsRunning ? "Abierto" : "Cerrado");
        public Brush StatusColor => IsInUse 
            ? new SolidColorBrush(Color.FromRgb(239, 68, 68))   // Red
            : IsRunning 
                ? new SolidColorBrush(Color.FromRgb(77, 168, 218))  // Blue
                : new SolidColorBrush(Color.FromRgb(100, 116, 139)); // Gray

        public string SecurityText => Security switch
        {
            SecurityLevel.Verified => "Verificado",
            SecurityLevel.Unverified => "Sin verificar",
            SecurityLevel.Suspicious => "Sospechoso",
            _ => "Desconocido"
        };
        
        public Brush SecurityColor => Security switch
        {
            SecurityLevel.Verified => new SolidColorBrush(Color.FromRgb(34, 197, 94)),    // Green
            SecurityLevel.Unverified => new SolidColorBrush(Color.FromRgb(234, 179, 8)),  // Yellow
            SecurityLevel.Suspicious => new SolidColorBrush(Color.FromRgb(239, 68, 68)),  // Red
            _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
        };
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
        var runningProcs = Process.GetProcesses();
        
        // Get all apps with camera access
        var camApps = _monitor.GetAllAppsWithPermission(DeviceType.Camera);
        var micApps = _monitor.GetAllAppsWithPermission(DeviceType.Microphone);

        // Merge into unified list by AppId
        var appDict = new Dictionary<string, UnifiedAppViewModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var cam in camApps)
        {
            if (!appDict.TryGetValue(cam.AppId, out var vm))
            {
                vm = CreateViewModel(cam, runningProcs);
                appDict[cam.AppId] = vm;
            }
            vm.HasCameraAccess = true;
            vm.IsCameraInUse = cam.IsActive;
        }

        foreach (var mic in micApps)
        {
            if (!appDict.TryGetValue(mic.AppId, out var vm))
            {
                vm = CreateViewModel(mic, runningProcs);
                appDict[mic.AppId] = vm;
            }
            vm.HasMicAccess = true;
            vm.IsMicInUse = mic.IsActive;
        }

        // Sort: InUse first, then Running, then Stopped
        _allApps = appDict.Values
            .OrderByDescending(x => x.SortPriority)
            .ThenBy(x => x.DisplayName)
            .ToList();

        ApplyFilter();
        LastUpdatedText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
    }

    private UnifiedAppViewModel CreateViewModel(PrivacyMonitor.PrivacyAppHistory app, Process[] runningProcs)
    {
        bool isRunning = false;
        
        if (app.IsNonPackaged)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(app.AppId);
            isRunning = runningProcs.Any(p => p.ProcessName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            isRunning = runningProcs.Any(p => p.ProcessName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase));
        }

        var vm = new UnifiedAppViewModel
        {
            AppId = app.AppId,
            DisplayName = app.DisplayName,
            IsNonPackaged = app.IsNonPackaged,
            IsRunning = isRunning
        };

        // Analyze security
        AnalyzeSecurity(vm);

        return vm;
    }

    private void AnalyzeSecurity(UnifiedAppViewModel vm)
    {
        // Store apps are considered verified
        if (!vm.IsNonPackaged)
        {
            vm.Security = SecurityLevel.Verified;
            vm.Publisher = "Microsoft Store";
            return;
        }

        // Check if it's Imperial Shield
        if (vm.DisplayName.Contains("ImperialShield", StringComparison.OrdinalIgnoreCase) ||
            vm.AppId.Contains("ImperialShield", StringComparison.OrdinalIgnoreCase))
        {
            vm.Security = SecurityLevel.Verified;
            vm.Publisher = "Imperial Shield";
            return;
        }

        // For non-packaged apps, verify signature
        if (System.IO.File.Exists(vm.AppId))
        {
            bool isSigned = VerifySignature(vm.AppId);
            string publisher = GetPublisher(vm.AppId);
            vm.Publisher = publisher;

            if (!isSigned)
            {
                // Check suspicious locations
                string lowerPath = vm.AppId.ToLower();
                if (lowerPath.Contains("\\temp\\") || 
                    lowerPath.Contains("\\downloads\\") || 
                    lowerPath.Contains("\\desktop\\") ||
                    lowerPath.Contains("appdata\\local\\temp"))
                {
                    vm.Security = SecurityLevel.Suspicious;
                }
                else
                {
                    vm.Security = SecurityLevel.Unverified;
                }
            }
            else if (TrustedPublishers.Any(p => publisher.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                vm.Security = SecurityLevel.Verified;
            }
            else
            {
                vm.Security = SecurityLevel.Verified; // Signed = Verified
            }
        }
        else
        {
            vm.Security = SecurityLevel.Unverified;
            vm.Publisher = "Desconocido";
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

    private void ApplyFilter()
    {
        IEnumerable<UnifiedAppViewModel> filtered = _allApps;

        // Apply exclusive filters
        if (FilterInUse.IsChecked == true)
        {
            filtered = filtered.Where(x => x.IsInUse);
        }
        else if (FilterRunning.IsChecked == true)
        {
            filtered = filtered.Where(x => x.IsRunning);
        }
        else if (FilterCamera.IsChecked == true)
        {
            filtered = filtered.Where(x => x.HasCameraAccess);
        }
        else if (FilterMic.IsChecked == true)
        {
            filtered = filtered.Where(x => x.HasMicAccess);
        }
        // FilterAll shows everything

        var list = filtered.ToList();
        AppList.ItemsSource = list;
        
        StatsText.Text = $"{list.Count} aplicaciones";
        EmptyState.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton clicked)
        {
            // Make filters mutually exclusive
            var filters = new[] { FilterAll, FilterInUse, FilterRunning, FilterCamera, FilterMic };
            foreach (var f in filters)
            {
                if (f != clicked) f.IsChecked = false;
            }
            
            // Ensure at least one is checked
            if (clicked.IsChecked != true)
                clicked.IsChecked = true;

            ApplyFilter();
        }
    }

    private void RevokeCamera_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is UnifiedAppViewModel app)
        {
            if (MessageBox.Show($"¿Revocar acceso a la CÁMARA para '{app.DisplayName}'?\n\nEsto modificará el registro de Windows.", 
                "Revocar Cámara", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _monitor.RevokePermission(app.AppId, app.IsNonPackaged, DeviceType.Camera);
                MessageBox.Show("Permiso de cámara revocado. Reinicia la aplicación para aplicar los cambios.", 
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshData();
            }
        }
    }

    private void RevokeMic_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is UnifiedAppViewModel app)
        {
            if (MessageBox.Show($"¿Revocar acceso al MICRÓFONO para '{app.DisplayName}'?\n\nEsto modificará el registro de Windows.", 
                "Revocar Micrófono", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _monitor.RevokePermission(app.AppId, app.IsNonPackaged, DeviceType.Microphone);
                MessageBox.Show("Permiso de micrófono revocado. Reinicia la aplicación para aplicar los cambios.", 
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshData();
            }
        }
    }

    private void Terminate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is UnifiedAppViewModel app)
        {
            if (MessageBox.Show($"¿Terminar el proceso '{app.DisplayName}'?\n\nEsto cerrará la aplicación forzosamente.", 
                "Terminar Proceso", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    TerminateProcess(app);
                    RefreshData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al terminar proceso: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void TerminateProcess(UnifiedAppViewModel app)
    {
        string procName = app.IsNonPackaged 
            ? System.IO.Path.GetFileNameWithoutExtension(app.AppId) 
            : app.DisplayName;
        
        var procs = Process.GetProcessesByName(procName);
        if (procs.Length == 0)
        {
            procs = Process.GetProcesses()
                .Where(p => p.ProcessName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        if (procs.Length > 0)
        {
            int killed = 0;
            foreach (var p in procs)
            {
                try { p.Kill(); killed++; } catch { }
            }
            if (killed > 0)
            {
                MessageBox.Show($"Se terminaron {killed} proceso(s).", "Proceso Terminado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            MessageBox.Show("No se encontraron procesos activos para esta aplicación.", "Sin Procesos", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshData();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
