---
description: Crear un nuevo proyecto WPF con systray en .NET 8
---

# Crear proyecto WPF con Systray

Este workflow documenta cómo crear un proyecto WPF con icono de systray usando .NET 8.

## 1. Crear el proyecto base
```powershell
dotnet new wpf -n MiProyecto -f net8.0-windows
cd MiProyecto
```

## 2. Agregar dependencias NuGet

### Systray (TaskbarIcon)
```powershell
dotnet add package Hardcodet.NotifyIcon.Wpf --version 1.1.0
```

### WMI (para acceso al sistema)
```powershell
dotnet add package System.Management --version 8.0.0
```

### PowerShell SDK (para ejecutar comandos PS)
```powershell
dotnet add package Microsoft.PowerShell.SDK --version 7.4.1
```

## 3. Configurar App.xaml

El namespace correcto para el TaskbarIcon es:
```xml
xmlns:tb="http://www.hardcodet.net/taskbar"
```

Para aplicaciones de systray, usar:
```xml
ShutdownMode="OnExplicitShutdown"
```

## 4. Usar el TaskbarIcon

### En XAML (App.xaml):
```xml
<tb:TaskbarIcon x:Key="NotifyIcon"
                ToolTipText="Mi Aplicación">
    <tb:TaskbarIcon.ContextMenu>
        <ContextMenu>
            <MenuItem Header="Abrir" Click="Open_Click"/>
            <MenuItem Header="Salir" Click="Exit_Click"/>
        </ContextMenu>
    </tb:TaskbarIcon.ContextMenu>
</tb:TaskbarIcon>
```

### En C# (App.xaml.cs):
```csharp
using Hardcodet.Wpf.TaskbarNotification;

// Obtener el icono
_notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");

// Mostrar notificación
_notifyIcon.ShowBalloonTip("Título", "Mensaje", BalloonIcon.Info);

// Liberar al cerrar
_notifyIcon?.Dispose();
```

## 5. Manifest para privilegios de Admin

Crear `app.manifest`:
```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```

Y agregar al .csproj:
```xml
<ApplicationManifest>app.manifest</ApplicationManifest>
```

## Notas importantes

- El namespace es `Hardcodet.Wpf.TaskbarNotification` (NO `Hardcodet.NotifyIcon.Wpf`)
- Para notificaciones balloon usar `ShowBalloonTip()` del TaskbarIcon
- Los handlers de clic del ContextMenu van en App.xaml.cs
