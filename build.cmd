@echo off
setlocal
cd /d "%~dp0"

for /f "tokens=2 delims=:." %%c in ('chcp') do set "OLD_CP=%%c"
chcp 65001 >nul
rem Файл в UTF-8 без BOM: с BOM cmd не прочитает первую строку.

set "EXIT_CODE=0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo Нужен .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0
    set "EXIT_CODE=1"
    goto :end
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\publish-windows.ps1" -SelfContained
if errorlevel 1 (
    set "EXIT_CODE=1"
    goto :end
)

echo.
echo Запуск: dist\win-x64-selfcontained\ExternalIpWidget.exe

:end
pause
chcp %OLD_CP% >nul
exit /b %EXIT_CODE%
