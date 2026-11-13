@echo off
rem Opens UDP 9999 in Windows Firewall and sets the PC's LAN address for the ESP32 control box.
set "PS1=%~dp0Thiet_Lap_Mang_Buong_Lai.ps1"
if not exist "%PS1%" set "PS1=%~dp0Tools\Thiet_Lap_Mang_Buong_Lai.ps1"
if not exist "%PS1%" (
  echo Khong tim thay Thiet_Lap_Mang_Buong_Lai.ps1
  pause
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS1%"
