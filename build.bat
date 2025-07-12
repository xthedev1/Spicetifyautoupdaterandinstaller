@echo off
echo Building Spicetify Auto Updater...
echo.

REM Check if .NET 6.0 is installed
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: .NET 6.0 SDK is not installed.
    echo Please install .NET 6.0 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

REM Build the solution
echo Building solution...
dotnet build SpicetifyAutoUpdater.sln --configuration Release

if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Build completed successfully!
echo.
echo To run the application:
echo 1. Navigate to: SpicetifyAutoUpdater\bin\Release\net6.0-windows\
echo 2. Run: SpicetifyAutoUpdater.exe
echo.
echo Or press any key to run it now...
pause >nul

REM Run the application
echo Running Spicetify Auto Updater...
start "" "SpicetifyAutoUpdater\bin\Release\net6.0-windows\SpicetifyAutoUpdater.exe" 