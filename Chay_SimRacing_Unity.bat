@echo off
cd /d "%~dp0"
if not exist "Builds\Windows\SimRacing.exe" (
  echo Chua co ban Windows. Mo du an trong Unity va chon SimRacing/Build Windows.
  pause
  exit /b 1
)
start "" "Builds\Windows\SimRacing.exe"
