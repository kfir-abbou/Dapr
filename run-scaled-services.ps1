# Run all services with multiple ServiceB instances
# Requires: Redis running (use start-redis.ps1 first)

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Dapr Scaled Services Launcher" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if Redis is running
Write-Host "Checking Redis..." -ForegroundColor Yellow
try {
    $redisRunning = docker ps --filter "name=^dapr-redis$" --format "{{.Names}}" 2>$null
    if ($redisRunning -ne "dapr-redis") {
        Write-Host "Redis is not running. Starting it now..." -ForegroundColor Yellow
        & "$PSScriptRoot\start-redis.ps1"
        Start-Sleep -Seconds 2
    }
    else {
        Write-Host "Redis is running!" -ForegroundColor Green
    }
}
catch {
    Write-Host "WARNING: Could not check Redis status. Make sure Docker is running." -ForegroundColor Yellow
    Write-Host "Run .\start-redis.ps1 manually if needed." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Starting Dapr Multi-App Run..." -ForegroundColor Cyan
Write-Host ""
Write-Host "Services:" -ForegroundColor White
Write-Host "  - ServiceA:   http://localhost:5001 (Dapr: 3500)" -ForegroundColor Gray
Write-Host "  - ServiceB-1: http://localhost:5002 (Dapr: 3502)" -ForegroundColor Gray
Write-Host "  - ServiceB-2: http://localhost:5012 (Dapr: 3512)" -ForegroundColor Gray
Write-Host "  - ServiceB-3: http://localhost:5022 (Dapr: 3522)" -ForegroundColor Gray
Write-Host "  - ServiceC:   http://localhost:5003 (Dapr: 3503)" -ForegroundColor Gray
Write-Host ""
Write-Host "Press Ctrl+C to stop all services" -ForegroundColor Yellow
Write-Host ""

Set-Location -Path $PSScriptRoot
dapr run -f dapr.yaml
