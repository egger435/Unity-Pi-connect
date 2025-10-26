@echo off

set "FRP_VERSION=0.65.0"                 
set "FRP_DIR=%~dp0frp_%FRP_VERSION%"

if exist "%FRP_DIR%" (
    echo Starting FRP client...
    cd /d "%FRP_DIR%\frp_%FRP_VERSION%_windows_amd64"
    .\frpc.exe -c frpc.toml
)

echo Configuring FRP client, Version: %FRP_VERSION%
echo.

echo Downloading FRP...
set "FRP_URL=https://github.com/fatedier/frp/releases/download/v%FRP_VERSION%/frp_%FRP_VERSION%_windows_amd64.zip"
set "TEMP_ZIP=%TEMP%\frp_temp.zip"

bitsadmin /transfer "FRPDownload" /download /priority normal "%FRP_URL%" "%TEMP_ZIP%"
if %errorlevel% neq 0 (
    echo Download failed
    pause
    exit
)
echo Download completed

echo Unpacking FRP...
mkdir "%FRP_DIR%" 2>nul
tar -xf "%TEMP_ZIP%" -C "%FRP_DIR%"
if %errorlevel% neq 0 (
    echo Unpacking failed. Please ensure that the 'tar' command is installed on your system
    pause
    exit
)
del "%TEMP_ZIP%"
echo Unpacking completed, dir: %FRP_DIR%

echo Generating configuration file...
set "INI_FILE=%FRP_DIR%\frp_%FRP_VERSION%_windows_amd64\frpc.toml"
type nul > "%INI_FILE%"
echo serverAddr = "服务器公网ip" >> "%INI_FILE%"
echo serverPort = xxxx >> "%INI_FILE%"  
echo auth.token = "神奇小密码" >> "%INI_FILE%"

echo. >> "%INI_FILE%"
echo [[proxies]] >> "%INI_FILE%"
echo name = "udp-receive" >> "%INI_FILE%"
echo type = "udp" >> "%INI_FILE%"
echo localIP = "127.0.0.1" >> "%INI_FILE%"
echo localPort = xxxxx >> "%INI_FILE%"
echo remotePort = xxxxx >> "%INI_FILE%"

echo Generation completed: %INI_FILE%

echo Starting FRP client...
type nul > "frpcflag"
cd /d "%FRP_DIR%\frp_%FRP_VERSION%_windows_amd64"
.\frpc.exe -c frpc.toml

echo FRP start.
echo configuration completed. If you want to exit the client, please close "frpc_start" window.
pause
exit