using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
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

            // 1. Registry - User
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (key != null)
                {
                    foreach (var name in key.GetValueNames())
                    {
                        items.Add(new StartupItem { 
                            Name = name, 
                            Command = key.GetValue(name)?.ToString() ?? "", 
                            Type = "Registro (U)",
                            Path = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run"
                        });
                    }
                }
            } catch { }

            // 2. Registry - Machine
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (key != null)
                {
                    foreach (var name in key.GetValueNames())
                    {
                        items.Add(new StartupItem { 
                            Name = name, 
                            Command = key.GetValue(name)?.ToString() ?? "", 
                            Type = "Registro (M)",
                            Path = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Run"
                        });
                    }
                }
            } catch { }

            // 3. Startup Folder - User
            try
            {
                var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (Directory.Exists(startupFolder))
                {
                    foreach (var file in Directory.GetFiles(startupFolder))
                    {
                        items.Add(new StartupItem { 
                            Name = Path.GetFileName(file), 
                            Command = file, 
                            Type = "Carpeta (U)",
                            Path = file
                        });
                    }
                }
            } catch { }

            // 4. Startup Folder - All Users (Common)
            try
            {
                var commonStartupFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                if (Directory.Exists(commonStartupFolder))
                {
                    foreach (var file in Directory.GetFiles(commonStartupFolder))
                    {
                        items.Add(new StartupItem { 
                            Name = Path.GetFileName(file), 
                            Command = file, 
                            Type = "Carpeta (S)",
                            Path = file
                        });
                    }
                }
            } catch { }

            StartupGrid.ItemsSource = items;
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is StartupItem item)
            {
                var result = MessageBox.Show($"¿Estás seguro de que deseas eliminar '{item.Name}' del inicio?\n\nEsta acción no se puede deshacer.", 
                    "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    bool success = false;
                    try
                    {
                        if (item.Type.Contains("Registro"))
                        {
                            var hive = item.Path.StartsWith("HKEY_CURRENT_USER") ? Registry.CurrentUser : Registry.LocalMachine;
                            var subKeyPath = item.Path.Substring(item.Path.IndexOf('\\') + 1);
                            using var key = hive.OpenSubKey(subKeyPath, true);
                            if (key != null)
                            {
                                key.DeleteValue(item.Name, false);
                                success = true;
                            }
                        }
                        else if (item.Type.Contains("Carpeta"))
                        {
                            if (File.Exists(item.Path))
                            {
                                File.Delete(item.Path);
                                success = true;
                            }
                        }

                        if (success)
                        {
                            Logger.Log($"User manually removed startup item: {item.Name} ({item.Type})");
                            LoadStartupItems();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al eliminar el item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadStartupItems();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class StartupItem
    {
        public required string Name { get; set; }
        public required string Command { get; set; }
        public required string Type { get; set; }
        public required string Path { get; set; }
    }
}
