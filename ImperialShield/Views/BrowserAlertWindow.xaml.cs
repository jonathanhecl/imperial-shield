using System.Windows;

namespace ImperialShield.Views;

public partial class BrowserAlertWindow : Window
{
    public BrowserAlertWindow(string oldBrowser, string newBrowser, string newBrowserId)
    {
        InitializeComponent();
        
        OldBrowserText.Text = oldBrowser;
        NewBrowserText.Text = newBrowser;
        NewBrowserIdText.Text = newBrowserId;
        
        // Reproducir sonido de alerta si existe
        try
        {
            // Podríamos añadir un sonido aquí en el futuro
        }
        catch { }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Open Windows Default Apps settings
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
