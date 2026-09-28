/**
 * 修复 electron 启动路径：path.txt 必须是相对路径且不含换行。
 * 用于 postinstall 未跑 / 手动补二进制后的一键修复。
 */
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const dist = path.join(root, 'node_modules', 'electron', 'dist');
const pathTxt = path.join(root, 'node_modules', 'electron', 'path.txt');
const exe = path.join(dist, process.platform === 'win32' ? 'electron.exe' : 'electron');

if (!fs.existsSync(exe)) {
  console.error('缺少 electron 二进制:', exe);
  console.error('请设置镜像后执行: node install.js（在 node_modules/electron 下）');
  process.exit(1);
}

fs.writeFileSync(pathTxt, path.basename(exe), 'utf8');
console.log('已写入 path.txt ->', path.basename(exe));
console.log('可执行文件:', exe);
