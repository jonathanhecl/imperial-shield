using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ImperialShield.Services;

namespace ImperialShield.Views;

public partial class PrivacyManagerWindow : Window
{
    private PrivacyMonitor _monitor;

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
        
        // For UI Binding
        public string LastActivity => LastUsedStop == DateTime.MinValue 
            ? (IsActiveRisk ? "Ahora mismo" : "Nunca/Desconocido") 
            : LastUsedStop.ToString("g");

        public string StatusText => IsActiveRisk ? "🔴 En Uso" : (IsRunning ? "🟢 Ejecutando" : "⚫ Detenido");
        
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
    }

    private List<PrivacyAppViewModel> MapToViewModel(List<PrivacyMonitor.PrivacyAppHistory> history, DeviceType type)
    {
        var runningProcs = Process.GetProcesses();
        
        // Group by AppId (Path or PackageFamilyName) to avoid duplicates like mentioned (Discord, etc)
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
                // Store apps heuristic
                isRunning = runningProcs.Any(p => p.ProcessName.Contains(h.DisplayName, StringComparison.OrdinalIgnoreCase));
            }

            result.Add(new PrivacyAppViewModel
            {
                AppId = h.AppId,
                DisplayName = h.DisplayName,
                IsNonPackaged = h.IsNonPackaged,
                LastUsedStart = h.LastUsedStart,
                LastUsedStop = h.LastUsedStop,
                IsActiveRisk = h.IsActive,
                IsRunning = isRunning,
                DeviceType = type,
                OriginalData = h
            });
        }
        
        return result.OrderByDescending(x => x.IsActiveRisk).ThenByDescending(x => x.IsRunning).ThenByDescending(x => x.LastUsedStop).ToList();
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

    private void Kill_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PrivacyAppViewModel app)
        {
            try
            {
                KillProcessesForApp(app);
                RefreshData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al matar proceso: {ex.Message}");
            }
        }
    }

    private void KillProcessesForApp(PrivacyAppViewModel app)
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
            MessageBox.Show($"Se terminaron {killed} proceso(s) de '{app.DisplayName}'.");
        }
        else
        {
            MessageBox.Show("No se encontraron procesos activos para esta aplicación.");
        }
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is PrivacyAppViewModel app)
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
}
