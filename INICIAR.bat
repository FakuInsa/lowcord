@echo off
title Iniciar Lowcord
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0EJECUTABLES_LISTOS\iniciar_host.ps1"
