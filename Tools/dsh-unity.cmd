@echo off
setlocal
rem Wrapper for dsh-unity.ps1 (bypasses the script execution policy).
rem Usage: Tools\dsh-unity.cmd ping
where pwsh >nul 2>nul
if %ERRORLEVEL%==0 (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0dsh-unity.ps1" %*
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0dsh-unity.ps1" %*
)
exit /b %ERRORLEVEL%
