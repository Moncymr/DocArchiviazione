# ============================================================================
# DocN - Document Archive System Startup Script (PowerShell - Cross-platform)
# ============================================================================
# This script starts both the Server (Backend API) and Client (Frontend UI)
# Works on Windows, Linux, and macOS
# ============================================================================

Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "   DocN - Document Archive System" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Starting the DocN System..." -ForegroundColor Yellow
Write-Host ""
Write-Host "This will start:" -ForegroundColor White
Write-Host "  1. DocN.Server (Backend API) on https://localhost:5211" -ForegroundColor White
Write-Host "  2. DocN.Client (Frontend UI) on http://localhost:5036" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C to stop both applications" -ForegroundColor Red
Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Check if .NET is installed
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: .NET SDK is not installed or not in PATH" -ForegroundColor Red
    Write-Host "Please install .NET 10.0 SDK from https://dotnet.microsoft.com/download" -ForegroundColor Red
    exit 1
}

# Function to cleanup background jobs on exit
function Cleanup {
    Write-Host ""
    Write-Host "Shutting down applications..." -ForegroundColor Yellow
    Get-Job | Stop-Job
    Get-Job | Remove-Job
    Write-Host "Applications stopped." -ForegroundColor Green
}

# Register cleanup on script exit
Register-EngineEvent PowerShell.Exiting -Action { Cleanup }

try {
    # Start the Server
    Write-Host "[1/2] Starting DocN.Server (Backend API)..." -ForegroundColor Green
    $serverJob = Start-Job -ScriptBlock {
        Set-Location DocN.Server
        dotnet run
    }
    
    # Wait for Server to initialize
    Write-Host "Waiting for Server to initialize..." -ForegroundColor Yellow
    Start-Sleep -Seconds 10
    
    # Start the Client
    Write-Host "[2/2] Starting DocN.Client (Frontend UI)..." -ForegroundColor Green
    $clientJob = Start-Job -ScriptBlock {
        Set-Location DocN.Client
        dotnet run
    }
    
    Write-Host ""
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host " APPLICATIONS STARTED!" -ForegroundColor Green
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Server (API):   https://localhost:5211" -ForegroundColor White
    Write-Host "  Client (UI):    http://localhost:5036" -ForegroundColor White
    Write-Host ""
    Write-Host "  Open your browser to: " -NoNewline -ForegroundColor White
    Write-Host "http://localhost:5036" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Press Ctrl+C to stop both applications" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Monitor the jobs and display their output
    Write-Host "Application logs:" -ForegroundColor Yellow
    Write-Host ""
    
    # Wait for jobs and display output
    while ($serverJob.State -eq 'Running' -or $clientJob.State -eq 'Running') {
        # Display Server output
        if ($serverJob.State -eq 'Running') {
            $serverOutput = Receive-Job -Job $serverJob
            if ($serverOutput) {
                Write-Host "[SERVER] " -NoNewline -ForegroundColor Blue
                Write-Host $serverOutput
            }
        }
        
        # Display Client output
        if ($clientJob.State -eq 'Running') {
            $clientOutput = Receive-Job -Job $clientJob
            if ($clientOutput) {
                Write-Host "[CLIENT] " -NoNewline -ForegroundColor Magenta
                Write-Host $clientOutput
            }
        }
        
        Start-Sleep -Seconds 1
    }
}
finally {
    Cleanup
}
