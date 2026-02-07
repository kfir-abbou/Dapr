# Clear all Redis data before starting services
# This removes all workflow state and pending pub/sub messages

Write-Host "Clearing Redis data..." -ForegroundColor Yellow
# Try both container names (dapr_redis and dapr-redis)
docker exec dapr_redis redis-cli FLUSHALL 2>$null
docker exec dapr-redis redis-cli FLUSHALL 2>$null

Write-Host "Redis cleared. Starting services..." -ForegroundColor Green
& "$PSScriptRoot\run-services.ps1"
