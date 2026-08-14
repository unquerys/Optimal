@echo off
setlocal
title Optimal Launcher
set "ROOT=%~dp0"

where dotnet.exe >nul 2>nul
if errorlevel 1 (
  echo.
  echo Optimal could not start because the .NET 9 SDK is not installed.
  echo Install the stable release from GitHub, or install the .NET 9 SDK to run from source.
  echo https://github.com/unquerys/Optimal/releases/latest
  echo.
  pause
  exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%Run-Optimal.ps1"
if errorlevel 1 (
  echo.
  echo Optimal failed to build or start.
  echo Review .logs\launcher.log and %%LOCALAPPDATA%%\Optimal\logs\startup.log.
  pause
  exit /b 1
)

exit /b 0
