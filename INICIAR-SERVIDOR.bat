@echo off
title Iniciar Servidor Lowcord (Solo Servidor / Headless)
cd /d "%~dp0"
if exist "%~dp0EJECUTABLES_LISTOS\iniciar_host.ps1" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0EJECUTABLES_LISTOS\iniciar_host.ps1" -NoClient
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0iniciar_host.ps1" -NoClient
)
