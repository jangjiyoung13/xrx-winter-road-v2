@echo off
echo ========================================
echo    Winter Road Game Server Stopper
echo ========================================
echo.

REM Change to current directory
cd /d "%~dp0"

echo [INFO] Looking for processes running on port 3000...

REM Find process using port 3000
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :3000') do (
    set pid=%%a
    goto :found
)

echo [INFO] No process found running on port 3000.
echo [INFO] Server might already be stopped or running on different port.
pause
exit /b 0

:found
echo [INFO] Terminating process ID %pid%...

REM Kill process
taskkill /PID %pid% /F >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Failed to terminate process!
    echo Try running as administrator.
) else (
    echo [SUCCESS] Server stopped successfully!
)

echo.
echo [INFO] Window will close in 3 seconds...
timeout /t 3 >nul
