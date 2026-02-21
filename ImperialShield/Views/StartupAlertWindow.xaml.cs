using System;
using System.Windows;
using Microsoft.Win32;

namespace ImperialShield.Views
{
    public partial class StartupAlertWindow : Window
    {
        private string _appName;
        
        public StartupAlertWindow(string appName)
        {
            InitializeComponent();
            _appName = appName;
            
            // Format parsing logic tailored to how StartupMonitor sends data: e.g. "OneDrive (User)" or "Update.exe (System)"
            // Usually the input is "Name (Source)"
            AppNameText.Text = appName;
            
            // Clean up display name if possible
            string cleanName = appName;
            string executablePath = "No disponible";

            if (appName.EndsWith(" (User)"))
            {
                cleanName = appName.Replace(" (User)", "");
                AppNameText.Text = cleanName;
                LocationText.Text = "Registro de Usuario (HKCU)";
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                    var value = key?.GetValue(cleanName);
                    if (value != null) executablePath = value.ToString();
                }
                catch { }
            }
            else if (appName.EndsWith(" (System)"))
            {
                cleanName = appName.Replace(" (System)", "");
                AppNameText.Text = cleanName;
                LocationText.Text = "Registro del Sistema (HKLM)";
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                    var value = key?.GetValue(cleanName);
                    if (value != null) executablePath = value.ToString();
                }
                catch { }
            }

            PathText.Text = executablePath;
            
            System.Media.SystemSounds.Hand.Play();
        }

        private void Disable_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DisableStartupApp(_appName);
                
                // Show success toast (using a temporary simpler method or delegate could be cleaner, 
                // but since this is a separate window we'll just close it and let the main app notify or assume success)
                // Integrating properly with Toast notifications requires reference to App, which is fine.
                
                // Close immediately on success
                this.Close();
                
                // Notify user via Toast
                // We'll invoke this on the main dispatcher to be safe
                Application.Current.Dispatcher.Invoke(() => 
                {
                   // Assuming App.xaml.cs has a public method or we can just ignore visual confirmation beyond the window closing
                   // For now, let's just close. The user sees the button action worked because the window goes away.
                   MessageBox.Show("Aplicación deshabilitada del inicio correctamente.", "Imperial Shield", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al deshabilitar la aplicación: {ex.Message}\n\nAsegúrate de tener permisos de administrador.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Ignore_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void DisableStartupApp(string fullIdentifier)
        {
            string cleanName = fullIdentifier;
            bool isSystem = false;
            
            if (fullIdentifier.EndsWith(" (User)"))
            {
                cleanName = fullIdentifier.Substring(0, fullIdentifier.LastIndexOf(" (User)"));
                isSystem = false;
            }
            else if (fullIdentifier.EndsWith(" (System)"))
            {
                cleanName = fullIdentifier.Substring(0, fullIdentifier.LastIndexOf(" (System)"));
                isSystem = true;
            }

            if (isSystem)
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    if (key.GetValue(cleanName) != null)
                        key.DeleteValue(cleanName);
                    else
                        throw new Exception("No se encontró la clave en HKLM.");
                }
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    if (key.GetValue(cleanName) != null)
                        key.DeleteValue(cleanName);
                    else
                        throw new Exception("No se encontró la clave en HKCU.");
                }
            }
        }
    }
}
