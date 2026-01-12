using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace ImperialShield.Views;

/// <summary>
/// Splash screen mostrada al iniciar la aplicación
/// </summary>
public partial class SplashWindow : Window
{
    private readonly DispatcherTimer _closeTimer;

    public SplashWindow()
    {
        InitializeComponent();
        
        // Mostrar versión del ensamblado
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version != null)
        {
            VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
        }
        
        // Timer para cerrar automáticamente después de 2.5 segundos
        _closeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(2500)
        };
        _closeTimer.Tick += (s, e) =>
        {
            _closeTimer.Stop();
            Close();
        };
    }

    public void UpdateStatus(string status)
    {
        StatusText.Text = status;
    }

    public void StartCloseTimer()
    {
        _closeTimer.Start();
    }

    public void CloseNow()
    {
        _closeTimer.Stop();
        Close();
    }
    
    public static string GetVersionString()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return $"v{version?.Major}.{version?.Minor}.{version?.Build}.{version?.Revision}";
    }
}
