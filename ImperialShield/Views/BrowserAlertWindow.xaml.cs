using System.Windows;

namespace ImperialShield.Views;

public partial class BrowserAlertWindow : Window
{
    private readonly bool _isDemoMode;

    public BrowserAlertWindow(string oldBrowser, string newBrowser, string newBrowserId, bool demoMode = false)
    {
        InitializeComponent();
        _isDemoMode = demoMode;
        
        OldBrowserText.Text = oldBrowser;
        NewBrowserText.Text = newBrowser;
        NewBrowserIdText.Text = newBrowserId;
        RestoreBtn.IsEnabled = !demoMode;
        
        try
        {
        }
        catch { }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_isDemoMode)
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
        }
        catch (System.Exception ex)
        {
            MessageBox.Show("No se pudo abrir la configuración: " + ex.Message);
        }
        Close();
    }

    private void Ignore_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
