# Usetime Manager

Windows 桌面端「屏幕使用时长统计」。后台记录前台应用使用情况，按 **日 / 周 / 月** 展示总时长、环比、分时柱状图与应用排行。

## 功能

- 自动采集前台窗口（进程名、exe 路径、窗口标题、起止时间）
- 日 / 周 / 月统计，可切换日期
- 总时长 + 较上一周期增减
- 日：24 小时柱状图；周：7 天；月：按日
- 应用使用排行（图标、时长、占比），可按名称 / 时长排序
- 关闭主窗口后进程继续在后台采集
- 单实例运行

## 运行

```bash
npm install
npm run build
npx electron .
```

开发模式（热更新）：

```bash
npm run dev                 # 终端 1：Vite
npm run electron:dev        # 终端 2：Electron 连 Vite
```

## 技术栈

- Electron + React + TypeScript + Vite
- Recharts（图表）
- koffi（Win32 前台窗口 API）
- 本地 JSON 存储（`%APPDATA%/usetime-manager/usetime-data/sessions.jsonl`）

## 数据位置

`%APPDATA%/usetime-manager/usetime-data/`

- `sessions.jsonl` — 会话记录
- `icons/` — 从 exe 提取的图标缓存

## 自检截图

```bash
$env:USETIME_CAPTURE="1"
npx electron .
# 生成 preview.png
```
