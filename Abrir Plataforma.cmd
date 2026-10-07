@echo off
rem Dois cliques aqui: sobe a Plataforma Piersec e abre o navegador.
title Plataforma Piersec - NAO FECHE esta janela enquanto usar o site
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0rodar.ps1" %*
echo.
echo O site foi encerrado. Se apareceu algum erro acima, mande para a equipe.
echo Aperte qualquer tecla para fechar esta janela.
pause >nul
