@echo off
title Imperial Shield - Build
cd /d "%~dp0ImperialShield"

echo.
echo ========================================
echo   Imperial Shield - Compilacion
echo ========================================
echo.

echo [1/2] Restaurando dependencias...
dotnet restore
if %ERRORLEVEL% neq 0 (
    echo ERROR: Fallo en restore
    pause
    exit /b 1
)

echo.
echo [2/2] Compilando en Release...
dotnet build --configuration Release
if %ERRORLEVEL% neq 0 (
    echo ERROR: Fallo en compilacion
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Compilacion exitosa!
echo ========================================
echo.
echo Ejecutable: bin\Release\net8.0-windows\ImperialShield.exe
echo.
echo Presiona cualquier tecla para ejecutar la aplicacion...
pause > nul

start "" "bin\Release\net8.0-windows\ImperialShield.exe"
