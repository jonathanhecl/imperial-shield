using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ImperialShield.Services;

namespace ImperialShield.Views;

public partial class HostsAlertWindow : Window
{
    private const string HOSTS_PATH = @"C:\Windows\System32\drivers\etc\hosts";
    
    // Contenido predeterminado de Windows para el archivo HOSTS
    private const string DEFAULT_HOSTS_CONTENT = @"# Copyright (c) 1993-2009 Microsoft Corp.
#
# This is a sample HOSTS file used by Microsoft TCP/IP for Windows.
#
# This file contains the mappings of IP addresses to host names. Each
# entry should be kept on an individual line. The IP address should
# be placed in the first column followed by the corresponding host name.
# The IP address and the host name should be separated by at least one
# space.
#
# Additionally, comments (such as these) may be inserted on individual
# lines or following the machine name denoted by a '#' symbol.
#
# For example:
#
#      102.54.94.97     rhino.acme.com          # source server
#       38.25.63.10     x.acme.com              # x client host

# localhost name resolution is handled within DNS itself.
#	127.0.0.1       localhost
#	::1             localhost
";

    public HostsAlertWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        
        System.Media.SystemSounds.Hand.Play();

        // Verificar si tenemos un backup en memoria válido para restaurar
        // TODO: This logic assumes we can verify the backup quality. Currently we assume app.HostsMonitor has it.
        // We will update button content in Loaded event or here if possible.
        
        CheckBackupStatus();
    }

    private void CheckBackupStatus()
    {
        // Default text is "LIMPIAR ARCHIVO HOSTS" (Reset to default)
        
        var app = Application.Current as App;
        // Si hay un backup en memoria y es DIFERENTE al contenido actual (lo cual asumimos si saltó la alerta)
        // Y no está vacío.
        
        // Note: HostsFileMonitor stores the backup of the "Last Known Good" state.
        
        if (app?.HostsMonitor != null)
        {
            // Use Dispatcher to ensure UI thread access if called from bg
            Dispatcher.Invoke(() => 
            {
               // Change button text/tag based on logic
               // For now, simpler approach: The button calls "CleanHosts_Click". 
               // We will modify that method to ask the user.
               
               // But wait, the user wants two options: "Restore from Backup" (if avail) OR "Reset to Default" (if no backup)
               // Or maybe "Restore" is preferred.
               
               // Let's modify the UI button text to reflect what we can do.
               if (app.HostsMonitor.CanRestoreBackup) // We need to add this property or check
               {
                   CleanButtonText.Text = "RESTAURAR RESPALDO ANTERIOR";
                   CleanButtonIcon.Text = "⏪";
                   CleanButton.Tag = "RESTORE"; 
               }
               else
               {
                    CleanButtonText.Text = "LIMPIAR ARCHIVO MAPEO";
                    CleanButtonIcon.Text = "🧹";
                    CleanButton.Tag = "RESET";
               }
            });
        }
    }

    private void CleanHosts_Click(object sender, RoutedEventArgs e)
    {
        string mode = (CleanButton.Tag as string) ?? "RESET";

        if (mode == "RESTORE")
        {
            var result = MessageBox.Show(
                "¿Deseas restaurar el Mapeo de Dominios (HOSTS) a su estado anterior conocido?\n\n" +
                "Esto recuperará la configuración que tenías antes de la última modificación detectada.",
                "Restaurar Respaldo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var app = Application.Current as App;
                if (app?.HostsMonitor != null && app.HostsMonitor.RestoreBackup())
                {
                    MessageBox.Show("✅ Archivo restaurado correctamente desde la memoria.", "Éxito");
                    Close();
                }
                else
                {
                    MessageBox.Show("❌ No se pudo restaurar el respaldo. Se intentará limpiar a cero.", "Error");
                    // Fallback to reset logic
                    PerformReset();
                }
            }
        }
        else
        {
            PerformReset();
        }
    }

    private void PerformReset()
    {
        var result = MessageBox.Show(
            "¿Estás seguro de que deseas limpiar el Mapeo de Dominios (HOSTS) a cero?\n\n" +
            "⚠️ ADVERTENCIA: Esto eliminará TODAS las entradas personalizadas.\n" +
            "Se recomienda hacer un respaldo antes de continuar.",
            "Confirmar Limpieza",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                // Crear backup antes de modificar
                string backupPath = HOSTS_PATH + $".backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                File.Copy(HOSTS_PATH, backupPath, true);
                Logger.Log($"HOSTS backup created: {backupPath}");

                // Restaurar contenido predeterminado
                File.WriteAllText(HOSTS_PATH, DEFAULT_HOSTS_CONTENT);
                Logger.Log("HOSTS file restored to default");

                MessageBox.Show(
                    "✅ El archivo ha sido limpiado correctamente.\n\n" +
                    $"Se creó un respaldo en:\n{backupPath}",
                    "Limpieza Completada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                this.Close();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "❌ No se pudo modificar el archivo.\n\n" +
                    "Imperial Shield necesita ejecutarse como Administrador.",
                    "Permisos Insuficientes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "PerformReset");
                MessageBox.Show(
                    $"❌ Error al limpiar:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void OpenHosts_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Abre el archivo HOSTS en Notepad (o el editor de texto predeterminado)
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = HOSTS_PATH,
                UseShellExecute = true,
                Verb = "runas" // Solicitar elevación para poder guardar cambios
            });
            
            Logger.Log("Opened HOSTS file for editing");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "OpenHosts_Click");
            
            // Fallback: abrir en Explorer
            try
            {
                Process.Start("explorer.exe", $"/select,\"{HOSTS_PATH}\"");
            }
            catch
            {
                MessageBox.Show(
                    $"No se pudo abrir el archivo HOSTS.\n\nRuta: {HOSTS_PATH}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        // User chose to IGNORE the change - accept it as the new baseline
        var app = Application.Current as App;
        app?.HostsMonitor?.AcceptChange();
        
        this.Close();
    }

    public static void Show(string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var alert = new HostsAlertWindow(message);
            alert.Topmost = true;
            alert.ShowDialog();
        });
    }
}
