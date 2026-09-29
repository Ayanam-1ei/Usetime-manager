using System;
using System.Drawing;
using System.IO;
using System.Windows;
using UsetimeManager.Services;
using Application = System.Windows.Application;

namespace UsetimeManager;

public partial class App : Application
{
    private SessionStore? _store;
    private TrackerService? _tracker;
    private System.Windows.Forms.NotifyIcon? _tray;
    private MainWindow? _mainWindow;
    private System.Threading.Mutex? _singleInstanceMutex;

    public SessionStore? Store => _store;
    public TrackerService? Tracker => _tracker;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例（持有 Mutex 字段，防止被 GC 回收）
        _singleInstanceMutex = new System.Threading.Mutex(true, "UsetimeManager_SingleInstance", out var created);
        if (!created)
        {
            Shutdown();
            return;
        }

        _store = new SessionStore();
        _tracker = new TrackerService(_store);
        _tracker.Start();

        InitTray();

        _mainWindow = new MainWindow(_store, _tracker!);
        MainWindow = _mainWindow;
        _mainWindow.Show();
    }

    private void InitTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "屏幕使用时长统计",
            Icon = CreateTrayIcon(),
            Visible = true
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示主界面", null, (_, _) => ShowMain());
        menu.Items.Add("暂停记录", null, (_, _) => TogglePause());
        menu.Items.Add(new ToolStripSeparator());

        var autoStartItem = new ToolStripMenuItem("开机自启")
        {
            CheckOnClick = true,
            Checked = AutostartService.IsEnabled()
        };
        autoStartItem.CheckedChanged += (_, _) =>
        {
            if (autoStartItem.Checked) AutostartService.Enable();
            else AutostartService.Disable();
        };
        menu.Items.Add(autoStartItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Quit());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowMain();
    }

    private void TogglePause()
    {
        if (_tracker == null) return;
        if (_tracker.IsRunning)
        {
            _tracker.Stop();
            if (_tray != null) _tray.Text = "屏幕使用时长统计（已暂停）";
        }
        else
        {
            _tracker.Start();
            if (_tray != null) _tray.Text = "屏幕使用时长统计";
        }
    }

    private void ShowMain()
    {
        if (_mainWindow == null)
        {
            var store = _store ?? new SessionStore();
            var tracker = _tracker ?? new TrackerService(store);
            _mainWindow = new MainWindow(store, tracker);
            MainWindow = _mainWindow;
        }
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void HideMain()
    {
        _mainWindow?.Hide();
    }

    public void Quit()
    {
        _tracker?.Dispose();
        _store?.FlushAndDispose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tracker?.Dispose();
        _store?.FlushAndDispose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        base.OnExit(e);
    }

    private static Icon CreateTrayIcon()
    {
        var bmp = new System.Drawing.Bitmap(32, 32);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(System.Drawing.Color.Transparent);
        using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(43, 108, 255));
        g.FillEllipse(brush, 2, 2, 28, 28);
        using var font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
        var fmt = new System.Drawing.StringFormat
        {
            Alignment = System.Drawing.StringAlignment.Center,
            LineAlignment = System.Drawing.StringAlignment.Center
        };
        g.DrawString("U", font, System.Drawing.Brushes.White, new System.Drawing.RectangleF(0, 0, 32, 32), fmt);
        return Icon.FromHandle(bmp.GetHicon());
    }
}
