@echo off
cd /d S:\可视化\usetime
set ELECTRON_MIRROR=https://npmmirror.com/mirrors/electron/
set ELECTRON_BUILDER_BINARIES_MIRROR=https://npmmirror.com/mirrors/electron-builder-binaries/
node node_modules\electron-builder\cli.js --win portable > build-log.txt 2>&1
echo EXIT=%ERRORLEVEL% >> build-log.txt
