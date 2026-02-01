# PowerShell script to run ServiceA individually with Dapr
# Prerequisites: Dapr CLI installed and initialized (dapr init)

Write-Host "Starting ServiceA with Dapr..." -ForegroundColor Green

Set-Location -Path $PSScriptRoot\ServiceA

D:\Dapr\dapr.exe run `
    --app-id servicea `
    --app-port 5001 `
    --dapr-http-port 3501 `
    --dapr-grpc-port 50001 `
    --resources-path ../components `
    -- dotnet run
