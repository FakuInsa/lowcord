@echo off
title Compilar Lowcord (.NET 9)
cd /d "%~dp0"
echo ===================================================
echo             COMPILANDO LOWCORD (.NET 9)
echo ===================================================
echo.
REM Cerrar procesos previos para evitar bloqueos de archivos en disco
taskkill /F /IM Lowcord.Server.exe >nul 2>&1
taskkill /F /IM Lowcord-Client.exe >nul 2>&1
taskkill /F /IM Lowcord.Client.exe >nul 2>&1

echo [1/2] Compilando Servidor WebRTC y Signaling...
dotnet publish src\Lowcord.Server\Lowcord.Server.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist\Servidor
if errorlevel 1 (
    echo [ERROR] Fallo al compilar el servidor.
    pause
    exit /b %errorlevel%
)

echo.
echo [2/2] Compilando Cliente Nativo de Escritorio...
dotnet publish src\Lowcord.Client\Lowcord.Client.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
if errorlevel 1 (
    echo [ERROR] Fallo al compilar el cliente.
    pause
    exit /b %errorlevel%
)

REM Renombrar a Lowcord-Client.exe para compatibilidad limpia y sin duplicados
if exist "dist\Lowcord.Client.exe" (
    move /y "dist\Lowcord.Client.exe" "dist\Lowcord-Client.exe" >nul
)
if exist "dist\Lowcord.Client.pdb" del /q "dist\Lowcord.Client.pdb" >nul

echo.
echo ===================================================
echo   Compilacion exitosa.
echo   Ejecutables listos en la carpeta: dist\
echo ===================================================
echo.
pause
