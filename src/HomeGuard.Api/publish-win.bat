@echo off
setlocal
cd /d "%~dp0"

echo Publishing HomeGuard.Api for win-x64 (single file, self-contained)...
dotnet publish . -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o bin\publish-win

if errorlevel 1 (
    echo.
    echo Publish failed.
    pause
    exit /b 1
)

echo.
echo Done: bin\publish-win
echo Copy that folder to the target machine and run run-dev.bat, or HomeGuard.Api.exe directly.
pause
