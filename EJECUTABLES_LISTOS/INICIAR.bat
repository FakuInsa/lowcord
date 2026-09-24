@echo off
title Iniciar Lowcord
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0iniciar_host.ps1"
