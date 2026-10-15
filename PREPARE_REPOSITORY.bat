@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareRepository.ps1"
set "PREPARE_EXIT=%ERRORLEVEL%"
echo.
pause
exit /b %PREPARE_EXIT%
