# Instrucciones para crear los iconos de Imperial Shield

## Iconos Generados

Se han generado dos imágenes PNG para los iconos:

1. **shield_icon.png** - Icono normal (escudo azul con checkmark)
2. **shield_alert_icon.png** - Icono de alerta (escudo rojo con exclamación)

Estas imágenes se encuentran en la carpeta temporal de Gemini.

## Conversión a .ICO

### Opción 1: Usando un servicio online (Recomendado)

1. Ve a https://www.icoconverter.com/
2. Sube el archivo PNG generado
3. Selecciona "Multi-size in one .ico" con tamaños: 16, 32, 48, 256
4. Descarga y renombra como:
   - `shield.ico` (para el icono normal)
   - `shield_alert.ico` (para el icono de alerta)

### Opción 2: Usando ImageMagick

```powershell
# Instalar ImageMagick
winget install ImageMagick.ImageMagick

# Convertir los iconos
magick convert shield_icon.png -resize 256x256 -define icon:auto-resize=256,128,64,48,32,16 shield.ico
magick convert shield_alert_icon.png -resize 256x256 -define icon:auto-resize=256,128,64,48,32,16 shield_alert.ico
```

### Opción 3: Usando Python con Pillow

```python
from PIL import Image

# Cargar imagen
img = Image.open('shield_icon.png')

# Guardar como ICO con múltiples tamaños
img.save('shield.ico', format='ICO', sizes=[(16,16), (32,32), (48,48), (256,256)])
```

## Ubicación Final

Después de crear los archivos .ICO, colócalos en:

```
ImperialShield/
└── Resources/
    ├── shield.ico        ← Icono normal
    └── shield_alert.ico  ← Icono de alerta
```

## Alternativa: Compilar sin icono

Si prefieres compilar sin icono por ahora, comenta estas líneas en `ImperialShield.csproj`:

```xml
<!-- <ApplicationIcon>Resources\shield.ico</ApplicationIcon> -->
```

Y en `App.xaml`, el TaskbarIcon funcionará sin `IconSource` (mostrará un ícono por defecto).

## Diseño de los Iconos

### Icono Normal (shield.ico)
- Escudo azul oscuro (#1E3A5F)
- Checkmark cyan (#4DA8DA)
- Significa: "Sistema protegido"

### Icono de Alerta (shield_alert.ico)
- Escudo rojo (#E74C3C)
- Signo de exclamación
- Significa: "Alerta de seguridad detectada"
