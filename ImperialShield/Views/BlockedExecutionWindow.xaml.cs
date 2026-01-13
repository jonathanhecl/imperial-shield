using System.Windows;
using ImperialShield.Services;

namespace ImperialShield.Views;

public partial class BlockedExecutionWindow : Window
{
    public BlockedExecutionWindow(string blockedExeName)
    {
        InitializeComponent();
        ExeNameText.Text = blockedExeName;
        
        // Sonido de alerta
        System.Media.SystemSounds.Hand.Play();
        
        Logger.Log($"BlockedExecutionWindow shown for: {blockedExeName}");
    }



    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void OpenQuarantine_Click(object sender, RoutedEventArgs e)
    {
        var win = new QuarantineWindow();
        win.Show();
        this.Close();
    }

    /// <summary>
    /// Muestra la ventana de bloqueo de forma modal.
    /// </summary>
    public static void ShowBlocked(string exeName)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new BlockedExecutionWindow(exeName);
            win.Topmost = true;
            win.ShowDialog();
        });
    }
}
