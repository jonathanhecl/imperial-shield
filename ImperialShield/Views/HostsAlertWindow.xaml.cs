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
    }

    private void CleanHosts_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "¿Estás seguro de que deseas restaurar el archivo HOSTS a su estado predeterminado?\n\n" +
            "⚠️ ADVERTENCIA: Esto eliminará TODAS las entradas personalizadas que hayas añadido " +
            "(como bloqueos de publicidad, redirecciones locales, etc.).\n\n" +
            "Se recomienda hacer un respaldo antes de continuar.",
            "Confirmar Limpieza de HOSTS",
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
                    "✅ El archivo HOSTS ha sido restaurado correctamente.\n\n" +
                    $"Se creó un respaldo en:\n{backupPath}",
                    "Limpieza Completada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                this.Close();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "❌ No se pudo modificar el archivo HOSTS.\n\n" +
                    "Imperial Shield necesita ejecutarse como Administrador para realizar esta acción.",
                    "Permisos Insuficientes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "CleanHosts_Click");
                MessageBox.Show(
                    $"❌ Error al limpiar el archivo HOSTS:\n\n{ex.Message}",
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
