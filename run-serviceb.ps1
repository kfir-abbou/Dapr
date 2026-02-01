# PowerShell script to run ServiceB individually with Dapr
# Prerequisites: Dapr CLI installed and initialized (dapr init)

Write-Host "Starting ServiceB with Dapr..." -ForegroundColor Green

Set-Location -Path $PSScriptRoot\ServiceB

D:\Dapr\dapr.exe run `
    --app-id serviceb `
    --app-port 5002 `
    --dapr-http-port 3502 `
    --dapr-grpc-port 50002 `
    --resources-path ../components `
    -- dotnet run
