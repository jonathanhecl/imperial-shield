using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Input;
using ImperialShield.Services;

namespace ImperialShield.Views
{
    public partial class StartupManagerWindow : Window
    {
        private List<StartupItem> _allItems = new();

        public StartupManagerWindow()
        {
            InitializeComponent();
            LoadStartupItems();
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton clicked)
            {
                // Make filters mutually exclusive
                var filters = new[] { FilterAll, FilterRunning, FilterEnabled, FilterDisabled, FilterSuspicious };
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

        private void ApplyFilter()
        {
            if (_allItems == null || !_allItems.Any()) return;

            IEnumerable<StartupItem> filtered = _allItems;

            // Apply exclusive filter
            if (FilterRunning.IsChecked == true)
            {
                filtered = filtered.Where(i => i.IsRunning);
            }
            else if (FilterEnabled.IsChecked == true)
            {
                filtered = filtered.Where(i => i.IsEnabled);
            }
            else if (FilterDisabled.IsChecked == true)
            {
                filtered = filtered.Where(i => !i.IsEnabled);
            }
            else if (FilterSuspicious.IsChecked == true)
            {
                filtered = filtered.Where(i => i.ThreatLevel >= StartupThreatLevel.Medium);
            }
            // FilterAll shows everything

            // Sort: Running > Suspicious > Name
            var sorted = filtered
                .OrderByDescending(i => i.IsRunning)
                .ThenByDescending(i => i.ThreatLevel)
                .ThenBy(i => i.Name)
                .ToList();

            StartupGrid.ItemsSource = sorted;
            StatsText.Text = $"{sorted.Count} apps";
        }

        private void LoadStartupItems()
        {
            var items = new List<StartupItem>();

            // --- Lógica del Registro ---
            void ScanRegistry(RegistryKey hive, string subKeyPath, string typeName)
            {
                try
                {
                    using var key = hive.OpenSubKey(subKeyPath);
                    if (key != null)
                    {
                        foreach (var name in key.GetValueNames())
                        {
                            var command = key.GetValue(name)?.ToString() ?? "";
                            var item = new StartupItem { 
                                Name = name, 
                                Command = command, 
                                Type = typeName,
                                RegistryPath = $@"{hive.Name}\{subKeyPath}",
                                IsEnabled = true,
                                Origin = StartupOrigin.Registry
                            };
                            AnalyzeItem(item);
                            items.Add(item);
                        }
                    }

                    // Buscar deshabilitados
                    using var disabledKey = hive.OpenSubKey(subKeyPath + @"\ImperialShield_Disabled");
                    if (disabledKey != null)
                    {
                        foreach (var name in disabledKey.GetValueNames())
                        {
                            var command = disabledKey.GetValue(name)?.ToString() ?? "";
                            var item = new StartupItem { 
                                Name = name, 
                                Command = command, 
                                Type = typeName,
                                RegistryPath = $@"{hive.Name}\{subKeyPath}",
                                IsEnabled = false,
                                Origin = StartupOrigin.Registry
                            };
                            AnalyzeItem(item);
                            items.Add(item);
                        }
                    }
                }
                catch { }
            }

            ScanRegistry(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Registro (Usuario)");
            ScanRegistry(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "Registro (Sistema)");

            // --- Lógica de Carpetas ---
            void ScanFolder(string folderPath, string typeName)
            {
                try
                {
                    if (Directory.Exists(folderPath))
                    {
                        // Activos
                        foreach (var file in Directory.GetFiles(folderPath))
                        {
                            if (Path.GetExtension(file).ToLower() == ".disabled") continue;
                            
                            var item = new StartupItem { 
                                Name = Path.GetFileNameWithoutExtension(file), 
                                Command = file, 
                                Type = typeName,
                                FilePath = file,
                                IsEnabled = true,
                                Origin = StartupOrigin.Folder
                            };
                            AnalyzeItem(item);
                            items.Add(item);
                        }

                        // Deshabilitados (.disabled)
                        foreach (var file in Directory.GetFiles(folderPath, "*.disabled"))
                        {
                            var item = new StartupItem { 
                                Name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file)),
                                Command = file, 
                                Type = typeName,
                                FilePath = file,
                                IsEnabled = false,
                                Origin = StartupOrigin.Folder
                            };
                            AnalyzeItem(item);
                            items.Add(item);
                        }
                    }
                }
                catch { }
            }

            ScanFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Carpeta (Usuario)");
            ScanFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Carpeta (Sistema)");

            // Ordenar: Ejecutándose primero, luego sospechosos, luego nombre
            StartupGrid.ItemsSource = items
                .OrderByDescending(i => i.IsRunning)
                .ThenByDescending(i => i.ThreatLevel)
                .ThenBy(i => i.Name)
                .ToList();
            
            var suspicious = items.Count(i => i.ThreatLevel >= StartupThreatLevel.Medium);
            AppCountText.Text = $"{items.Count} apps";
            SuspiciousCountText.Text = suspicious > 0 ? $"⚠️ {suspicious} sospechosas" : "✅ Todo seguro";
            SuspiciousCountText.Foreground = suspicious > 0 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")) 
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            
            LastUpdateText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
        }

