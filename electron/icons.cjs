/**
 * 从 exe 提取图标，缓存为 png data URL。
 */
const path = require('path');
const fs = require('fs');
const { execFile } = require('child_process');
const { app } = require('electron');

const cache = new Map(); // exePath -> dataUrl | null

function getIconCacheDir() {
  const base = app && app.getPath ? app.getPath('userData') : path.join(process.cwd(), 'data');
  const dir = path.join(base, 'usetime-data', 'icons');
  fs.mkdirSync(dir, { recursive: true });
  return dir;
}

function safeName(exePath) {
  return exePath.replace(/[<>:"/\\|?*]/g, '_').slice(-120);
}

function extractIcon(exePath) {
  return new Promise((resolve) => {
    if (!exePath || !/\.exe$/i.test(exePath)) {
      resolve(null);
      return;
    }
    if (cache.has(exePath)) {
      resolve(cache.get(exePath));
      return;
    }

    const dir = getIconCacheDir();
    const outFile = path.join(dir, `${safeName(exePath)}.png`);

    // 已有缓存文件
    if (fs.existsSync(outFile)) {
      try {
        const buf = fs.readFileSync(outFile);
        const dataUrl = `data:image/png;base64,${buf.toString('base64')}`;
        cache.set(exePath, dataUrl);
        resolve(dataUrl);
        return;
      } catch {
        /* fallthrough */
      }
    }

    const ps = `
Add-Type -AssemblyName System.Drawing
$p = $env:ICON_EXE
if (-not (Test-Path -LiteralPath $p)) { exit 2 }
try {
  $icon = [System.Drawing.Icon]::ExtractAssociatedIcon($p)
  if ($null -eq $icon) { exit 3 }
  $bmp = $icon.ToBitmap()
  $bmp.Save($env:ICON_OUT, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  $icon.Dispose()
  exit 0
} catch { exit 1 }
`;

    execFile(
      'powershell.exe',
      ['-NoProfile', '-NonInteractive', '-Command', ps],
      {
        env: { ...process.env, ICON_EXE: exePath, ICON_OUT: outFile },
        windowsHide: true,
        timeout: 8000,
      },
      (err) => {
        let dataUrl = null;
        if (!err && fs.existsSync(outFile)) {
          try {
            const buf = fs.readFileSync(outFile);
            dataUrl = `data:image/png;base64,${buf.toString('base64')}`;
          } catch {
            dataUrl = null;
          }
        }
        cache.set(exePath, dataUrl);
        resolve(dataUrl);
      }
    );
  });
}

module.exports = { extractIcon };
