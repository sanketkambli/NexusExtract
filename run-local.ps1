# NexusExtract Gateway - Local Launch Script (Dual-Engine Execution)
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Starting NexusExtract Enterprise Gateway (.NET 10 + React 19)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$env:PATH = "C:\Program Files\dotnet;" + $env:PATH
$env:DOTNET_CLI_HOME = "E:\2026 Learn\repos\.dotnet"
$env:APPDATA = "E:\2026 Learn\repos\.appdata"
$env:NUGET_PACKAGES = "E:\2026 Learn\repos\.nuget\packages"

Write-Host "`n[1/2] Launching ASP.NET Core Web API on http://localhost:5000..." -ForegroundColor Green
Write-Host " - Geneva Socket Mock Listener: Active on 127.0.0.1:9100" -ForegroundColor Gray
Write-Host " - Swagger Documentation: http://localhost:5000/swagger" -ForegroundColor Gray
Write-Host " - Health Check Probe: http://localhost:5000/health" -ForegroundColor Gray

Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$env:PATH = 'C:\Program Files\dotnet;' + `$env:PATH; `$env:DOTNET_CLI_HOME = 'E:\2026 Learn\repos\.dotnet'; `$env:APPDATA = 'E:\2026 Learn\repos\.appdata'; `$env:NUGET_PACKAGES = 'E:\2026 Learn\repos\.nuget\packages'; Set-Location 'E:\2026 Learn\repos\NexusExtractGateway\src\NexusExtract.Api'; dotnet run --no-restore"

Write-Host "`n[2/2] Launching React 19 Executive UI on http://localhost:5173..." -ForegroundColor Green
Set-Location "E:\2026 Learn\repos\NexusExtractGateway\client"
npm.cmd run dev
