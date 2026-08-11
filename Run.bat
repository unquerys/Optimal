@echo off
setlocal
title Optimal Launcher
set "ROOT=%~dp0"

if exist "%ROOT%artifacts\package\Optimal.exe" (
  "%ROOT%artifacts\package\Optimal.exe" --onboarding
  exit /b %errorlevel%
)

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

dotnet run --project "%ROOT%Optimal.App\Optimal.App.csproj" -c Release -- --onboarding
if errorlevel 1 (
  echo.
  echo Optimal failed to build or start. Review the error above.
  pause
  exit /b 1
)

exit /b 0
