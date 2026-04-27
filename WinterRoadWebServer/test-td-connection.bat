@echo off
chcp 65001 > nul
title Test TD Server Connection

echo ========================================
echo 🌐 TD Server Connection Test
echo ========================================
echo.

REM TD 서버 IP와 포트 설정
set TD_HOST=192.168.0.13
set TD_PORT=50013

echo Target TD Server: %TD_HOST%:%TD_PORT%
echo.
echo This will test:
echo   1. Network connectivity (Ping)
echo   2. OSC connection ability
echo   3. Send test message to TD server
echo.
echo Press any key to start test...
pause > nul
echo.

node test-network-connection.js %TD_HOST% %TD_PORT%

echo.
echo ========================================
echo Test completed!
echo ========================================
pause







