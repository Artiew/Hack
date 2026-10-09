@echo off
rem Dois cliques aqui: sobe a Plataforma Piersec e abre o navegador.
title Plataforma Piersec - NAO FECHE esta janela enquanto usar o site
cd /d "%~dp0"
rem -SemTestes: inicia sem executar a suite; os testes podem ser rodados separadamente.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0rodar.ps1" -SemTestes %*
echo.
echo O site foi encerrado. Se apareceu algum erro acima, mande para a equipe.
echo Aperte qualquer tecla para fechar esta janela.
pause >nul
