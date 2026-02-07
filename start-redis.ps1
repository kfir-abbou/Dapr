# Start Redis container for Dapr state store and pub/sub
# Requires Docker to be installed and running

$ErrorActionPreference = "Stop"

Write-Host "Starting Redis container..." -ForegroundColor Cyan

# Check if Docker is running
try {
    docker info | Out-Null
}
catch {
    Write-Host "ERROR: Docker is not running. Please start Docker Desktop first." -ForegroundColor Red
    exit 1
}

# Check if redis container already exists
$existingContainer = docker ps -a --filter "name=^dapr-redis$" --format "{{.Names}}"

if ($existingContainer -eq "dapr-redis") {
    # Container exists, check if it's running
    $runningContainer = docker ps --filter "name=^dapr-redis$" --format "{{.Names}}"
    
    if ($runningContainer -eq "dapr-redis") {
        Write-Host "Redis container is already running!" -ForegroundColor Green
    }
    else {
        Write-Host "Starting existing Redis container..." -ForegroundColor Yellow
        docker start dapr-redis
        Write-Host "Redis container started!" -ForegroundColor Green
    }
}
else {
    # Create new container
    Write-Host "Creating new Redis container..." -ForegroundColor Yellow
    docker run -d --name dapr-redis -p 6379:6379 redis:alpine
    Write-Host "Redis container created and started!" -ForegroundColor Green
}

Write-Host ""
Write-Host "Redis is running on localhost:6379" -ForegroundColor Cyan
Write-Host ""
Write-Host "To verify: docker ps | Select-String redis" -ForegroundColor Gray
Write-Host "To stop:   .\stop-redis.ps1" -ForegroundColor Gray
