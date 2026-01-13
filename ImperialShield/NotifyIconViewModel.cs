using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;
using ImperialShield.Services;
using ImperialShield.Views;

namespace ImperialShield;

/// <summary>
/// Comandos globales para el systray
/// </summary>
public static class Commands
{
    public static readonly RoutedCommand ShowDashboard = new();
}

/// <summary>
/// Handler de eventos del icono del systray
/// </summary>
public class NotifyIconViewModel
{
    private DashboardWindow? _dashboardWindow;

    public void ShowDashboard_Click(object sender, RoutedEventArgs e)
    {
        ShowDashboard();
    }

    public void ShowDashboard()
    {
        if (_dashboardWindow == null || !_dashboardWindow.IsLoaded)
        {
            _dashboardWindow = new DashboardWindow();
        }
        
        _dashboardWindow.Show();
        _dashboardWindow.WindowState = WindowState.Normal;
        _dashboardWindow.Activate();
    }

    public void ShowProcessViewer_Click(object sender, RoutedEventArgs e)
    {
        var window = new ProcessViewerWindow();
        window.Show();
    }

    public void ShowNetworkViewer_Click(object sender, RoutedEventArgs e)
    {
        var window = new NetworkViewerWindow();
        window.Show();
    }

    public void ShowStartupManager_Click(object sender, RoutedEventArgs e)
    {
        var window = new StartupManagerWindow();
        window.Show();
    }

    public void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWin = new SettingsWindow();
        settingsWin.ShowDialog();
    }

    public void Exit_Click(object sender, RoutedEventArgs e)
    {
        var confirmWindow = new ConfirmExitWindow();
        if (confirmWindow.ShowDialog() == true)
        {
            Application.Current.Shutdown();
        }
    }
}
