@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "APP_DIR=%~dp0"
set "APP_EXE=%APP_DIR%ApexHMI.exe"
set "RUNTIME_URL=https://go.microsoft.com/fwlink/?linkid=2088631"
set "LOCAL_INSTALLER=%APP_DIR%ndp48-x86-x64-allos-enu.exe"
set "RUNTIME_EXE=%TEMP%\ndp48-x86-x64-allos-enu.exe"

if not exist "%APP_EXE%" (
  echo [ERROR] ApexHMI.exe not found in: %APP_DIR%
  pause
  exit /b 1
)

call :check_runtime
if "%RUNTIME_OK%"=="1" goto run_app

echo [INFO] .NET Framework 4.8 not found. Installing...

if exist "%LOCAL_INSTALLER%" (
  set "INSTALLER=%LOCAL_INSTALLER%"
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -Command "try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -UseBasicParsing -Uri '%RUNTIME_URL%' -OutFile '%RUNTIME_EXE%' } catch { Write-Error $_; exit 1 }"
  if errorlevel 1 (
    echo [ERROR] Failed to download .NET Framework 4.8 installer.
    echo [HINT] For fully offline install, place ndp48-x86-x64-allos-enu.exe next to this script.
    pause
    exit /b 1
  )
  set "INSTALLER=%RUNTIME_EXE%"
)

echo [INFO] Running .NET Framework 4.8 installer (may require admin permission)...
start /wait "" "%INSTALLER%" /quiet /norestart
if errorlevel 1 (
  echo [ERROR] Runtime installation failed or was cancelled.
  pause
  exit /b 1
)

call :check_runtime
if "%RUNTIME_OK%"=="1" goto run_app

echo [ERROR] Runtime still not detected after installation.
pause
exit /b 1

:run_app
start "" "%APP_EXE%"
exit /b 0

:check_runtime
set "RUNTIME_OK=0"
for /f "tokens=3" %%R in ('reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release 2^>nul ^| findstr Release') do (
  set /a RELEASE=%%R
)
if defined RELEASE (
  if !RELEASE! GEQ 528040 set "RUNTIME_OK=1"
)
exit /b 0
