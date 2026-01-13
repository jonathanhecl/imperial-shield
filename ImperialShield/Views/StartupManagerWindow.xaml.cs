using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using ImperialShield.Services;

namespace ImperialShield.Views
{
    public partial class StartupManagerWindow : Window
    {
        public StartupManagerWindow()
        {
            InitializeComponent();
            LoadStartupItems();
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
                            items.Add(new StartupItem { 
                                Name = name, 
                                Command = key.GetValue(name)?.ToString() ?? "", 
                                Type = typeName,
                                Path = $@"{hive.Name}\{subKeyPath}",
                                IsEnabled = true,
                                Origin = StartupOrigin.Registry
                            });
                        }
                    }

                    // Buscar deshabilitados (nuestra convención: subclave 'Disabled')
                    using var disabledKey = hive.OpenSubKey(subKeyPath + @"\ImperialShield_Disabled");
                    if (disabledKey != null)
                    {
                        foreach (var name in disabledKey.GetValueNames())
                        {
                            items.Add(new StartupItem { 
                                Name = name, 
                                Command = disabledKey.GetValue(name)?.ToString() ?? "", 
                                Type = typeName,
                                Path = $@"{hive.Name}\{subKeyPath}",
                                IsEnabled = false,
                                Origin = StartupOrigin.Registry
                            });
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
                            
                            items.Add(new StartupItem { 
                                Name = Path.GetFileNameWithoutExtension(file), 
                                Command = file, 
                                Type = typeName,
                                Path = file,
                                IsEnabled = true,
                                Origin = StartupOrigin.Folder
                            });
                        }

                        // Deshabilitados (.disabled)
                        foreach (var file in Directory.GetFiles(folderPath, "*.disabled"))
                        {
                            items.Add(new StartupItem { 
                                Name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file)), // Quitar .disabled y luego la extensión original
                                Command = file, 
                                Type = typeName,
                                Path = file,
                                IsEnabled = false,
                                Origin = StartupOrigin.Folder
                            });
                        }
                    }
                }
                catch { }
            }

            ScanFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Carpeta (Usuario)");
            ScanFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Carpeta (Sistema)");

            StartupGrid.ItemsSource = items.OrderBy(i => i.Name).ToList();
            AppCountText.Text = $"{items.Count} apps";
            LastUpdatedText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is StartupItem item)
            {
                try
                {
                    if (item.IsEnabled)
                    {
                        // DESHABILITAR
                        DisableItem(item);
                    }
                    else
                    {
                        // HABILITAR
                        EnableItem(item);
                    }
                    LoadStartupItems(); // Recargar todo
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al cambiar estado: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DisableItem(StartupItem item)
        {
            if (item.Origin == StartupOrigin.Registry)
            {
                // Mover a subclave 'ImperialShield_Disabled'
                var hive = item.Path.StartsWith("HKEY_CURRENT_USER") ? Registry.CurrentUser : Registry.LocalMachine;
                var subKeyPath = item.Path.Substring(item.Path.IndexOf('\\') + 1);
                
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
                // Renombrar archivo a .disabled
                var newPath = item.Path + ".disabled";
                if (File.Exists(item.Path)) File.Move(item.Path, newPath);
            }
        }

        private void EnableItem(StartupItem item)
        {
             if (item.Origin == StartupOrigin.Registry)
            {
                // Mover desde subclave 'ImperialShield_Disabled' a raíz
                var hive = item.Path.StartsWith("HKEY_CURRENT_USER") ? Registry.CurrentUser : Registry.LocalMachine;
                var subKeyPath = item.Path.Substring(item.Path.IndexOf('\\') + 1);
                
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
                // Quitar .disabled
                if (item.Path.EndsWith(".disabled"))
                {
                    var newPath = item.Path.Substring(0, item.Path.Length - 9); // Remove .disabled
                    if (File.Exists(item.Path)) File.Move(item.Path, newPath);
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadStartupItems();
        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
    }

    public enum StartupOrigin { Registry, Folder }

    public class StartupItem
    {
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public string Type { get; set; } = "";
        public string Path { get; set; } = "";
        public bool IsEnabled { get; set; }
        public StartupOrigin Origin { get; set; }


        // Propiedades Visuales (MVVM simple)
        public string Status => IsEnabled ? "Activo" : "Deshabilitado";
        public Brush StatusColor => IsEnabled ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
        
        public string ActionText => IsEnabled ? "Deshabilitar" : "Habilitar";
        public Brush ActionColor => IsEnabled ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
    }
}
