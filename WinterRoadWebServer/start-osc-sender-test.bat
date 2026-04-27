@echo off
chcp 65001 > nul
title OSC Sender Test

echo ========================================
echo 🚀 Starting OSC Sender Test
echo ========================================
echo.
echo Target: 127.0.0.1:50013
echo.
echo Make sure OSC Receiver is running first!
echo (Run start-osc-receiver.bat in another window)
echo.
pause

node test-osc-sender.js

echo.
echo ========================================
echo Test completed!
echo ========================================
pause







