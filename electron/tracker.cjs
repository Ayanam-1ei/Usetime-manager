/**
 * 前台窗口采集：轮询当前活动窗口，合并连续同一程序会话，写入存储。
 */
const koffi = require('koffi');
const path = require('path');
const db = require('./db.cjs');

const POLL_MS = 2000;
const FLUSH_MS = 15000;

let api = null;
let timer = null;
let flushTimer = null;
let current = null; // { processName, exePath, windowTitle, startTs, endTs }
let running = false;

function initApi() {
  if (api) return true;
  try {
    const user32 = koffi.load('user32.dll');
    const kernel32 = koffi.load('kernel32.dll');
    api = {
      GetForegroundWindow: user32.func('void* __stdcall GetForegroundWindow()'),
      GetWindowThreadProcessId: user32.func(
        'uint32 __stdcall GetWindowThreadProcessId(void* hWnd, void* pidOut)'
      ),
      GetWindowTextLengthW: user32.func('int __stdcall GetWindowTextLengthW(void* hWnd)'),
      GetWindowTextW: user32.func(
        'int __stdcall GetWindowTextW(void* hWnd, void* buffer, int maxCount)'
      ),
      OpenProcess: kernel32.func(
        'void* __stdcall OpenProcess(uint32 access, int inherit, uint32 pid)'
      ),
      QueryFullProcessImageNameW: kernel32.func(
        'int __stdcall QueryFullProcessImageNameW(void* hProcess, uint32 flags, void* buffer, void* size)'
      ),
      CloseHandle: kernel32.func('int __stdcall CloseHandle(void* handle)'),
    };
    return true;
  } catch (err) {
    console.error('[tracker] Win32 API init failed:', err);
    api = null;
    return false;
  }
}

function getForegroundInfo() {
  if (!api) return null;
  try {
    const hwnd = api.GetForegroundWindow();
    if (!hwnd) return null;

    const pidOut = Buffer.alloc(4);
    api.GetWindowThreadProcessId(hwnd, pidOut);
    const pid = pidOut.readUInt32LE(0);
    if (!pid) return null;

    // window title
    let windowTitle = '';
    const titleLen = api.GetWindowTextLengthW(hwnd);
    if (titleLen > 0) {
      const tBuf = Buffer.alloc((titleLen + 1) * 2);
      const written = api.GetWindowTextW(hwnd, tBuf, titleLen + 1);
      if (written > 0) {
        windowTitle = tBuf.subarray(0, written * 2).toString('utf16le');
      }
    }

    // exe path
    let exePath = '';
    let processName = `pid-${pid}`;
    const hProc = api.OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, 0, pid);
    if (hProc) {
      try {
        const buf = Buffer.alloc(2048);
        const size = Buffer.alloc(4);
        size.writeUInt32LE(1024, 0);
        const ok = api.QueryFullProcessImageNameW(hProc, 0, buf, size);
        if (ok) {
          const chars = size.readUInt32LE(0);
          exePath = buf.subarray(0, chars * 2).toString('utf16le');
        }
      } finally {
        api.CloseHandle(hProc);
      }
    }

    if (exePath) {
      processName = path.basename(exePath).replace(/\.exe$/i, '') || processName;
    }

    return { processName, exePath, windowTitle, pid };
  } catch (err) {
    console.error('[tracker] sample failed:', err);
    return null;
  }
}

function sample() {
  const now = Date.now();
  const info = getForegroundInfo();
  if (!info) return;

  const isSame =
    current &&
    current.processName === info.processName &&
    current.exePath === info.exePath;

  if (isSame) {
    current.windowTitle = info.windowTitle || current.windowTitle;
    current.endTs = now;
    return;
  }

  if (current) {
    const duration = current.endTs - current.startTs;
    if (duration >= 1000) {
      db.addSession({
        startTs: current.startTs,
        endTs: current.endTs,
        processName: current.processName,
        exePath: current.exePath,
        windowTitle: current.windowTitle,
      });
    }
  }

  current = {
    processName: info.processName,
    exePath: info.exePath,
    windowTitle: info.windowTitle,
    startTs: now,
    endTs: now,
  };
}

function flushCurrent() {
  if (!current) return;
  const duration = current.endTs - current.startTs;
  if (duration < 500) return;
  db.addSession({
    startTs: current.startTs,
    endTs: current.endTs,
    processName: current.processName,
    exePath: current.exePath,
    windowTitle: current.windowTitle,
  });
  current.startTs = current.endTs;
}

function startTracker() {
  if (running) return;
  if (!initApi()) {
    console.error('[tracker] cannot start, API unavailable');
    return;
  }
  running = true;
  sample();
  timer = setInterval(sample, POLL_MS);
  flushTimer = setInterval(flushCurrent, FLUSH_MS);
  console.log('[tracker] started, poll', POLL_MS, 'ms');
}

function stopTracker() {
  if (!running) return;
  running = false;
  if (timer) clearInterval(timer);
  if (flushTimer) clearInterval(flushTimer);
  timer = null;
  flushTimer = null;
  if (current) {
    const duration = current.endTs - current.startTs;
    if (duration >= 500) {
      db.addSession({
        startTs: current.startTs,
        endTs: current.endTs,
        processName: current.processName,
        exePath: current.exePath,
        windowTitle: current.windowTitle,
      });
    }
    current = null;
  }
  console.log('[tracker] stopped');
}

function getTrackerStatus() {
  return {
    running,
    apiReady: !!api,
    pollMs: POLL_MS,
    current: current
      ? {
          processName: current.processName,
          startTs: current.startTs,
          endTs: current.endTs,
        }
      : null,
  };
}

module.exports = { startTracker, stopTracker, getTrackerStatus };
