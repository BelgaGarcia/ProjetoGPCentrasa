@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0V.ps1" %*
exit /b %ERRORLEVEL%
