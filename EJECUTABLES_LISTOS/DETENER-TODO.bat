@echo off
title Detener Sala Completa (Todos)
cd /d "%~dp0"
echo =========================================================
echo  Cerrando Lowcord: Finalizando llamada para TODOS...
echo =========================================================

REM 1. Notificar a todos los amigos conectados para que se desconecten de inmediato
powershell -NoProfile -Command "try { $null = Invoke-RestMethod -Uri 'http://localhost:8080/api/shutdown' -TimeoutSec 1 -ErrorAction SilentlyContinue } catch {}" >nul 2>&1
timeout /t 1 /nobreak >nul

REM 2. Marcar sala como offline en el registro publico
powershell -NoProfile -Command "try { $rc = 'facu'; if (Test-Path '%~dp0room_code.txt') { $rc = (Get-Content '%~dp0room_code.txt' -Raw).Trim() }; Invoke-RestMethod -Uri ('https://api.keyval.org/set/lowcord_' + $rc + '/offline') -TimeoutSec 2 -ErrorAction SilentlyContinue } catch {}" >nul 2>&1

REM 3. Terminar procesos en tu equipo
taskkill /F /IM Lowcord-Client.exe >nul 2>&1
taskkill /F /IM Lowcord.Client.exe >nul 2>&1
taskkill /F /IM Lowcord.Server.exe >nul 2>&1
taskkill /F /IM cloudflared.exe >nul 2>&1

echo.
echo La llamada ha sido finalizada para todos y el servidor se ha apagado.
timeout /t 2 >nul
