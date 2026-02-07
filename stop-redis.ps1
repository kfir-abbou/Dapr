# Stop Redis container

$ErrorActionPreference = "SilentlyContinue"

Write-Host "Stopping Redis container..." -ForegroundColor Cyan

$runningContainer = docker ps --filter "name=^dapr-redis$" --format "{{.Names}}"

if ($runningContainer -eq "dapr-redis") {
    docker stop dapr-redis
    Write-Host "Redis container stopped!" -ForegroundColor Green
}
else {
    Write-Host "Redis container is not running." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Note: Container still exists. To remove it completely:" -ForegroundColor Gray
Write-Host "      docker rm dapr-redis" -ForegroundColor Gray
