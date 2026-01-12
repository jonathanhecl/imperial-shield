using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ImperialShield.Services;

namespace ImperialShield.Views
{
    public partial class SettingsWindow : Window
    {
        public bool SettingsChanged { get; private set; }

        public SettingsWindow()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            var currentInterval = SettingsManager.Current.PollingIntervalMs.ToString();
            foreach (ComboBoxItem item in IntervalComboBox.Items)
            {
                if (item.Tag.ToString() == currentInterval)
                {
                    IntervalComboBox.SelectedItem = item;
                    break;
                }
            }

            StartupCheckBox.IsChecked = StartupManager.IsStartupEnabled();
        }

        private void ViewStartupApps_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Obtener apps de inicio vía PowerShell para mayor control
                var script = "Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run', 'HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' -ErrorAction SilentlyContinue | Select-Object -Property PSChildName, (Get-ItemProperty $_.PSPath) | Format-List";
                
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' | Get-Member -MemberType NoteProperty | ForEach-Object { Write-Output \\\"$($_.Name): $((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run').$($_.Name))\\\" }; Get-ItemProperty 'HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' | Get-Member -MemberType NoteProperty | ForEach-Object { Write-Output \\\"$($_.Name): $((Get-ItemProperty 'HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run').$($_.Name))\\\" }\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                var output = process?.StandardOutput.ReadToEnd() ?? "Error al obtener lista.";
                process?.WaitForExit();

                MessageBox.Show(string.IsNullOrWhiteSpace(output) ? "No se encontraron aplicaciones en el registro de inicio." : output, 
                    "Aplicaciones de Inicio (Registro)", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "ViewStartupApps");
            }
        }

        private void TestAlert_Click(object sender, RoutedEventArgs e)
        {
            AlertWindow.Show("¡Prueba de Seguridad! Se ha detectado un cambio simulado en el sistema.");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (IntervalComboBox.SelectedItem is ComboBoxItem selected)
            {
                SettingsManager.Current.PollingIntervalMs = int.Parse(selected.Tag.ToString()!);
                SettingsManager.Save();
                
                // Aplicar cambio de inicio
                bool wantStartup = StartupCheckBox.IsChecked ?? false;
                bool isEnabled = StartupManager.IsStartupEnabled();
                
                if (wantStartup != isEnabled)
                {
                    StartupManager.ToggleStartup();
                }

                SettingsChanged = true;
                this.Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
