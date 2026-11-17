# NexusExtract Gateway - GitHub Push Script
$env:PATH = "E:\2026 Learn\repos\MinGit\cmd;" + $env:PATH
Set-Location "E:\2026 Learn\repos\NexusExtractGateway"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  NexusExtract Gateway - Pushing Code to GitHub" -ForegroundColor Cyan
Write-Host "  Target: https://github.com/sanketkambli/NexusExtract" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

git push -u origin main
