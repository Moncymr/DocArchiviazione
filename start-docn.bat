@echo off
REM ============================================================================
REM DocN - Document Archive System Startup Script (Windows)
REM ============================================================================
REM This script starts both the Server (Backend API) and Client (Frontend UI)
REM ============================================================================

echo.
echo ============================================================================
echo    DocN - Document Archive System
echo ============================================================================
echo.
echo Starting the DocN System...
echo.
echo This will start:
echo   1. DocN.Server (Backend API) on https://localhost:5211
echo   2. DocN.Client (Frontend UI) on http://localhost:5036
echo.
echo Press Ctrl+C in this window to stop both applications
echo.
echo ============================================================================
echo.

REM Check if .NET is installed
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: .NET SDK is not installed or not in PATH
    echo Please install .NET 10.0 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

REM Start the Server in a new window
echo [1/2] Starting DocN.Server (Backend API)...
start "DocN.Server - Backend API" cmd /k "cd DocN.Server && dotnet run"

REM Wait a few seconds for the server to start
echo Waiting for Server to initialize...
timeout /t 10 /nobreak >nul

REM Start the Client in a new window
echo [2/2] Starting DocN.Client (Frontend UI)...
start "DocN.Client - Frontend UI" cmd /k "cd DocN.Client && dotnet run"

echo.
echo ============================================================================
echo  APPLICATIONS STARTED!
echo ============================================================================
echo.
echo   Server (API):   https://localhost:5211
echo   Client (UI):    http://localhost:5036
echo.
echo   Open your browser to: http://localhost:5036
echo.
echo   To stop the applications, close both command windows
echo   or press Ctrl+C in each window.
echo.
echo ============================================================================
echo.

REM Wait for user input before closing this window
pause
