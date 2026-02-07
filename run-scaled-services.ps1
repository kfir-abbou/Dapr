# Run all services with multiple ServiceB instances
# Requires: Redis running (use start-redis.ps1 first)
# Usage: .\run-scaled-services.ps1 [-Clean]

param(
    [switch]$Clean
)

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

# Clear Redis if -Clean flag is specified
if ($Clean) {
    Write-Host ""
    Write-Host "Clearing Redis data (workflows, state, pub/sub messages)..." -ForegroundColor Yellow
    # Try both container names (dapr_redis and dapr-redis)
    docker exec dapr_redis redis-cli FLUSHALL 2>$null | Out-Null
    docker exec dapr-redis redis-cli FLUSHALL 2>$null | Out-Null
    Write-Host "Redis cleared!" -ForegroundColor Green
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
Write-Host "Tip: Use -Clean flag to clear Redis before starting (removes old messages)" -ForegroundColor DarkGray
Write-Host "Press Ctrl+C to stop all services" -ForegroundColor Yellow
Write-Host ""

Set-Location -Path $PSScriptRoot
dapr run -f dapr.yaml
