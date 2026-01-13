using System;
using System.Windows;
using Microsoft.Win32;
using System.Diagnostics;

namespace ImperialShield.Views;

public enum SecurityAction
{
    Delete,
    Ignore
}

public partial class SecurityWarningWindow : Window
{
    public SecurityAction Result { get; private set; } = SecurityAction.Ignore;
    private readonly string _exeName;

    public SecurityWarningWindow(string exeName, string debuggerPath)
    {
        InitializeComponent();
        _exeName = exeName;
        TargetExeText.Text = exeName;
        DebuggerPathText.Text = debuggerPath;
        
        System.Media.SystemSounds.Hand.Play();
    }

    private void OpenDebuggerLocation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
             if (System.IO.File.Exists(DebuggerPathText.Text))
             {
                 Process.Start("explorer.exe", $"/select,\"{DebuggerPathText.Text}\"");
             }
        }
        catch { }
    }

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            $"¿Estás seguro de que quieres eliminar la redirección de '{_exeName}'?\n\n" +
            "Se eliminará la entrada del registro que intercepta la ejecución de este programa.",
            "Confirmar Acción", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            Result = SecurityAction.Delete;
            this.Close();
        }
    }

    private void Ignore_Click(object sender, RoutedEventArgs e)
    {
        Result = SecurityAction.Ignore;
        this.Close();
    }

    public static SecurityAction ShowWarning(string exeName, string debuggerPath)
    {
        SecurityAction result = SecurityAction.Ignore;
        Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new SecurityWarningWindow(exeName, debuggerPath);
            win.ShowDialog();
            result = win.Result;
        });
        return result;
    }
}
