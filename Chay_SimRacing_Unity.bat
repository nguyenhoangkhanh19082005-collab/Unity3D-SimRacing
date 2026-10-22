@echo off
cd /d "%~dp0"
set "GAME=SimRacing-Windows\SimRacing.exe"
if not exist "%GAME%" set "GAME=Builds\Windows\SimRacing.exe"
if not exist "%GAME%" (
  echo Khong tim thay ban choi SimRacing-Windows\SimRacing.exe.
  echo Hay chay: git pull  ^(hoac clone lai repo^).
  pause
  exit /b 1
)
for %%I in ("%GAME%") do if %%~zI LSS 100000 (
  echo Ban choi chua duoc tai day du ^(file Git LFS moi la con tro^).
  echo Cai Git LFS ^(https://git-lfs.com^), roi chay trong thu muc nay:
  echo     git lfs install
  echo     git lfs pull
  pause
  exit /b 1
)
start "" "%GAME%"
