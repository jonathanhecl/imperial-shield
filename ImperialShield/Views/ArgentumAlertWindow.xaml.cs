using System;
using System.Windows;
using ImperialShield.Services;

namespace ImperialShield.Views;

public partial class ArgentumAlertWindow : Window
{
    private readonly string _launcherPath;
    private readonly string _subprocessPath;

    public ArgentumAlertWindow(string launcherName, string launcherPath, string subprocessName, string subprocessPath, string details)
    {
        InitializeComponent();
        
        _launcherPath = launcherPath;
        _subprocessPath = subprocessPath;

        LauncherNameText.Text = launcherName;
        LauncherPathText.Text = launcherPath;
        SubprocessNameText.Text = subprocessName;
        SubprocessPathText.Text = subprocessPath;
        DetailsText.Text = details;

        // Sonido de alerta crítica
        System.Media.SystemSounds.Hand.Play();
        
        Logger.Log($"ArgentumAlertWindow shown: Launcher={launcherName}, Subprocess={subprocessName}");
    }

    private void KeepBlocked_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void WhitelistLauncher_Click(object sender, RoutedEventArgs e)
    {
        AddToWhitelist(_launcherPath, "Launcher");
    }

    private void WhitelistSubprocess_Click(object sender, RoutedEventArgs e)
    {
        AddToWhitelist(_subprocessPath, "Subproceso");
    }

    private void AddToWhitelist(string path, string typeName)
    {
        if (string.IsNullOrEmpty(path))
        {
            MessageBox.Show("Ruta no válida.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var confirm = MessageBox.Show(
            $"¿Deseas excluir este {typeName.ToLowerInvariant()} de las reglas de seguridad?\n\n" +
            $"Ruta: {path}\n\n" +
            "Se permitirá su ejecución y tráfico de red en el futuro sin generar alertas.",
            $"Confirmar Exclusión de {typeName}",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            try
            {
                if (!SettingsManager.Current.WhitelistedNetworkApps.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    SettingsManager.Current.WhitelistedNetworkApps.Add(path);
                    SettingsManager.Save();
                }

                MessageBox.Show(
                    $"✅ El {typeName.ToLowerInvariant()} ha sido añadido a la lista blanca.",
                    "Operación Completada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Notificar al Dashboard para refrescar si está abierto
                NotifyDashboardRefresh();
                
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar en la lista blanca: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
