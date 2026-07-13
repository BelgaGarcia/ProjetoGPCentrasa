@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0audit-portfolio.ps1" %*
exit /b %ERRORLEVEL%
