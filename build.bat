@echo off
echo ========================================
echo  Building DualKey for Windows
echo ========================================
echo.

:: Check for .NET SDK
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK not found.
    echo Please install .NET 8.0 SDK from:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [INFO] .NET SDK found: 
dotnet --version
echo.

:: Build the project
echo [INFO] Building DualKey...
cd src\windows
dotnet build -c Release -o ..\..\build
if errorlevel 1 (
    echo [ERROR] Build failed.
    cd ..\..
    pause
    exit /b 1
)
cd ..\..

echo.
echo ========================================
echo  Build completed successfully!
echo  Output: build\DualKey.exe
echo ========================================
pause
