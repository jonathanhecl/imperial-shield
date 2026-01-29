# 🛡️ Imperial Shield

<div align="center">

![Imperial Shield](https://img.shields.io/badge/Imperial%20Shield-Security-blue?style=for-the-badge&logo=shield)
![.NET 8](https://img.shields.io/badge/.NET-8.0-purple?style=for-the-badge&logo=dotnet)
![Windows](https://img.shields.io/badge/Windows-7%2F10%2F11-0078D6?style=for-the-badge&logo=windows)
![License](https://img.shields.io/badge/License-AGPL--3.0-green?style=for-the-badge)

**Sistema de Monitoreo de Seguridad para Windows**

*Detección en tiempo real de modificaciones al archivo HOSTS, exclusiones de Windows Defender, procesos sospechosos y conexiones de red anómalas.*

</div>

---

## 📋 Características

### 🔔 Módulo Centinela (Background/Systray)

| Función | Descripción |
|---------|-------------|
| **Monitor de HOSTS** | Detecta cambios en `C:\Windows\System32\drivers\etc\hosts` en tiempo real usando FileSystemWatcher |
| **Monitor de Defender** | Verifica estado del antivirus y detecta nuevas exclusiones cada 60 segundos |
| **Monitor de Navegador** | Detecta cambios no autorizados en el navegador predeterminado (Anti-Hijack) |
| **Monitor de Privacidad** | Alerta inmediatamanete cuando una app accede a tu **Cámara** o **Micrófono** |
| **Monitor DDoS** | Detecta si un proceso local está inundando la red o participando en una botnet |
| **Backup Automático** | Guarda copia de seguridad del archivo HOSTS limpio para restauración |
| **Notificaciones Toast** | Alertas nativas de Windows con acciones rápidas |

### 🔧 Herramientas de Investigación (Bajo Demanda)

| Herramienta | Descripción |
|-------------|-------------|
| **Visor de Procesos** | Análisis de procesos con verificación de firma digital y detección de rutas sospechosas |
| **Monitor de Conexiones** | NetStat con esteroides: mapea conexiones TCP a procesos y detecta Reverse Shells |
| **Dashboard** | Panel centralizado con estado del sistema y acceso rápido a herramientas |

---

## 🚀 Instalación

### Requisitos

- Windows 7 / 10 / 11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (o .NET 8 SDK para compilar)
- Privilegios de Administrador (para acceder a WMI y Windows Defender)

### Opción 1: Compilar desde código fuente

```powershell
# Clonar el repositorio
git clone https://github.com/jonathanhecl/imperial-shield.git
cd imperial-shield/ImperialShield

# Restaurar dependencias y compilar
dotnet restore
dotnet build --configuration Release

# Ejecutar
dotnet run
```

### Opción 2: Descargar Release

1. Ve a [Releases](https://github.com/jonathanhecl/imperial-shield/releases)
2. Descarga `ImperialShield-vX.X.X.zip`
3. Extrae y ejecuta `ImperialShield.exe` como Administrador

---

## 🔍 Uso

### Inicio Automático

Al ejecutarse, Imperial Shield se posiciona en el área de notificaciones (systray) y comienza a monitorear automáticamente.

### Menú del Systray

Haz clic derecho en el icono del escudo para acceder a:

- **📊 Abrir Dashboard** - Panel principal con estado del sistema
- **🔍 Visor de Procesos** - Analiza procesos en ejecución
- **🌐 Monitor de Conexiones** - Ve conexiones de red activas
- **📄 Ver Archivo HOSTS** - Abre el archivo hosts en Notepad
- **🛡️ Estado de Defender** - Muestra información del antivirus
- **⚙️ Configuración** - Opciones de la aplicación
- **❌ Salir** - Cierra Imperial Shield

### Alertas

Cuando se detecta una amenaza, recibirás una notificación Toast de Windows con:

- 📄 Descripción del evento
- 🔘 Botones de acción (Restaurar, Ver Detalles, etc.)

---

## 🏗️ Arquitectura

```
ImperialShield/
├── App.xaml                    # Punto de entrada de WPF
├── App.xaml.cs                 # Inicialización y manejo de eventos
├── ImperialShield.csproj       # Archivo de proyecto .NET 8
├── app.manifest                # Solicitud de privilegios de admin
│
├── Services/
│   ├── HostsFileMonitor.cs     # FileSystemWatcher para HOSTS
│   ├── DefenderMonitor.cs      # WMI + PowerShell para Defender
│   ├── ProcessAnalyzer.cs      # Análisis de procesos y firmas
│   ├── NetworkMonitor.cs       # P/Invoke a GetExtendedTcpTable
│   ├── BrowserMonitor.cs       # Monitor de registro para default browser
│   ├── DDoSMonitor.cs          # Análisis de inundación de paquetes
│   ├── PrivacyMonitor.cs       # Monitor de uso de Webcam/Mic
│   ├── SingleInstanceManager.cs # Mutex para instancia única
│   └── StartupManager.cs       # Gestión del registro Run
│
├── Views/
│   ├── DashboardWindow.xaml    # Panel principal
│   ├── ProcessViewerWindow.xaml # Visor de procesos
│   └── NetworkViewerWindow.xaml # Monitor de conexiones
│
├── Themes/
│   └── DarkTheme.xaml          # Tema oscuro premium
│
└── Resources/
    ├── shield.ico              # Icono normal
    └── shield_alert.ico        # Icono de alerta
```

---

## 🔐 Detección de Amenazas

### Niveles de Amenaza para Procesos

| Nivel | Color | Descripción |
|-------|-------|-------------|
| 🔴 **Crítico** | Rojo | Proceso del sistema ejecutándose desde ubicación no autorizada |
| 🟠 **Alto** | Naranja | Sin firma digital, desde carpeta de usuario |
| 🟡 **Medio** | Amarillo | Sin firma digital |
| 🟢 **Bajo** | Verde claro | Firmado por emisor desconocido |
| ✅ **Seguro** | Verde | Firmado por emisor confiable |

### Detección de Reverse Shells

Imperial Shield marca como **CRÍTICO** cuando detecta:

- `powershell.exe`, `cmd.exe`, `wscript.exe` con conexiones ESTABLISHED
- Conexiones a puertos conocidos de malware (4444, 6666, 31337, etc.)
- Procesos escuchando en puertos efímeros

---

## ⚙️ Configuración

El archivo `appsettings.json` permite personalizar:

```json
{
  "Monitoring": {
    "HostsFileEnabled": true,
    "DefenderMonitorEnabled": true,
    "DefenderPollingIntervalSeconds": 60
  },
  "Notifications": {
    "ShowToastNotifications": true,
    "PlaySoundOnAlert": true
  },
  "Startup": {
    "StartWithWindows": true,
    "StartMinimized": true
  }
}
```

---

## 🛠️ Desarrollo

### Dependencias NuGet

- `Hardcodet.NotifyIcon.Wpf` - Icono de systray para WPF
- `System.Management` - Acceso a WMI
- `Microsoft.Toolkit.Uwp.Notifications` - Toast notifications

### Compilación de Debug

```powershell
dotnet build --configuration Debug
```

### Publicar como ejecutable único

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 📊 Recursos del Sistema

| Métrica | Valor |
|---------|-------|
| RAM (reposo) | < 20 MB |
| CPU (reposo) | 0% |
| CPU (monitoreo) | < 1% |

---

## 🔒 Seguridad

- La aplicación requiere **privilegios de administrador** para:
  - Leer exclusiones de Windows Defender (`Get-MpPreference`)
  - Acceder a WMI `SecurityCenter2`
  - Modificar el archivo HOSTS (restauración)

- **No recopila ni envía datos** a servidores externos
- Todo el procesamiento es local

---

## 📜 Licencia

Este proyecto está bajo la licencia **AGPL-3.0**. Ver [LICENSE](LICENSE) para más detalles.

---

## 🤝 Contribuir

1. Fork del repositorio
2. Crear rama (`git checkout -b feature/NuevaCaracteristica`)
3. Commit (`git commit -m 'Agregar nueva característica'`)
4. Push (`git push origin feature/NuevaCaracteristica`)
5. Abrir Pull Request

---

## 📞 Soporte

- **Issues**: [GitHub Issues](https://github.com/jonathanhecl/imperial-shield/issues)
- **Discusiones**: [GitHub Discussions](https://github.com/jonathanhecl/imperial-shield/discussions)

---

<div align="center">

**Hecho con ❤️ para la comunidad de seguridad**

*Imperial Shield - Protegiendo tu Windows*

</div>