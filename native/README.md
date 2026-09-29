# UsetimeManager（轻量原生版）

C# / WPF / .NET 8 的屏幕使用时长统计，替代 Electron 版以降低占用。

## 运行

```powershell
# 开发
cd native\UsetimeManager
dotnet run

# 发布单文件 exe（依赖本机 .NET 8 Desktop 运行时）
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

产物：`native\UsetimeManager\publish\UsetimeManager.exe`（约 0.2MB）

若目标机器未安装 .NET 8 Desktop Runtime，改用自包含：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-sc
```

## 功能

- 后台采集前台窗口（进程名、exe、标题、起止时间）
- 日 / 周 / 月统计，日期切换
- 总时长 + 环比，分时柱状图，应用排行
- 托盘图标：显示 / 暂停 / 开机自启 / 退出
- 关闭窗口即隐藏，进程继续采集

## 数据

`%LocalAppData%\UsetimeManager\sessions.jsonl`

与 Electron 版（`%APPDATA%\usetime-manager`）互不影响。
