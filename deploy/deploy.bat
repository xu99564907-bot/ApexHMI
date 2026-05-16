@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

REM ============================================================
REM  ApexHMI 部署脚本
REM  自动检查并安装 .NET 4.8 / WebView2 / Git，最后启动软件
REM ============================================================

set "ROOT=%~dp0"
set "DEPS=%ROOT%deps"
set "APEX=%ROOT%ApexHMI"

echo.
echo ========================================
echo   ApexHMI 自动部署
echo ========================================
echo 部署目录: %ROOT%
echo.

REM ---------- 1) 检查 .NET Framework 4.8 ----------
echo [1/4] 检查 .NET Framework 4.8...
set "NET48_OK="
for /f "tokens=3" %%a in ('reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release 2^>nul ^| findstr Release') do (
    if %%a GEQ 528040 set "NET48_OK=1"
)
if defined NET48_OK (
    echo     [OK] .NET 4.8 已安装
) else (
    echo     [缺失] 正在安装 .NET 4.8...
    if exist "%DEPS%\ndp48-x86-x64-allos-enu.exe" (
        "%DEPS%\ndp48-x86-x64-allos-enu.exe" /q /norestart
        echo     [完成] .NET 4.8 安装完毕，可能需要重启
    ) else (
        echo     [错误] 未找到 deps\ndp48-x86-x64-allos-enu.exe
        echo     请下载 .NET Framework 4.8 离线安装包到 deps 目录后重试：
        echo     https://dotnet.microsoft.com/download/dotnet-framework/net48
        pause
        exit /b 1
    )
)

REM ---------- 2) 检查 WebView2 Runtime ----------
echo.
echo [2/4] 检查 Edge WebView2 Runtime...
set "WV2_OK="
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv >nul 2>&1 && set "WV2_OK=1"
reg query "HKLM\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv >nul 2>&1 && set "WV2_OK=1"
if defined WV2_OK (
    echo     [OK] WebView2 Runtime 已安装
) else (
    echo     [缺失] 正在安装 WebView2 Runtime...
    if exist "%DEPS%\MicrosoftEdgeWebview2Setup.exe" (
        "%DEPS%\MicrosoftEdgeWebview2Setup.exe" /silent /install
        echo     [完成] WebView2 安装完毕
    ) else (
        echo     [警告] 未找到 deps\MicrosoftEdgeWebview2Setup.exe
        echo     HTML/PDF 控件将无法工作。下载地址：
        echo     https://developer.microsoft.com/microsoft-edge/webview2/
        echo     （此项可选，不影响其他功能）
    )
)

REM ---------- 3) Portable Git ----------
echo.
echo [3/4] 检查 Git...
where git >nul 2>&1
if %errorlevel% equ 0 (
    for /f "tokens=*" %%i in ('where git') do echo     [OK] 系统已安装 Git: %%i
) else (
    REM MinGit 用 cmd\git.exe；完整 PortableGit 用 bin\git.exe — 二选一
    set "GIT_DIR="
    if exist "%DEPS%\PortableGit\cmd\git.exe" set "GIT_DIR=%DEPS%\PortableGit\cmd"
    if exist "%DEPS%\PortableGit\bin\git.exe" set "GIT_DIR=%DEPS%\PortableGit\bin"
    if defined GIT_DIR (
        echo     [使用] 内置 PortableGit: !GIT_DIR!
        echo     正在把 git 路径加入 PATH...
        REM 1) setx 写入用户全局 PATH（持久化，新开命令行/重启电脑后仍有效）
        setx PATH "!GIT_DIR!;%PATH%" >nul
        REM 2) set 改当前 cmd 会话 PATH（让本次紧接着启动的 ApexHMI 立刻能看到 git，
        REM    否则 ApexHMI 作为本 bat 的子进程，继承的还是 setx 之前的旧 PATH）
        set "PATH=!GIT_DIR!;%PATH%"
        echo     [完成] 当前会话已生效；新开的命令行 / 重启后也可直接用 git
    ) else (
        echo     [警告] 未找到 deps\PortableGit\
        echo     ApexHMI 的"Git 代码同步"功能将无法工作。
        echo     下载 MinGit 解压到 deps\PortableGit\：
        echo     https://github.com/git-for-windows/git/releases/latest
        echo     （此项可选，不影响其他功能）
    )
)

REM ---------- 4) 启动 ApexHMI ----------
echo.
echo [4/4] 启动 ApexHMI...
echo.
if exist "%APEX%\ApexHMI.exe" (
    start "" "%APEX%\ApexHMI.exe"
    echo     [完成] ApexHMI 已启动
) else (
    echo     [错误] 未找到 ApexHMI\ApexHMI.exe
    pause
    exit /b 1
)

echo.
echo ========================================
echo   部署完成
echo ========================================
echo.
timeout /t 5 >nul
endlocal
