@echo off
REM Launches Decompile-Bannerlord.ps1 with ExecutionPolicy Bypass.
REM Prefers PowerShell 7 (pwsh) if available, falls back to Windows PowerShell (powershell).
pushd "%~dp0"
where pwsh >nul 2>nul
if %ERRORLEVEL%==0 (
    pwsh -NoProfile -ExecutionPolicy Bypass -File ".\Decompile-Bannerlord.ps1" %*
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File ".\Decompile-Bannerlord.ps1" %*
)
set EXITCODE=%ERRORLEVEL%
popd
echo.
echo === Press any key to close ===
pause >nul
exit /b %EXITCODE%
