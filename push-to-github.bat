@echo off
setlocal
set "PATH=E:\2026 Learn\repos\MinGit\cmd;%PATH%"
cd /d "E:\2026 Learn\repos\NexusExtractGateway"

echo ============================================================
echo   NexusExtract Gateway - Pushing Code to GitHub
echo   Target: https://github.com/sanketkambli/NexusExtract
echo ============================================================
echo.
git push -u origin main
echo.
pause
