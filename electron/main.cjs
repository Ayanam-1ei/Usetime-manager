const { app, BrowserWindow, ipcMain, nativeImage } = require('electron');
const path = require('path');
const fs = require('fs');
const { startTracker, stopTracker, getTrackerStatus } = require('./tracker.cjs');
const db = require('./db.cjs');
const { extractIcon } = require('./icons.cjs');
const { enableAutostart, disableAutostart, isAutostartEnabled } = require('./autostart.cjs');

const logFile = process.env.USETIME_LOG || path.join(process.cwd(), 'run.log');
function log(...args) {
  const line = args.map((a) => (typeof a === 'string' ? a : JSON.stringify(a))).join(' ');
  try {
    fs.appendFileSync(logFile, line + '\n');
  } catch {
    /* ignore */
  }
  console.log(...args);
}
function logErr(...args) {
  log('[error]', ...args);
}

// 自检截图时不必禁用 GPU（此前 0x0 空图与 GPU 无关）


// 仅当显式开启 VITE_DEV_SERVER=1 时连本地 Vite，否则加载 dist 构建产物
const isDev = process.env.VITE_DEV_SERVER === '1';
let mainWindow = null;
let isQuitting = false;

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 880,
    height: 860,
    minWidth: 720,
    minHeight: 640,
    show: true,
    title: '屏幕使用时长统计',
    backgroundColor: '#f2f3f7',
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
    },
  });

  mainWindow.setMenuBarVisibility(false);

  mainWindow.webContents.on('console-message', (_e, level, message) => {
    log('[renderer]', level, message);
  });

  mainWindow.webContents.on('did-fail-load', (_e, code, desc) => {
    logErr('[load-fail]', code, desc);
  });

  // 自检截图：便于无界面环境验证 UI（写入项目 preview.png）
  mainWindow.webContents.on('did-finish-load', () => {
    log('[load] did-finish-load');
    if (process.env.USETIME_CAPTURE === '1') {
      setTimeout(async () => {
        try {
          const text = await mainWindow.webContents.executeJavaScript(
            'document.body ? document.body.innerText.slice(0, 2000) : \"(no body)\"'
          );
          log('[dom]', JSON.stringify(text));
          const htmlLen = await mainWindow.webContents.executeJavaScript(
            'document.getElementById(\"root\") ? document.getElementById(\"root\").innerHTML.length : -1'
          );
          log('[root-html-len]', htmlLen);
        } catch (e) {
          logErr('[dom] failed', e && e.message ? e.message : e);
        }
        try {
          const probe = await mainWindow.webContents.executeJavaScript(`
            (() => {
              const q = (s) => {
                const el = document.querySelector(s);
                if (!el) return null;
                const r = el.getBoundingClientRect();
                const cs = getComputedStyle(el);
                return {
                  w: Math.round(r.width), h: Math.round(r.height),
                  display: cs.display, visibility: cs.visibility, opacity: cs.opacity,
                  color: cs.color, bg: cs.backgroundColor
                };
              };
              return {
                body: q('body'),
                root: q('#root'),
                app: q('.app'),
                title: q('.app-title'),
                card: q('.card'),
              };
            })()
          `);
          log('[probe]', JSON.stringify(probe));
        } catch (e) {
          logErr('[probe] failed', e && e.message ? e.message : e);
        }
        try {
          mainWindow.show();
          mainWindow.focus();
          mainWindow.setAlwaysOnTop(true);
          mainWindow.setBounds({ x: 50, y: 50, width: 880, height: 860 });
          await new Promise((r) => setTimeout(r, 800));
          const image = await mainWindow.webContents.capturePage();
          const buf = image.toPNG();
          const p = path.join(app.getAppPath(), 'preview.png');
          fs.writeFileSync(p, buf);
          log('[capture] saved', p, 'bytes', buf.length, 'size', image.getSize());
          mainWindow.setAlwaysOnTop(false);
        } catch (e) {
          logErr('[capture] failed', e && e.message ? e.message : e);
        }
        if (process.env.USETIME_CAPTURE_EXIT === '1') {
          isQuitting = true;
          stopTracker();
          db.close();
          app.quit();
        }
      }, 2000);
    }
  });

  if (isDev) {
    mainWindow.loadURL('http://localhost:5173');
  } else {
    mainWindow.loadFile(path.join(__dirname, '../dist/index.html'));
  }

  // 关闭窗口时隐藏，保持后台采集；真正退出走应用退出逻辑
  mainWindow.on('close', (e) => {
    if (!isQuitting && process.env.USETIME_ALLOW_CLOSE !== '1') {
      e.preventDefault();
      mainWindow.hide();
    }
  });

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

function getMainWindow() {
  return mainWindow;
}

// ---- IPC ----
ipcMain.handle('stats:summary', (_e, period, dateStr) => {
  return db.getSummary(period, dateStr);
});

ipcMain.handle('stats:chart', (_e, period, dateStr) => {
  return db.getChartData(period, dateStr);
});

ipcMain.handle('stats:apps', (_e, period, dateStr) => {
  return db.getAppUsage(period, dateStr);
});

ipcMain.handle('app:show', () => {
  if (mainWindow) {
    mainWindow.show();
    mainWindow.focus();
  }
});

ipcMain.handle('app:hide', () => {
  if (mainWindow) mainWindow.hide();
});

ipcMain.handle('app:quit', () => {
  isQuitting = true;
  stopTracker();
  app.quit();
});

ipcMain.handle('app:getIcon', async (_e, exePath) => {
  try {
    return await extractIcon(exePath);
  } catch {
    return null;
  }
});

ipcMain.handle('tracker:status', () => getTrackerStatus());

ipcMain.handle('autostart:get', () => isAutostartEnabled());

ipcMain.handle('autostart:set', (_e, enabled) => {
  if (enabled) return enableAutostart();
  return disableAutostart();
});

// ---- lifecycle ----
const gotLock = app.requestSingleInstanceLock();
if (!gotLock) {
  app.quit();
} else {
  app.on('second-instance', () => {
    if (mainWindow) {
      if (mainWindow.isMinimized()) mainWindow.restore();
      mainWindow.show();
      mainWindow.focus();
    }
  });

  app.whenReady().then(() => {
    db.init();
    startTracker();
    createWindow();
  });

  app.on('before-quit', () => {
    isQuitting = true;
    stopTracker();
    db.close();
  });

  app.on('window-all-closed', () => {
    // 后台继续采集，不退出
    if (isQuitting) app.quit();
  });

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    } else if (mainWindow) {
      mainWindow.show();
    }
  });
}
