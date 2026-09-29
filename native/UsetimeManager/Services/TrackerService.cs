using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace UsetimeManager.Services;

/// <summary>
/// Win32 前台窗口采集。
/// 轮询 1 秒 + 前台切换事件双通道，减少短切换漏记；
/// 进行中的会话可通过 GetCurrentSession() 参与统计。
/// </summary>
public sealed class TrackerService : IDisposable
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(1000);
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(10);
    private const long MinSessionMs = 300;

    private readonly SessionStore _store;
    private readonly System.Threading.Timer _pollTimer;
    private readonly System.Threading.Timer _flushTimer;

    private UsageSession? _current;
    private readonly object _gate = new();
    private bool _running;
    private IntPtr _winEventHook = IntPtr.Zero;
    private WinEventDelegate? _winEventProc; // 防止 GC 回收回调

    public bool IsRunning => _running;

    public TrackerService(SessionStore store)
    {
        _store = store;
        _pollTimer = new System.Threading.Timer(_ => Sample(), null, Timeout.Infinite, Timeout.Infinite);
        _flushTimer = new System.Threading.Timer(_ => FlushCurrent(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_running) return;
            _running = true;
        }
        InstallHook();
        Sample();
        _pollTimer.Change(PollInterval, PollInterval);
        _flushTimer.Change(FlushInterval, FlushInterval);
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_running) return;
            _running = false;
        }
        _pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);
        RemoveHook();
        CommitCurrent();
    }

    /// <summary>进行中的会话快照（尚未落库），供 UI 实时统计</summary>
    public UsageSession? GetCurrentSession()
    {
        lock (_gate)
        {
            if (_current is null) return null;
            return new UsageSession
            {
                StartTs = _current.StartTs,
                EndTs = _current.EndTs,
                ProcessName = _current.ProcessName,
                ExePath = _current.ExePath,
                WindowTitle = _current.WindowTitle
            };
        }
    }

    /// <summary>落库会话 + 进行中会话</summary>
    public List<UsageSession> SnapshotAll()
    {
        var list = new List<UsageSession>(_store.Snapshot());
        var cur = GetCurrentSession();
        if (cur != null && cur.EndTs > cur.StartTs)
            list.Add(cur);
        return list;
    }

    private void Sample()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var info = GetForegroundInfo();
        if (info is null) return;

        lock (_gate)
        {
            AcceptSample(info.Value.ProcessName, info.Value.ExePath, info.Value.WindowTitle, now);
        }
    }

    private void AcceptSample(string processName, string exePath, string windowTitle, long now)
    {
        var same = _current != null &&
                   _current.ProcessName == processName &&
                   _current.ExePath == exePath;

        if (same)
        {
            if (!string.IsNullOrEmpty(windowTitle))
                _current!.WindowTitle = windowTitle;
            _current!.EndTs = now;
            return;
        }

        if (_current != null && _current.EndTs - _current.StartTs >= MinSessionMs)
        {
            _store.Add(_current);
        }

        _current = new UsageSession
        {
            StartTs = now,
            EndTs = now,
            ProcessName = processName,
            ExePath = exePath,
            WindowTitle = windowTitle
        };
    }

    private void FlushCurrent()
    {
        lock (_gate)
        {
            if (_current == null) return;
            if (_current.EndTs - _current.StartTs < MinSessionMs) return;
            _store.Add(_current);
            _current.StartTs = _current.EndTs;
        }
    }

    private void CommitCurrent()
    {
        lock (_gate)
        {
            if (_current == null) return;
            if (_current.EndTs - _current.StartTs >= MinSessionMs)
            {
                _store.Add(_current);
            }
            _current = null;
        }
    }

    // ---- 前台切换钩子（补充轮询，降低漏记） ----
    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    private void InstallHook()
    {
        if (_winEventHook != IntPtr.Zero) return;
        _winEventProc = OnForegroundChanged;
        _winEventHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    private void RemoveHook()
    {
        if (_winEventHook == IntPtr.Zero) return;
        UnhookWinEvent(_winEventHook);
        _winEventHook = IntPtr.Zero;
        _winEventProc = null;
    }

    private void OnForegroundChanged(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (!_running) return;
        // 事件回调里直接采样一次，避免等到下一次轮询
        Sample();
    }

    public void Dispose()
    {
        Stop();
        _pollTimer.Dispose();
        _flushTimer.Dispose();
    }

    // ---- Win32 ----
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder sb, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLengthW(IntPtr hWnd);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, int inherit, uint pid);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint flags, StringBuilder exe, ref uint size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private readonly record struct ForegroundInfo(string ProcessName, string ExePath, string WindowTitle);

    private static ForegroundInfo? GetForegroundInfo()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return null;

            string title = "";
            var len = GetWindowTextLengthW(hwnd);
            if (len > 0)
            {
                var sb = new StringBuilder(len + 1);
                if (GetWindowTextW(hwnd, sb, sb.Capacity) > 0)
                    title = sb.ToString();
            }

            string exePath = "";
            var hProc = OpenProcess(ProcessQueryLimitedInformation, 0, pid);
            if (hProc != IntPtr.Zero)
            {
                try
                {
                    var sb = new StringBuilder(1024);
                    uint size = (uint)sb.Capacity;
                    if (QueryFullProcessImageNameW(hProc, 0, sb, ref size))
                        exePath = sb.ToString();
                }
                finally
                {
                    CloseHandle(hProc);
                }
            }

            var name = string.IsNullOrEmpty(exePath)
                ? $"pid-{pid}"
                : Path.GetFileNameWithoutExtension(exePath);
            if (string.IsNullOrEmpty(name)) name = $"pid-{pid}";

            return new ForegroundInfo(name, exePath, title);
        }
        catch
        {
            return null;
        }
    }
}
