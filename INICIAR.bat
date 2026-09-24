@echo off
title Iniciar Lowcord (Servidor + Cliente)
cd /d "%~dp0"
if exist "%~dp0EJECUTABLES_LISTOS\iniciar_host.ps1" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0EJECUTABLES_LISTOS\iniciar_host.ps1"
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0iniciar_host.ps1"
)
