@echo off
title Detener Servidor Local (Amigos siguen en P2P)
echo =========================================================
echo  Cerrando tu servidor local y cliente...
echo  (Tus amigos conectados continuaran hablando en modo P2P)
echo =========================================================

REM 1. Terminar procesos locales sin expulsar a tus amigos conectados
taskkill /F /IM Lowcord-Client.exe >nul 2>&1
taskkill /F /IM Lowcord.Client.exe >nul 2>&1
taskkill /F /IM Lowcord.Server.exe >nul 2>&1
taskkill /F /IM cloudflared.exe >nul 2>&1

echo.
echo Tu servidor local se ha cerrado.
echo Tus amigos continuaran comunicandose directamente por P2P mientras no cierren su ventana.
echo Para cortar la sala a todos usa: DETENER-TODO.bat
timeout /t 3 >nul
