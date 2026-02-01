# PowerShell script to run both services with Dapr
# Prerequisites: Dapr CLI installed and initialized (dapr init)

Write-Host "Starting Dapr Multi-App Run..." -ForegroundColor Green
Write-Host "Make sure Dapr is initialized: dapr init" -ForegroundColor Yellow
Write-Host ""

# Run both services using dapr multi-app run
D:\Dapr\dapr.exe run -f .

