/**
 * 开机自启（当前用户启动项，注册表 Run）
 */
const { app } = require('electron');
const path = require('path');
const { execFile } = require('child_process');

const APP_NAME = 'UsetimeManager';

function getExePath() {
  // 开发环境用 electron 可执行文件 + 项目路径不便于自启，生产打包后 process.execPath 即应用
  return process.execPath;
}

function isAutostartEnabled() {
  return new Promise((resolve) => {
    const ps = `
$k = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run'
try {
  $v = (Get-ItemProperty -Path $k -Name '${APP_NAME}' -ErrorAction Stop).'${APP_NAME}'
  if ($v) { Write-Output '1' } else { Write-Output '0' }
} catch { Write-Output '0' }
`;
    execFile(
      'powershell.exe',
      ['-NoProfile', '-NonInteractive', '-Command', ps],
      { windowsHide: true, timeout: 5000 },
      (err, stdout) => {
        resolve(!err && String(stdout).trim() === '1');
      }
    );
  });
}

function enableAutostart() {
  return new Promise((resolve) => {
    // 打包后：直接 exe；开发环境标记为不支持自启
    const exe = getExePath();
    const isDev = !app.isPackaged;
    if (isDev) {
      resolve({ ok: false, reason: '开发模式不写入开机自启，打包后可用' });
      return;
    }
    const escaped = exe.replace(/'/g, "''");
    const ps = `
$k = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run'
New-Item -Path $k -Force | Out-Null
Set-ItemProperty -Path $k -Name '${APP_NAME}' -Value "'${escaped}'"
`;
    execFile(
      'powershell.exe',
      ['-NoProfile', '-NonInteractive', '-Command', ps],
      { windowsHide: true, timeout: 5000 },
      (err) => {
        resolve({ ok: !err, reason: err ? String(err.message) : null });
      }
    );
  });
}

function disableAutostart() {
  return new Promise((resolve) => {
    const ps = `
$k = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run'
try { Remove-ItemProperty -Path $k -Name '${APP_NAME}' -ErrorAction Stop } catch {}
`;
    execFile(
      'powershell.exe',
      ['-NoProfile', '-NonInteractive', '-Command', ps],
      { windowsHide: true, timeout: 5000 },
      (err) => {
        resolve({ ok: !err, reason: err ? String(err.message) : null });
      }
    );
  });
}

module.exports = { enableAutostart, disableAutostart, isAutostartEnabled };
