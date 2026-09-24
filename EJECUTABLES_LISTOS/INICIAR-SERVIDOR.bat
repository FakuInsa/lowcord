@echo off
title Iniciar Servidor Lowcord (Solo Servidor / Headless)
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0iniciar_host.ps1" -NoClient
