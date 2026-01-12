---
description: Arquitectura y patrones del proyecto Imperial Shield
---

# Arquitectura de Imperial Shield

Este documento describe la arquitectura y patrones usados en el proyecto.

## Stack Tecnológico

| Componente | Tecnología |
|------------|------------|
| Framework | .NET 8.0 |
| UI | WPF (Windows Presentation Foundation) |
| Systray | Hardcodet.NotifyIcon.Wpf |
| WMI | System.Management |
| PowerShell | Microsoft.PowerShell.SDK |

## Estructura del Proyecto

```
ImperialShield/
├── App.xaml                 # Punto de entrada + TaskbarIcon
├── App.xaml.cs              # Inicialización + Event handlers
├── app.manifest             # Privilegios de admin
├── appsettings.json         # Configuración
│
├── Services/                # Lógica de negocio
│   ├── HostsFileMonitor.cs      # FileSystemWatcher
│   ├── DefenderMonitor.cs       # WMI + PowerShell
│   ├── ProcessAnalyzer.cs       # Análisis de procesos
│   ├── NetworkMonitor.cs        # P/Invoke a iphlpapi.dll
│   ├── SingleInstanceManager.cs # Mutex
│   └── StartupManager.cs        # Registro de Windows
│
├── Views/                   # Ventanas WPF
│   ├── DashboardWindow.xaml
│   ├── ProcessViewerWindow.xaml
│   └── NetworkViewerWindow.xaml
│
├── Themes/                  # Estilos
│   └── DarkTheme.xaml
│
└── Resources/              # Iconos y recursos
    └── shield.ico
```

## Patrones Usados

### 1. FileSystemWatcher para monitoreo de archivos
```csharp
var watcher = new FileSystemWatcher(directory)
{
    Filter = "hosts",
    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
    EnableRaisingEvents = true
};
watcher.Changed += OnFileChanged;
```

### 2. Timer para polling periódico
```csharp
_timer = new Timer(CheckStatus, null, 0, 60000); // Cada 60 segundos
```

### 3. P/Invoke para APIs de Windows
```csharp
[DllImport("iphlpapi.dll", SetLastError = true)]
private static extern uint GetExtendedTcpTable(...);
```

### 4. PowerShell desde C#
```csharp
using var ps = PowerShell.Create();
ps.AddScript("Get-MpPreference");
var results = ps.Invoke();
```

### 5. WMI para información del sistema
```csharp
using var searcher = new ManagementObjectSearcher(
    @"root\SecurityCenter2",
    "SELECT * FROM AntiVirusProduct");
```

## Flujo de Eventos

```
App.Startup
    │
    ├─► SingleInstanceManager.TryAcquireLock()
    │
    ├─► TaskbarIcon (systray)
    │
    ├─► HostsFileMonitor.Start()
    │       └── FileSystemWatcher → OnChanged → ShowBalloonTip
    │
    └─► DefenderMonitor.Start()
            └── Timer 60s → CheckStatus → ShowBalloonTip
```

## Notas de Implementación

1. **ShutdownMode="OnExplicitShutdown"**: La app solo cierra con Application.Current.Shutdown()
2. **requireAdministrator**: Necesario para WMI SecurityCenter2 y Get-MpPreference
3. **Mutex**: Previene múltiples instancias de la aplicación
4. **BalloonTip**: Notificaciones nativas más compatibles que Toast UWP
