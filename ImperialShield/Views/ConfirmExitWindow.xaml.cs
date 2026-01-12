using System.Windows;

namespace ImperialShield.Views
{
    public partial class ConfirmExitWindow : Window
    {
        public bool Confirmed { get; private set; }

        public ConfirmExitWindow()
        {
            InitializeComponent();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            this.Close();
        }
    }
}
