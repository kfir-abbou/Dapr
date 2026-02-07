# Clear all Redis data before starting services
# This removes all workflow state and pending pub/sub messages

Write-Host "Clearing Redis data..." -ForegroundColor Yellow
docker exec dapr_redis redis-cli FLUSHALL

Write-Host "Redis cleared. Starting services..." -ForegroundColor Green
& "$PSScriptRoot\run-services.ps1"
