# 🛡️ Imperial Shield

<div align="center">

![Imperial Shield](https://img.shields.io/badge/Imperial%20Shield-Security-blue?style=for-the-badge&logo=shield)
![.NET 8](https://img.shields.io/badge/.NET-8.0-purple?style=for-the-badge&logo=dotnet)
![Windows](https://img.shields.io/badge/Windows-7%2F10%2F11-0078D6?style=for-the-badge&logo=windows)
![License](https://img.shields.io/badge/License-AGPL--3.0-green?style=for-the-badge)

**Centinela de Seguridad en tiempo real para Windows**

[🌐 Visitar Sitio Web](https://jonathanhecl.github.io/imperial-shield/)

</div>

---

## ✨ ¿Qué es Imperial Shield?

**Imperial Shield** es una herramienta ligera de código abierto diseñada para proteger y monitorear la integridad de tu sistema Windows. Se ejecuta en segundo plano y te alerta instantáneamente sobre cambios sospechosos que suelen ser vectores de ataque para malware y troyanos.

---

## 🚀 Características Principales

### 🛡️ Módulo Centinela (Primer Plano de Defensa)
*   **Monitor de HOSTS:** Detecta al instante si algún programa intenta redirigir tus sitios web (Phishing/Bloqueo de AV).
*   **Monitor de Defender:** Te avisa si el antivirus se desactiva o si se añaden exclusiones sospechosas.
*   **Anti-Hijack de Navegador:** Supervisa cambios no autorizados en tu navegador predeterminado.
*   **Alertas de Privacidad:** Notificaciones en tiempo real cuando una aplicación accede a tu **Cámara** o **Micrófono**.
*   **Detección DDoS/Botnet:** Identifica si tu PC está siendo usado para atacar otros servidores.

### 🔍 Herramientas Profesionales
*   **Visor de Procesos:** Análisis profundo con verificación de firmas digitales para detectar intrusos.
*   **Monitor de Red:** Mapea cada conexión de red a su proceso correspondiente para detectar *Reverse Shells*.
*   **Dashboard Intuitivo:** Control total desde una interfaz moderna y sencilla.

---

## 💻 Instalación y Uso

### Opción Rápida (Recomendada)
1. Descarga la última versión en [Releases](https://github.com/jonathanhecl/imperial-shield/releases).
2. Ejecuta `ImperialShield.exe` como **Administrador**.

### Para Desarrolladores (Compilación)
```powershell
git clone https://github.com/jonathanhecl/imperial-shield.git
cd imperial-shield/ImperialShield
dotnet run
```

---

## ⚙️ Configuración y Alertas

Imperial Shield vive en tu **bandeja de sistema (systray)**. Haz clic derecho en el icono del escudo para:
*   Abrir el Panel de Control.
*   Analizar procesos en tiempo real.
*   Ver las herramientas de red.

Cuando ocurre algo sospecho, recibirás una **notificación nativa de Windows** con opciones rápidas para bloquear o revertir el cambio.

---

## 🔒 Privacidad y Seguridad

*   **100% Offline:** No se envían datos a la nube. Todo el análisis ocurre en tu PC.
*   **Transparente:** Código abierto para que cualquiera pueda verificar su funcionamiento.
*   **Ligero:** Consume menos de 20MB de RAM en reposo.

---

## 🏗️ Estructura del Proyecto

*   **Services/**: Lógica de monitoreo (Hosts, Defender, Red, Privacidad).
*   **Views/**: Interfaz de usuario moderna en WPF.
*   **Themes/**: Diseño premium en modo oscuro.

---

## 📜 Licencia y Comunidad

Este proyecto es **Código Abierto** bajo la licencia **AGPL-3.0**.

*   ¿Encontraste un error? Abre un [Issue](https://github.com/jonathanhecl/imperial-shield/issues).
*   ¿Quieres ayudar? ¡Los Pull Requests son bienvenidos!

---

<div align="center">

**Hecho con ❤️ para la comunidad de seguridad**

*Imperial Shield - Mantén tu Windows bajo control*

</div>
