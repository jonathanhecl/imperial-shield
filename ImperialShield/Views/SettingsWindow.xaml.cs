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

        private void TestAlert_Click(object sender, RoutedEventArgs e)
        {
            var demoRisk = new PrivacyRisk(
                @"C:\Program Files\AppEspía\bin\AppEspía.exe", 
                DeviceType.Camera, 
                true
            );
            var alert = new PrivacyAlertWindow(demoRisk);
            alert.ShowDialog();
        }

        private void TestDDoS_Click(object sender, RoutedEventArgs e)
        {
            DDoSTrackerWindow.ShowAlert("TestDDoS.exe", "203.0.113.55", 666, 
                "ATAQUE SALIENTE SIMULADO: 'TestDDoS.exe' está enviando tráfico masivo a 203.0.113.55");
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
