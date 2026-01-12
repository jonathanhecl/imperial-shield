@echo off
title Imperial Shield - Publish
cd /d "%~dp0ImperialShield"

echo.
echo ========================================
echo   Imperial Shield - Publicacion
echo ========================================
echo.
echo Creando ejecutable autocontenido...
echo.

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if %ERRORLEVEL% neq 0 (
    echo ERROR: Fallo en publicacion
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Publicacion exitosa!
echo ========================================
echo.
echo Ejecutable: bin\Release\net8.0-windows\win-x64\publish\ImperialShield.exe
echo.
echo Este ejecutable es autocontenido y no requiere .NET instalado.
echo.
pause
