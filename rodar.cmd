@echo off
rem Atalho para o rodar.ps1 que funciona mesmo com scripts do PowerShell bloqueados.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0rodar.ps1" %*
