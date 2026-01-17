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
        Revoke,
        KillOnly,
        RevokeAndKill
    }

    public partial class PrivacyAlertWindow : Window
    {
        private readonly string _appPath;
        public PrivacyAlertResult Result { get; private set; } = PrivacyAlertResult.Ignore;

        public PrivacyAlertWindow(PrivacyRisk risk)
        {
            InitializeComponent();
            _appPath = risk.ApplicationPath;

            AppNameText.Text = risk.ApplicationName;
            AppNameText.ToolTip = risk.ApplicationPath;
            
            string deviceStr = risk.Device == DeviceType.Camera ? "cámara" : "micrófono";
            MessageText.Text = $"Se ha detectado una nueva aplicación intentando acceder a tu {deviceStr}. Imperial Shield recomienda bloquearla si no la reconoces.";

            // Sonido de alerta
            System.Media.SystemSounds.Exclamation.Play();
        }

        private void Revoke_Click(object sender, RoutedEventArgs e)
        {
            Result = PrivacyAlertResult.RevokeAndKill;
            KillProcess();
            this.Close();
        }

        private void KillOnly_Click(object sender, RoutedEventArgs e)
        {
            Result = PrivacyAlertResult.KillOnly;
            KillProcess();
            this.Close();
        }

        private void KillProcess()
        {
            try
            {
                string processName = Path.GetFileNameWithoutExtension(_appPath);
                var processes = Process.GetProcessesByName(processName);
                foreach (var p in processes)
                {
                    try { p.Kill(); } catch { }
                }
                Logger.Log($"Privacy: Killed process {processName}");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "PrivacyAlertWindow.KillProcess");
            }
        }

        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            OpenFileLocation();
        }

        private void OpenFileLocation()
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
