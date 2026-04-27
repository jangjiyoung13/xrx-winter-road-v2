@echo off
chcp 65001 > nul
title OSC Receiver Test Server

echo ========================================
echo 🎵 Starting OSC Receiver Test Server
echo ========================================
echo.
echo Port: 50013
echo Press Ctrl+C to stop
echo.

node test-osc-receiver.js

pause







