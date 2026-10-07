@echo off
rem Dois cliques aqui: gera uma senha temporaria nova para o admin (mostrada uma vez) e sobe o site.
title Plataforma Piersec - senha do admin redefinida - NAO FECHE esta janela
cd /d "%~dp0"
echo.
echo  Isto vai gerar uma SENHA TEMPORARIA NOVA para o usuario admin.
echo  A senha antiga deixa de valer. A nova aparece abaixo, uma unica vez.
echo.
choice /c SN /m "Continuar"
if errorlevel 2 exit /b
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0rodar.ps1" -RedefinirSenhaAdmin
echo.
echo O site foi encerrado. Aperte qualquer tecla para fechar esta janela.
pause >nul
