@echo off
setlocal
cd /d "%~dp0"
set OUT=newlife-iot-platform-1.0.3.tar.gz
copy /b %OUT%.part00 + %OUT%.part01 + %OUT%.part02 %OUT%
if errorlevel 1 exit /b 1
docker load -i %OUT%
if errorlevel 1 exit /b 1
echo Loaded newlife-iot-platform:1.0.3 (and :latest).
echo Run: docker compose up
echo   or: docker run --rm -p 5288:5288 -p 1883:1883 -v iot-data:/app/Data -v iot-logs:/app/Log newlife-iot-platform:1.0.3