        private void AnalyzeItem(StartupItem item)
        {
            item.ExecutablePath = ExtractExecutablePath(item.Command);
            item.IsRunning = IsProcessRunning(item.ExecutablePath);
            item.IsSigned = VerifySignature(item.ExecutablePath);
            item.Publisher = GetPublisher(item.ExecutablePath);
            
            // Caso especial Imperial Shield
            if (item.Name.Contains("ImperialShield", StringComparison.OrdinalIgnoreCase) ||
                item.ExecutablePath.Contains("ImperialShield", StringComparison.OrdinalIgnoreCase))
            {
                item.IsImperialShield = true;
                item.IsSigned = true; // Forzar para lógica visual
                item.Publisher = "Imperial Shield (Verificado)";
                item.ThreatLevel = StartupThreatLevel.Trusted;
            }
            else
            {
                item.ThreatLevel = AnalyzeThreat(item);
            }
        }

        private string ExtractExecutablePath(string command)
        {
            if (string.IsNullOrEmpty(command)) return "";
            var cmd = command.Trim();
            if (cmd.StartsWith("\""))
            {
                var endQuote = cmd.IndexOf('"', 1);
                if (endQuote > 1) return cmd.Substring(1, endQuote - 1);
            }
            var spaceIndex = cmd.IndexOf(' ');
            if (spaceIndex > 0) return cmd.Substring(0, spaceIndex);
            return cmd;
        }

        private bool IsProcessRunning(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return false;
            try
            {
                var exeName = Path.GetFileNameWithoutExtension(exePath);
                return Process.GetProcessesByName(exeName).Length > 0;
            }
            catch { return false; }
        }

        private bool VerifySignature(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;
            try
            {
                var cert = X509Certificate.CreateFromSignedFile(filePath);
                return cert != null;
            }
            catch { return false; }
        }

        private string GetPublisher(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return "Desconocido";
            try
            {
                var cert = X509Certificate.CreateFromSignedFile(filePath);
                if (cert != null)
                {
                    var subject = cert.Subject;
                    var cnStart = subject.IndexOf("CN=", StringComparison.OrdinalIgnoreCase);
                    if (cnStart >= 0)
                    {
                        cnStart += 3;
                        var cnEnd = subject.IndexOf(',', cnStart);
                        if (cnEnd < 0) cnEnd = subject.Length;
                        return subject.Substring(cnStart, cnEnd - cnStart).Trim('"');
                    }
                    return cert.GetName();
                }
            }
            catch { }
            return "Sin firma";
        }

        private StartupThreatLevel AnalyzeThreat(StartupItem item)
        {
            if (item.IsImperialShield) return StartupThreatLevel.Trusted;
            
            if (!item.IsSigned)
            {
                var susfolders = new[] { "temp", "appdata\\local\\temp", "downloads", "desktop" };
                if (susfolders.Any(f => item.ExecutablePath.ToLower().Contains(f)))
                    return StartupThreatLevel.High;
                return StartupThreatLevel.Medium;
            }
            
            var trustedPublishers = new[] { "Microsoft", "Google", "Mozilla", "Adobe", "NVIDIA", "Intel", "AMD", "Realtek", "Logitech" };
            if (trustedPublishers.Any(p => item.Publisher.Contains(p, StringComparison.OrdinalIgnoreCase)))
                return StartupThreatLevel.Trusted;
            
            return StartupThreatLevel.Safe;
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is StartupItem item)
            {
                try
                {
                    if (item.IsEnabled) DisableItem(item);
                    else EnableItem(item);
                    LoadStartupItems();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al cambiar estado: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            HandleOpenLocation(sender);
        }

        private void OpenLocation_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            HandleOpenLocation(sender);
        }

