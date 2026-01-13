using System;
using System.Windows;
using ImperialShield.Services;

namespace ImperialShield.Views
{
    public partial class AlertTestWindow : Window
    {
        public AlertTestWindow()
        {
            InitializeComponent();
        }

        private void TestPrivacyCamera_Click(object sender, RoutedEventArgs e)
        {
            var demoRisk = new PrivacyRisk(
                @"C:\Program Files\Common Files\System\RuntimeCamera.exe", 
                DeviceType.Camera, 
                true
            );
            var alert = new PrivacyAlertWindow(demoRisk);
            alert.ShowDialog();
        }

        private void TestPrivacyMic_Click(object sender, RoutedEventArgs e)
        {
            var demoRisk = new PrivacyRisk(
                @"C:\Users\User\AppData\Local\Discord\app-1.0.9001\Discord.exe", 
                DeviceType.Microphone, 
                true
            );
            var alert = new PrivacyAlertWindow(demoRisk);
            alert.ShowDialog();
        }

        private void TestDDoS_Click(object sender, RoutedEventArgs e)
        {
            DDoSTrackerWindow.ShowAlert("Flooder.exe", "185.12.33.190", 1240, 
                "DETECCIÓN CRÍTICA: Se ha detectado un ataque de denegación de servicio saliente.");
        }

        private void TestBlocked_Click(object sender, RoutedEventArgs e)
        {
            BlockedExecutionWindow.ShowBlocked("malware_test_sample.exe");
        }

        private void TestSecurityWarning_Click(object sender, RoutedEventArgs e)
        {
            SecurityWarningWindow.ShowWarning("chrome.exe", @"C:\Users\Public\Documents\dropper.exe");
        }

        private void TestDefenderOff_Click(object sender, RoutedEventArgs e)
        {
            DefenderAlertWindow.Show(
                "La Protección en Tiempo Real de Windows Defender ha sido DESACTIVADA.\n\n" +
                "Tu equipo se encuentra vulnerable a virus, malware y ransomware.\n\n" +
                "Se recomienda encarecidamente reactivar la protección inmediatamente.");
        }

        private void TestHosts_Click(object sender, RoutedEventArgs e)
        {
            HostsAlertWindow.Show(
                "Se han detectado cambios en el archivo HOSTS del sistema.\n\n" +
                "Nuevas entradas añadidas:\n• 127.0.0.1 → windowsupdate.microsoft.com\n• 127.0.0.1 → update.microsoft.com\n\n" +
                "El archivo HOSTS puede ser usado por malware para redirigir sitios web legítimos a páginas falsas.");
        }

        private void TestNewTask_Click(object sender, RoutedEventArgs e)
        {
            var alert = new NewTaskAlertWindow(
                "SilentCryptoMinerUpdater", 
                @"\Microsoft\Windows\SystemMaintenance\Updater"
            );
            alert.ShowDialog();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
