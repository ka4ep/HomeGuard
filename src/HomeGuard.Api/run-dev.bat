@echo off
setlocal
cd /d "%~dp0"
set ASPNETCORE_ENVIRONMENT=Development
echo Starting HomeGuard.Api in Development mode...
HomeGuard.Api.exe
echo.
echo Server stopped (exit code %ERRORLEVEL%).
pause
