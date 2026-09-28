@echo off
cd /d S:\???\usetime
set ELECTRON_MIRROR=https://npmmirror.com/mirrors/electron/
set ELECTRON_BUILDER_BINARIES_MIRROR=https://npmmirror.com/mirrors/electron-builder-binaries/
node node_modules\electron-builder\cli.js --win nsis > build-log-nsis.txt 2>&1
echo EXIT=%ERRORLEVEL% >> build-log-nsis.txt
