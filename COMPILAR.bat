@echo off
title Compilar Lowcord (.NET 9)
cd /d "%~dp0"
echo ===================================================
echo             COMPILANDO LOWCORD (.NET 9)
echo ===================================================
echo.
echo [1/2] Compilando Servidor WebRTC y Signaling...
dotnet publish src\Lowcord.Server\Lowcord.Server.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o EJECUTABLES_LISTOS\Servidor
if errorlevel 1 (
    echo [ERROR] Fallo al compilar el servidor.
    pause
    exit /b %errorlevel%
)

echo.
echo [2/2] Compilando Cliente Nativo de Escritorio...
dotnet publish src\Lowcord.Client\Lowcord.Client.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o src\Lowcord.Client\bin\publish
if errorlevel 1 (
    echo [ERROR] Fallo al compilar el cliente.
    pause
    exit /b %errorlevel%
)

copy /y "src\Lowcord.Client\bin\publish\Lowcord.Client.exe" "EJECUTABLES_LISTOS\Lowcord-Client.exe" >nul
xcopy /y /e /i "src\Lowcord.Server\wwwroot" "EJECUTABLES_LISTOS\Servidor\wwwroot" >nul

echo.
echo ===================================================
echo   Compilacion exitosa.
echo   Ejecutables listos en la carpeta: EJECUTABLES_LISTOS\
echo ===================================================
echo.
pause
