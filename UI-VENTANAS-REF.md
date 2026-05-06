# Referencia Rapida - Ventanas de Imperial Shield

## Ubicacion
Todas las ventanas XAML estan en: `ImperialShield/Views/`

---

## Ventanas con NUEVO diseno RPG (actualizadas)

| Archivo | Tipo | Estado |
|---------|------|--------|
| `DashboardWindow.xaml` | Principal | Grid HUD, header dorado, iconos glow, badges activos |
| `SplashWindow.xaml` | Carga | Aura escudo, barra progreso glow, titulo neon, grid fondo |
| `SettingsWindow.xaml` | Configuracion | Grid azul, borde gradiente dorado, header glow |
| `AlertWindow.xaml` | Alerta critica (DDoS) | Fondo #060D18, grid rojo, glow radial, borde pulsante |
| `DefenderAlertWindow.xaml` | Alerta Defender | Fondo #060D18, grid rojo, glow radial, borde pulsante |
| `BrowserAlertWindow.xaml` | Alerta navegador | Fondo #060D18, grid ambar, glow radial, borde pulsante |
| `HostsAlertWindow.xaml` | Alerta HOSTS | Fondo #060D18, grid ambar, glow radial, borde pulsante |
| `NewTaskAlertWindow.xaml` | Alerta tarea nueva | Fondo #060D18, grid ambar, glow radial, borde pulsante |

---

## Ventanas con diseno RPG actualizado

| Archivo | Tipo | Color tema |
|---------|------|----------|
| `AlertWindow.xaml` | Alerta critica (DDoS) | Rojo `#F43F5E` |
| `DefenderAlertWindow.xaml` | Alerta Defender | Rojo `#F43F5E` |
| `BrowserAlertWindow.xaml` | Alerta navegador | Ambar `#F39C12` |
| `HostsAlertWindow.xaml` | Alerta HOSTS | Ambar `#F39C12` |
| `NewTaskAlertWindow.xaml` | Alerta tarea nueva | Ambar `#F59E0B` |
| `PrivacyAlertWindow.xaml` | Alerta privacidad | Rojo `#F43F5E` |
| `StartupAlertWindow.xaml` | Alerta inicio | Rojo `#E74C3C` |
| `SecurityWarningWindow.xaml` | Advertencia seguridad | Rojo `#E74C3C` |
| `BlockedExecutionWindow.xaml` | Ejecucion bloqueada | Ambar `#F39C12` |
| `ConfirmExitWindow.xaml` | Confirmar salida | Rojo `#E11D48` |
| `AlertTestWindow.xaml` | Debug alertas | Dorado `#FFCC00` |

---

## Ventanas de herramienta/gestion (pendientes de revisar)

| Archivo | Tipo | Estado |
|---------|------|--------|
| `NetworkViewerWindow.xaml` | Visor red | Revisar |
| `DDoSTrackerWindow.xaml` | Tracker DDoS | Revisar |
| `ProcessViewerWindow.xaml` | Visor procesos | Revisar |
| `PrivacyManagerWindow.xaml` | Centro privacidad | Revisar |
| `QuarantineWindow.xaml` | Cuarentena | Revisar |
| `ScheduledTasksWindow.xaml` | Tareas programadas | Tiene buscador, revisar estilo visual |
| `StartupManagerWindow.xaml` | Gestor inicio | Revisar |

---

## Patron para actualizar una ventana al estilo RPG

Copiar este bloque dentro del `<Grid>` principal de la ventana, **antes** del contenido:

```xml
<Grid>
    <!-- Background Grid -->
    <Border Opacity="0.08" Margin="4">
        <Border.Background>
            <VisualBrush TileMode="Tile" Viewport="0,0,60,60" ViewportUnits="Absolute">
                <VisualBrush.Visual>
                    <Path Data="M0,0 L60,0 M0,0 L0,60" 
                          Stroke="#COLOR_ALERTA" 
                          StrokeThickness="0.8" 
                          StrokeDashArray="4,4"/>
                </VisualBrush.Visual>
            </VisualBrush>
        </Border.Background>
    </Border>
    <!-- Center Glow -->
    <Border>
        <Border.Background>
            <RadialGradientBrush Center="0.5,0.4" RadiusX="0.5" RadiusY="0.5">
                <GradientStop Color="#COLOR_SUTIL" Offset="0"/>
                <GradientStop Color="Transparent" Offset="1"/>
            </RadialGradientBrush>
        </Border.Background>
    </Border>

    <!-- CONTENIDO EXISTENTE VA AQUI -->
</Grid>
```

**Colores segun tipo de alerta:**
- Alertas criticas (rojas): `Stroke="#F43F5E"`, glow `#1A0508`
- Alertas advertencia (ambar): `Stroke="#F39C12"`, glow `#1A1205`
- Ventanas normales/configuracion: `Stroke="#4DA8DA"`, glow `#0A1525`

**Fondo del Border principal:** cambiar `Background="#0A0F1E"` por `Background="#060D18"`

**Glow del borde:** aumentar `BlurRadius="80"` y `Opacity="0.8"` en el `DropShadowEffect`

---

## Archivo de temas global

`Themes/RPGTheme.xaml` contiene estilos reutilizables:
- `RPGCard` - estilo de tarjeta con borde 3D
- `RPGButton`, `RPGDangerButton`, `RPGSuccessButton` - botones con glow
- `RPGBadge`, `RPGSuccessBadge`, `RPGWarningBadge`, `RPGDangerBadge` - badges con glow
- `RPGIconContainer` - contenedor de icono 44x44 con sombra
- Colores y brushes centralizados

Para usar: `{StaticResource RPGCard}` o `{StaticResource RPGAccentBrush}`
