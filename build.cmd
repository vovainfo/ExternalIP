@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo Нужен .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\publish-windows.ps1" -SelfContained
if errorlevel 1 (
    pause
    exit /b 1
)

echo.
echo Запуск: dist\win-x64-selfcontained\ExternalIpWidget.exe
pause