        private void HandleOpenLocation(object sender)
        {
            if (sender is FrameworkElement element && element.DataContext is StartupItem item)
            {
                try
                {
                    var path = item.ExecutablePath;
                    if (File.Exists(path))
                    {
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                    else if (Directory.Exists(Path.GetDirectoryName(path)))
                    {
                        Process.Start("explorer.exe", Path.GetDirectoryName(path)!);
                    }
                    else
                    {
                        MessageBox.Show("No se encontró la ubicación del archivo.", "Imperial Shield", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al abrir ubicación: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void KillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is StartupItem item)
            {
                if (!item.IsRunning) return;
                
                var result = MessageBox.Show(
                    $"¿Deseas terminar el proceso de '{item.Name}'?\n\nEsto cerrará la aplicación inmediatamente.",
                    "Confirmar Terminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var exeName = Path.GetFileNameWithoutExtension(item.ExecutablePath);
                        foreach (var proc in Process.GetProcessesByName(exeName))
                        {
                            proc.Kill();
                        }
                        LoadStartupItems();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al terminar proceso: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DisableItem(StartupItem item)
        {
            if (item.Origin == StartupOrigin.Registry)
            {
                var hive = item.RegistryPath.StartsWith("HKEY_CURRENT_USER") ? Registry.CurrentUser : Registry.LocalMachine;
                var subKeyPath = item.RegistryPath.Substring(item.RegistryPath.IndexOf('\\') + 1);
                using var currentKey = hive.OpenSubKey(subKeyPath, true);
                using var disabledKey = hive.CreateSubKey(subKeyPath + @"\ImperialShield_Disabled", true);
                var value = currentKey?.GetValue(item.Name);
                if (value != null)
                {
                    disabledKey?.SetValue(item.Name, value);
                    currentKey?.DeleteValue(item.Name);
                }
            }
            else
            {
                var newPath = item.FilePath + ".disabled";
                if (File.Exists(item.FilePath)) File.Move(item.FilePath, newPath);
            }
        }

        private void EnableItem(StartupItem item)
        {
            if (item.Origin == StartupOrigin.Registry)
            {
                var hive = item.RegistryPath.StartsWith("HKEY_CURRENT_USER") ? Registry.CurrentUser : Registry.LocalMachine;
                var subKeyPath = item.RegistryPath.Substring(item.RegistryPath.IndexOf('\\') + 1);
                using var currentKey = hive.OpenSubKey(subKeyPath, true);
                using var disabledKey = hive.OpenSubKey(subKeyPath + @"\ImperialShield_Disabled", true);
                var value = disabledKey?.GetValue(item.Name);
                if (value != null)
                {
                    currentKey?.SetValue(item.Name, value);
                    disabledKey?.DeleteValue(item.Name);
                }
            }
            else
            {
                if (item.FilePath.EndsWith(".disabled"))
                {
                    var newPath = item.FilePath.Substring(0, item.FilePath.Length - 9);
                    if (File.Exists(item.FilePath)) File.Move(item.FilePath, newPath);
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadStartupItems();
        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
    }

    public enum StartupOrigin { Registry, Folder }
    public enum StartupThreatLevel { Trusted = 0, Safe = 1, Medium = 2, High = 3 }

    public class StartupItem
    {
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public string Type { get; set; } = "";
        public string RegistryPath { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public bool IsEnabled { get; set; }
        public bool IsRunning { get; set; }
        public bool IsSigned { get; set; }
        public bool IsImperialShield { get; set; }
        public string Publisher { get; set; } = "";
        public StartupOrigin Origin { get; set; }
        public StartupThreatLevel ThreatLevel { get; set; }

        public string StatusText => IsEnabled ? (IsRunning ? "🟢 Ejecutándose" : "✅ Activo") : "⏸️ Deshabilitado";
        public Brush StatusColor => IsEnabled 
            ? (IsRunning ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")) 
                         : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DA8DA")))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
        
        public string ActionText => IsEnabled ? "Deshabilitar" : "Habilitar";
        public Brush ActionColor => IsEnabled 
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")) 
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));

        public string ThreatIcon => ThreatLevel switch
        {
            StartupThreatLevel.Trusted => "🛡️",
            StartupThreatLevel.Safe => "✅",
            StartupThreatLevel.Medium => "🟡",
            StartupThreatLevel.High => "🔴",
            _ => "⚪"
        };

        public string ThreatText => ThreatLevel switch
        {
            StartupThreatLevel.Trusted => "Verificado",
            StartupThreatLevel.Safe => "Seguro",
            StartupThreatLevel.Medium => "Sin firma",
            StartupThreatLevel.High => "¡Sospechoso!",
            _ => ""
        };

        public Brush ThreatColor => ThreatLevel switch
        {
            StartupThreatLevel.Trusted => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DA8DA")),
            StartupThreatLevel.Safe => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
            StartupThreatLevel.Medium => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")),
            StartupThreatLevel.High => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"))
        };

        public string SignatureText {
            get {
                if (IsImperialShield) return "🛡️ Sistema (Verificado)";
                return IsSigned ? $"✅ {Publisher}" : "❌ No firmado";
            }
        }
        public Brush SignatureColor => IsImperialShield ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DA8DA")) : (IsSigned 
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")) 
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")));
    }
}
