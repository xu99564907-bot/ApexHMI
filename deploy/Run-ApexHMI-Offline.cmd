@echo off
:: Optional launcher for a copied .NET Framework 4.8 deployment folder.
:: Double-clicking ApexHMI.exe is enough after .NET Framework 4.8 is installed.
setlocal
set "APP_DIR=%~dp0"
if exist "%APP_DIR%ApexHMI.exe" (
  start "" "%APP_DIR%ApexHMI.exe"
  exit /b 0
)
echo [ERROR] ApexHMI.exe not found next to this script: %APP_DIR%
pause
exit /b 1
