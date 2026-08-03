using System;
using System.Windows;
using ImperialShield.Services;

namespace ImperialShield.Views
{
    public partial class AlertWindow : Window
    {
        public AlertWindow(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
            
            // Sonido de parada crítica (más impactante que la exclamación)
            System.Media.SystemSounds.Hand.Play();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public static void Show(string message)
        {
            AlertManager.ShowAlert(AlertType.Exclusion, () => new AlertWindow(message));
        }
    }
}
