using System;
using System.Windows;
using System.Windows.Threading;

namespace ImperialShield.Views
{
    public partial class AlertWindow : Window
    {
        public AlertWindow(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
            
            // Posicionar en la esquina inferior derecha (estilo notificación)
            var desktopWorkingArea = SystemParameters.WorkArea;
            this.Left = desktopWorkingArea.Right - this.Width - 10;
            this.Top = desktopWorkingArea.Bottom - this.Height - 10;

            // Auto-cerrar después de 10 segundos
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            timer.Tick += (s, e) => { timer.Stop(); this.Close(); };
            timer.Start();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public static void Show(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var alert = new AlertWindow(message);
                alert.Show();
            });
        }
    }
}
