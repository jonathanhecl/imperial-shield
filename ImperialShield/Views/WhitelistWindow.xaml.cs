using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ImperialShield.Services;

namespace ImperialShield.Views;

public partial class WhitelistWindow : Window
{
    public WhitelistWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshWhitelist();
    }

    private void RefreshWhitelist()
    {
        try
        {
            var apps = SettingsManager.Current.WhitelistedNetworkApps ?? new List<string>();
            var items = apps.Select(name => new WhitelistedAppItem { Name = name }).ToList();
            WhitelistGrid.ItemsSource = items;
            
            EmptyListText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "WhitelistWindow.RefreshWhitelist");
        }
    }

    private void BrowseExe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar aplicación para excluir del monitoreo",
            Filter = "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            NewExeTextBox.Text = dialog.FileName;
        }
    }

    private void AddWhitelist_Click(object sender, RoutedEventArgs e)
    {
        string exePath = NewExeTextBox.Text.Trim();
        
        if (string.IsNullOrEmpty(exePath))
        {
            MessageBox.Show("Ingresa la ruta completa de un ejecutable o búscalo usando el explorador.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Validar si la ruta es absoluta/enraizada
        if (!System.IO.Path.IsPathRooted(exePath))
        {
            MessageBox.Show(
                "Debes ingresar la ruta completa del archivo ejecutable (ej: C:\\Program Files\\App\\app.exe).\n\n" +
                "Para mayor seguridad, la lista blanca solo acepta rutas absolutas.",
                "Ruta no Válida",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Añadir .exe si no lo tiene
        if (!exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            exePath += ".exe";
        }

        if (SettingsManager.Current.WhitelistedNetworkApps.Contains(exePath, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show($"'{exePath}' ya se encuentra en la lista blanca.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"¿Deseas añadir '{exePath}' a la lista blanca?\n\n" +
            "Se omitirá el monitoreo de tráfico anómalo únicamente para esta ruta específica.",
            "Confirmar Exclusión",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            try
            {
                SettingsManager.Current.WhitelistedNetworkApps.Add(exePath);
                SettingsManager.Save();
                
                MessageBox.Show(
                    $"✅ '{exePath}' ha sido añadida a la lista blanca.",
                    "Operación Completada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                NewExeTextBox.Text = "";
                RefreshWhitelist();
                NotifyDashboardRefresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar en la lista blanca: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void RemoveWhitelist_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string fileName)
        {
            var confirm = MessageBox.Show(
                $"¿Deseas remover '{fileName}' de la lista blanca?\n\n" +
                "El tráfico de esta aplicación volverá a ser analizado de forma normal.",
                "Confirmar Eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    SettingsManager.Current.WhitelistedNetworkApps.RemoveAll(x => x.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                    SettingsManager.Save();

                    MessageBox.Show(
                        $"'{fileName}' ha sido removido de la lista blanca.",
                        "Operación Completada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    RefreshWhitelist();
                    NotifyDashboardRefresh();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al remover de la lista blanca: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void NotifyDashboardRefresh()
    {
        try
        {
            var app = App.CurrentApp;
            var dashboard = app.DashboardWindow;
            
            if (dashboard != null && dashboard.IsLoaded)
            {
                dashboard.Dispatcher.InvokeAsync(async () => 
                {
                    await dashboard.ForceRefreshAsync();
                });
            }
        }
        catch { }
    }
}

public class WhitelistedAppItem
{
    public string Name { get; set; } = string.Empty;
}
