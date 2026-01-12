---
description: Compilar y ejecutar el proyecto Imperial Shield
---

# Compilar Imperial Shield

Este workflow describe cómo compilar y ejecutar el proyecto de seguridad Imperial Shield.

## Requisitos previos
- .NET 8 SDK instalado
- Windows 7/10/11
- Iconos .ico en `Resources/` (shield.ico, shield_alert.ico)

## Compilación

### 1. Restaurar dependencias
// turbo
```powershell
cd ImperialShield
dotnet restore
```

### 2. Compilar en modo Debug
// turbo
```powershell
dotnet build
```

### 3. Compilar en modo Release
// turbo
```powershell
dotnet build --configuration Release
```

## Ejecución

### Ejecutar la aplicación (requiere Admin)
```powershell
.\bin\Release\net8.0-windows\ImperialShield.exe
```

**Nota**: La app requiere privilegios de administrador para:
- Acceder a WMI (SecurityCenter2)
- Ejecutar comandos PowerShell (Get-MpPreference)
- Monitorear el archivo HOSTS

## Publicar como ejecutable único

Para crear un ejecutable autocontenido:
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

El ejecutable estará en: `bin\Release\net8.0-windows\win-x64\publish\`

## Estructura de salida
```
bin\Release\net8.0-windows\
├── ImperialShield.exe          # Ejecutable principal
├── ImperialShield.dll          # Librería principal
├── appsettings.json            # Configuración
└── [dependencias NuGet]        # DLLs de terceros
```
