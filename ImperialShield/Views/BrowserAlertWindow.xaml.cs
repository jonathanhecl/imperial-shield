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

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
