@echo off
REM ========================================
REM Disable QuickEdit Mode for PowerShell
REM This prevents console from freezing when clicked
REM ========================================

echo ========================================
echo   QuickEdit Mode Disabler
echo ========================================
echo.
echo This script will disable QuickEdit Mode for PowerShell
echo to prevent the server from freezing when you click the console.
echo.
echo This requires Administrator privileges.
echo.
pause

REM Check for admin privileges
net session >nul 2>&1
if errorlevel 1 (
    echo [ERROR] This script requires Administrator privileges!
    echo Please right-click and select "Run as administrator"
    pause
    exit /b 1
)

echo [INFO] Disabling QuickEdit Mode for PowerShell...

REM Disable QuickEdit Mode in Registry
reg add "HKCU\Console\%%SystemRoot%%_System32_WindowsPowerShell_v1.0_powershell.exe" /v QuickEdit /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\Console\%%SystemRoot%%_SysWOW64_WindowsPowerShell_v1.0_powershell.exe" /v QuickEdit /t REG_DWORD /d 0 /f >nul 2>&1
reg add "HKCU\Console" /v QuickEdit /t REG_DWORD /d 0 /f >nul 2>&1

echo [SUCCESS] QuickEdit Mode has been disabled!
echo.
echo Please close all PowerShell windows and restart them.
echo The server will no longer freeze when you click the console.
echo.
pause








