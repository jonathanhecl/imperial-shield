# Imperial Shield Landing Page

Esta carpeta contiene la Landing Page del proyecto.

## 📁 Estructura

- `index.html`: Página principal.
- `styles.css`: Estilos (Tema oscuro, responsive, animaciones).

## 🚀 Cómo desplegar en GitHub Pages

Dado que esta página está en una carpeta personalizada (`web/`), hemos configurado un **GitHub Action** para desplegarla automáticamente.

1.  Ve a la pestaña **Settings** de tu repositorio en GitHub.
2.  En el menú lateral, selecciona **Pages**.
3.  En **Build and deployment** > **Source**, selecciona **GitHub Actions**.
4.  ¡Listo! El worfklow `.github/workflows/deploy-web.yml` se encargará de todo.
    - Cada vez que hagas push a `main`, el sitio se actualizará.

## 🧪 Probar localmente

Simplemente abre el archivo `index.html` en tu navegador web. No requiere servidor, funciona directamente.

**Nota:** Los enlaces de descarga apuntan a `releases/latest`, por lo que siempre dirigirán a la última versión disponible en GitHub.
