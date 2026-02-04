# Run ServiceC with Dapr
# Ensure you've installed Python dependencies: pip install -r requirements.txt

$ErrorActionPreference = "Stop"

Write-Host "Starting ServiceC (Python FastAPI) with Dapr..." -ForegroundColor Cyan

Set-Location -Path $PSScriptRoot\ServiceC

# Run with Dapr sidecar
dapr run `
    --app-id servicec `
    --app-port 5003 `
    --dapr-http-port 3503 `
    --dapr-grpc-port 50003 `
    --resources-path ../components `
    --config ../config.yaml `
    -- python -m uvicorn main:app --host 0.0.0.0 --port 5003
