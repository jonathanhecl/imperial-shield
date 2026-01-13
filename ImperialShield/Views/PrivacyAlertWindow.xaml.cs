using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ImperialShield.Services;

namespace ImperialShield.Views
{
    public enum PrivacyAlertResult
    {
        Ignore,
        Revoke
    }

    public partial class PrivacyAlertWindow : Window
    {
        private readonly string _appPath;
        public PrivacyAlertResult Result { get; private set; } = PrivacyAlertResult.Ignore;

        public PrivacyAlertWindow(PrivacyRisk risk)
        {
            InitializeComponent();
            _appPath = risk.ApplicationPath;

            DeviceIcon.Text = risk.Device == DeviceType.Camera ? "📷" : "🎤";
            AppNameText.Text = risk.ApplicationName;
            AppPathText.Text = risk.ApplicationPath;
            
            string deviceStr = risk.Device == DeviceType.Camera ? "cámara" : "micrófono";
            MessageText.Text = $"Se ha detectado una nueva aplicación intentando acceder a tu {deviceStr}. Imperial Shield recomienda bloquearla si no la reconoces.";

            // Sonido de alerta
            System.Media.SystemSounds.Exclamation.Play();
        }

        private void Revoke_Click(object sender, RoutedEventArgs e)
        {
            Result = PrivacyAlertResult.Revoke;
            this.Close();
        }

        private void OpenPath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(_appPath))
                {
                    Process.Start("explorer.exe", $"/select,\"{_appPath}\"");
                }
                else if (Directory.Exists(_appPath))
                {
                    Process.Start("explorer.exe", $"\"{_appPath}\"");
                }
                else
                {
                    MessageBox.Show("No se pudo encontrar la ubicación física del archivo.", "Aviso");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "PrivacyAlertWindow.OpenPath");
            }
        }

        private void Ignore_Click(object sender, RoutedEventArgs e)
        {
            Result = PrivacyAlertResult.Ignore;
            this.Close();
        }
    }
}
