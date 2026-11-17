@echo off
title NexusExtract Enterprise Gateway Launcher
echo ==========================================================
echo  Starting NexusExtract Enterprise Gateway (.NET 10 + React 19)
echo ==========================================================

set "PATH=C:\Program Files\dotnet;%PATH%"
set "DOTNET_CLI_HOME=E:\2026 Learn\repos\.dotnet"
set "APPDATA=E:\2026 Learn\repos\.appdata"
set "NUGET_PACKAGES=E:\2026 Learn\repos\.nuget\packages"

echo.
echo [1/2] Launching ASP.NET Core Web API on http://localhost:5000...
echo  - Geneva Socket Mock Listener: Active on 127.0.0.1:9100
echo  - Swagger Documentation: http://localhost:5000/swagger
echo  - Health Check Probe: http://localhost:5000/health
start "NexusExtract Web API (.NET 10)" cmd /k "set PATH=C:\Program Files\dotnet;%%PATH%%&& set DOTNET_CLI_HOME=E:\2026 Learn\repos\.dotnet&& set APPDATA=E:\2026 Learn\repos\.appdata&& set NUGET_PACKAGES=E:\2026 Learn\repos\.nuget\packages&& cd /d ""E:\2026 Learn\repos\NexusExtractGateway\src\NexusExtract.Api""&& dotnet run --no-restore"

echo.
echo [2/2] Launching React 19 Executive UI on http://localhost:5173...
cd /d "E:\2026 Learn\repos\NexusExtractGateway\client"
call npm.cmd run dev
