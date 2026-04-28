@echo off
echo ========================================
echo    Winter Road Game Server Starter (DEBUG)
echo ========================================
echo.

REM Change to current directory
cd /d "%~dp0"
echo [DEBUG] Current directory: %CD%

REM Check if Node.js is installed
echo [DEBUG] Checking Node.js installation...
node --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Node.js is not installed!
    echo Please install Node.js first: https://nodejs.org/
    pause
    exit /b 1
) else (
    echo [DEBUG] Node.js found: 
    node --version
)

REM Check if server.js exists
echo [DEBUG] Checking for server.js...
if not exist "server.js" (
    echo [ERROR] server.js not found in current directory!
    echo [DEBUG] Current directory contents:
    dir
    pause
    exit /b 1
) else (
    echo [DEBUG] server.js found
)

REM Check if config.json exists
echo [DEBUG] Checking for config.json...
if not exist "config.json" (
    echo [ERROR] config.json not found!
    pause
    exit /b 1
) else (
    echo [DEBUG] config.json found
)

REM Check if required packages are installed
REM 폴더 존재 여부만이 아니라 핵심 모듈(express)이 실제로 있는지 검사.
REM (이전 버전은 node_modules 폴더만 있고 내용물이 일부 빠진 상태에서도 설치를 건너뛰어
REM  Cannot find module 'express' 에러가 발생하는 함정이 있었음)
echo [DEBUG] Checking for node_modules...
if not exist "node_modules\express" (
    echo [DEBUG] node_modules incomplete or missing - running npm install...
    npm install
    if errorlevel 1 (
        echo [ERROR] Failed to install packages!
        echo [DEBUG] NPM Error occurred
        pause
        exit /b 1
    )
) else (
    echo [DEBUG] node_modules found
)

REM Kill any existing processes on port 3000
echo [DEBUG] Killing any existing processes on port 3000...
echo [DEBUG] Killing any existing server processes on port 3000...
for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr :3000 ^| findstr LISTENING') do (
    echo [DEBUG] Killing server process ID %%a
    taskkill /PID %%a /F >nul 2>&1
)

REM Wait a moment
timeout /t 2 >nul

echo [DEBUG] Starting server with verbose output...
echo [DEBUG] Command: node server.js
echo ========================================
echo.

REM Start server with error handling
node server.js
set SERVER_EXIT_CODE=%errorlevel%

echo.
echo ========================================
echo [DEBUG] Server process ended with exit code: %SERVER_EXIT_CODE%

if %SERVER_EXIT_CODE% neq 0 (
    echo [ERROR] Server failed to start or crashed!
    echo [DEBUG] Common solutions:
    echo 1. Check if port 3000 is available
    echo 2. Run as administrator
    echo 3. Check config.json settings
    echo 4. Verify all dependencies are installed
) else (
    echo [INFO] Server stopped normally
)

echo.
echo Press any key to exit...
pause >nul

