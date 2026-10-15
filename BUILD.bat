@echo off
setlocal
cd /d "%~dp0"
echo MouseOverAssist - Build
echo.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
set "BUILD_EXIT=%ERRORLEVEL%"
echo.
if not "%BUILD_EXIT%"=="0" echo Build failed. Please check BUILD_LOG.txt.
pause
exit /b %BUILD_EXIT%
