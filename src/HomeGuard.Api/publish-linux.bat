@echo off
setlocal
cd /d "%~dp0"

echo Publishing HomeGuard.Api for linux-x64 (single file, self-contained)...
dotnet publish . -c Release -r linux-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o bin\publish-linux

if errorlevel 1 (
    echo.
    echo Publish failed.
    pause
    exit /b 1
)

echo.
echo Done: bin\publish-linux
echo Copy that folder to the target machine, then on Linux:
echo   chmod +x HomeGuard.Api run-dev.sh
echo   ./run-dev.sh
pause
