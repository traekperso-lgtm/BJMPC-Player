@echo off
rem BJMPC Player - double-clic pour installer (telecharge la derniere version publiee)
rem   installer.bat --compiler  : compile depuis les sources (installe le SDK .NET 8 si besoin)
title BJMPC Player - installation
cd /d "%~dp0"
set "ARGS="
if /i "%~1"=="--compiler" set "ARGS=-Compiler"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\install.ps1" %ARGS%
echo.
pause
