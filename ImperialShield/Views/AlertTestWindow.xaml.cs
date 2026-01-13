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
            AlertWindow.Show("¡SISTEMA VULNERABLE!\n\nMicrosoft Defender está desactivado. Imperial Shield recomienda activarlo inmediatamente.");
        }

        private void TestHosts_Click(object sender, RoutedEventArgs e)
        {
            AlertWindow.Show("MODIFICACIÓN DETECTADA\n\nEl archivo HOSTS del sistema ha sido modificado recientemente. Esto podría indicar un intento de redirección de tráfico.");
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
