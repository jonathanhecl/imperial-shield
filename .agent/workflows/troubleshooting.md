---
description: Soluciones a errores comunes de compilación en .NET WPF
---

# Troubleshooting - Errores de Compilación

Este workflow documenta errores comunes encontrados y sus soluciones.

## Error: "El tipo 'TaskbarIcon' no se encontró"

**Causa**: Namespace incorrecto en el using.

**Solución**: 
```csharp
// ❌ INCORRECTO
using Hardcodet.NotifyIcon.Wpf;

// ✅ CORRECTO
using Hardcodet.Wpf.TaskbarNotification;
```

## Error: "El nombre 'File' no existe en el contexto actual"

**Causa**: Falta el using de System.IO.

**Solución**:
```csharp
using System.IO;
```

## Error: "ToastContentBuilder no contiene Show"

**Causa**: El método Show() es una extensión que requiere paquete adicional o configuración.

**Solución alternativa**: Usar las notificaciones balloon del TaskbarIcon:
```csharp
// En lugar de:
new ToastContentBuilder().AddText("Hola").Show();

// Usar:
_notifyIcon.ShowBalloonTip("Título", "Mensaje", BalloonIcon.Info);
```

## Error: PowerShell SDK - "Assembly not found"

**Causa**: Falta el paquete Microsoft.PowerShell.SDK.

**Solución**:
```powershell
dotnet add package Microsoft.PowerShell.SDK --version 7.4.1
```

## Error: "dotnet no reconocido"

**Causa**: .NET SDK no está en el PATH o no está instalado.

**Solución**:
1. Descargar .NET 8 SDK desde https://dotnet.microsoft.com/download/dotnet/8.0
2. Reiniciar PowerShell después de instalar
3. Verificar con `dotnet --version`

## Ver errores detallados de compilación

Para ver todos los errores sin truncamiento:
```powershell
dotnet build 2>&1 | Out-File -FilePath build_output.txt -Encoding UTF8
Get-Content build_output.txt
```

## Limpiar y recompilar

Si hay errores extraños después de cambios en el .csproj:
```powershell
dotnet clean
dotnet restore
dotnet build
```
