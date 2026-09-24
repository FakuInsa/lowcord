@echo off
title Detener Lowcord
cd /d "%~dp0"
echo =========================================================
echo             DETENIENDO SERVICIO LOWCORD
echo =========================================================
echo.
echo [1/3] Notificando a participantes y cerrando sala...
powershell -NoProfile -Command "try { $null = Invoke-RestMethod -Uri 'http://localhost:8080/api/shutdown' -TimeoutSec 1 -ErrorAction SilentlyContinue } catch {}" >nul 2>&1

echo [2/3] Marcando sala como fuera de linea...
powershell -NoProfile -Command "try { $rc = ''; if (Test-Path '%~dp0room_code.txt') { $rc = (Get-Content '%~dp0room_code.txt' -Raw).Trim() } elseif (Test-Path '%~dp0EJECUTABLES_LISTOS\room_code.txt') { $rc = (Get-Content '%~dp0EJECUTABLES_LISTOS\room_code.txt' -Raw).Trim() }; if ($rc) { Invoke-RestMethod -Uri ('https://api.keyval.org/set/lowcord_' + $rc + '/offline') -TimeoutSec 2 -ErrorAction SilentlyContinue } } catch {}" >nul 2>&1

echo [3/3] Cerrando procesos locales...
taskkill /F /IM Lowcord-Client.exe >nul 2>&1
taskkill /F /IM Lowcord.Client.exe >nul 2>&1
taskkill /F /IM Lowcord.Server.exe >nul 2>&1
taskkill /F /IM cloudflared.exe >nul 2>&1

echo.
echo =========================================================
echo   Lowcord se ha detenido correctamente.
echo =========================================================
timeout /t 2 >nul
