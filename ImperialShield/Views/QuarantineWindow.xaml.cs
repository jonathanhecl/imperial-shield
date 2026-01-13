using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ImperialShield.Services;

namespace ImperialShield.Views;

public partial class QuarantineWindow : Window
{
    public QuarantineWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshVBSStatus();
        RefreshQuarantineList();
    }

    #region VBS Toggle

    private void RefreshVBSStatus()
    {
        bool isEnabled = QuarantineService.IsVBSEnabled();
        
        if (isEnabled)
        {
            VBSStatusText.Text = "🟢 HABILITADO - Los scripts .vbs/.js pueden ejecutarse";
            VBSStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
            VBSToggleButton.Content = "DESACTIVAR";
            VBSToggleButton.Style = (Style)FindResource("DangerButton");
        }
        else
        {
            VBSStatusText.Text = "🔴 BLOQUEADO - Los scripts .vbs/.js NO pueden ejecutarse";
            VBSStatusText.Foreground = System.Windows.Media.Brushes.Tomato;
            VBSToggleButton.Content = "ACTIVAR";
            VBSToggleButton.Style = (Style)FindResource("ModernButton");
        }
    }

    private void ToggleVBS_Click(object sender, RoutedEventArgs e)
    {
        bool currentlyEnabled = QuarantineService.IsVBSEnabled();
        
        if (currentlyEnabled)
        {
            // Desactivar VBS
            if (MessageBox.Show(
                "¿Deseas DESACTIVAR Windows Script Host?\n\n" +
                "Esto bloqueará la ejecución de todos los archivos .vbs, .js, .vbe, .jse.\n\n" +
                "Es una medida de seguridad recomendada ya que estos scripts son " +
                "muy usados por virus y ransomware.",
                "Confirmar Bloqueo de Scripts",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (QuarantineService.SetVBSEnabled(false))
                {
                    MessageBox.Show(
                        "✅ Windows Script Host ha sido DESACTIVADO.\n\n" +
                        "Ahora cualquier intento de ejecutar un script .vbs mostrará un error de Windows.",
                        "Scripts Bloqueados",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("No se pudo modificar la configuración.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        else
        {
            // Reactivar VBS - Mostrar advertencia grave
            var result = MessageBox.Show(
                "⚠️ ¡ADVERTENCIA DE SEGURIDAD!\n\n" +
                "Estás a punto de REACTIVAR Windows Script Host.\n\n" +
                "Esto permitirá que cualquier archivo .vbs/.js se ejecute,\n" +
                "incluyendo posibles virus y ransomware.\n\n" +
                "Solo reactiva esto si REALMENTE necesitas ejecutar scripts\n" +
                "y sabes lo que estás haciendo.\n\n" +
                "¿Estás SEGURO de que quieres reactivar los scripts?",
                "⚠️ ALERTA DE SEGURIDAD",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (QuarantineService.SetVBSEnabled(true))
                {
                    MessageBox.Show(
                        "Windows Script Host ha sido REACTIVADO.\n\n" +
                        "Los scripts .vbs/.js ahora pueden ejecutarse.\n" +
                        "Ten cuidado con los archivos que abres.",
                        "Scripts Habilitados",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show("No se pudo modificar la configuración.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        RefreshVBSStatus();
    }

    #endregion

    #region Executable Quarantine

    private void RefreshQuarantineList()
    {
        var apps = QuarantineService.GetQuarantinedApps();
        QuarantineGrid.ItemsSource = apps;
        
        EmptyListText.Visibility = apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BrowseExe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar ejecutable para bloquear",
            Filter = "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            NewExeTextBox.Text = System.IO.Path.GetFileName(dialog.FileName);
        }
    }

    private void AddQuarantine_Click(object sender, RoutedEventArgs e)
    {
        string exeName = NewExeTextBox.Text.Trim();
        
        if (string.IsNullOrEmpty(exeName))
        {
            MessageBox.Show("Ingresa el nombre de un ejecutable.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Añadir .exe si no lo tiene
        if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            exeName += ".exe";
        }

        // Validar que no sea un ejecutable del sistema crítico
        string[] protectedExes = { "explorer.exe", "svchost.exe", "csrss.exe", "winlogon.exe", 
                                   "services.exe", "lsass.exe", "smss.exe", "wininit.exe",
                                   "dwm.exe", "taskmgr.exe", "cmd.exe", "powershell.exe" };
        
        if (Array.Exists(protectedExes, x => x.Equals(exeName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                $"No puedes bloquear '{exeName}' porque es un proceso crítico del sistema.\n\n" +
                "Bloquear este ejecutable causaría inestabilidad o fallas graves en Windows.",
                "Ejecutable Protegido",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        if (QuarantineService.IsQuarantined(exeName))
        {
            MessageBox.Show($"'{exeName}' ya está en cuarentena.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"¿Deseas BLOQUEAR permanentemente '{exeName}'?\n\n" +
            "Este ejecutable no podrá ejecutarse bajo ninguna circunstancia\n" +
            "hasta que lo liberes de la cuarentena.\n\n" +
            "⚠️ Requiere permisos de Administrador.",
            "Confirmar Bloqueo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            if (QuarantineService.QuarantineExecutable(exeName))
            {
                MessageBox.Show(
                    $"✅ '{exeName}' ha sido puesto en CUARENTENA.\n\n" +
                    "El programa ya no puede ejecutarse. Si alguien intenta abrirlo,\n" +
                    "Imperial Shield mostrará una alerta de bloqueo.",
                    "Ejecutable Bloqueado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                NewExeTextBox.Text = "";
                RefreshQuarantineList();
            }
            else
            {
                MessageBox.Show(
                    "No se pudo poner el ejecutable en cuarentena.\n\n" +
                    "Asegúrate de ejecutar Imperial Shield como Administrador.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void ReleaseQuarantine_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string fileName)
        {
            var confirm = MessageBox.Show(
                $"¿Deseas LIBERAR '{fileName}' de la cuarentena?\n\n" +
                "El ejecutable podrá ejecutarse nuevamente de forma normal.\n\n" +
                "⚠️ Solo hazlo si estás seguro de que el archivo es seguro.",
                "Confirmar Liberación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                if (QuarantineService.UnquarantineExecutable(fileName))
                {
                    MessageBox.Show(
                        $"'{fileName}' ha sido liberado de la cuarentena.\n\n" +
                        "El programa puede ejecutarse nuevamente.",
                        "Ejecutable Liberado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    RefreshQuarantineList();
                }
                else
                {
                    MessageBox.Show(
                        "No se pudo liberar el ejecutable.\n\n" +
                        "Asegúrate de ejecutar Imperial Shield como Administrador.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }

    #endregion
}
