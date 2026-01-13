using System.Windows;
using ImperialShield.Services;

namespace ImperialShield.Views;

public partial class BlockedExecutionWindow : Window
{
    private readonly string _fullPath;

    public BlockedExecutionWindow(string fullPath)
    {
        InitializeComponent();
        _fullPath = fullPath;
        ExeNameText.Text = System.IO.Path.GetFileName(fullPath);
        ExeNameText.ToolTip = fullPath;
        
        // Sonido de alerta
        System.Media.SystemSounds.Hand.Play();
        
        Logger.Log($"BlockedExecutionWindow shown for: {fullPath}");
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (System.IO.File.Exists(_fullPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_fullPath}\"");
            }
            else
            {
                // Si el archivo no existe (ya borrado?), abrir la carpeta contenedora si es posible
                string? dir = System.IO.Path.GetDirectoryName(_fullPath);
                if (System.IO.Directory.Exists(dir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{dir}\"");
                }
            }
        }
        catch { }
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
    public static void ShowBlocked(string fullPath)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new BlockedExecutionWindow(fullPath);
            win.Topmost = true;
            win.ShowDialog();
        });
    }
}
