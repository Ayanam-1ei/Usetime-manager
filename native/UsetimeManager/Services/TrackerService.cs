using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace UsetimeManager.Services;

/// <summary>Win32 前台窗口采集</summary>
public sealed class TrackerService : IDisposable
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(2000);
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(15);

    private readonly SessionStore _store;
    private readonly System.Threading.Timer _pollTimer;
    private readonly System.Threading.Timer _flushTimer;

    private UsageSession? _current;
    private readonly object _gate = new();
    private bool _running;

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
        CommitCurrent();
    }

    private void Sample()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var info = GetForegroundInfo();
        if (info is null) return;

        lock (_gate)
        {
            var same = _current != null &&
                       _current.ProcessName == info.Value.ProcessName &&
                       _current.ExePath == info.Value.ExePath;

            if (same)
            {
                if (!string.IsNullOrEmpty(info.Value.WindowTitle))
                    _current!.WindowTitle = info.Value.WindowTitle;
                _current!.EndTs = now;
                return;
            }

            if (_current != null && _current.EndTs - _current.StartTs >= 1000)
            {
                _store.Add(_current);
            }

            _current = new UsageSession
            {
                StartTs = now,
                EndTs = now,
                ProcessName = info.Value.ProcessName,
                ExePath = info.Value.ExePath,
                WindowTitle = info.Value.WindowTitle
            };
        }
    }

    private void FlushCurrent()
    {
        lock (_gate)
        {
            if (_current == null) return;
            if (_current.EndTs - _current.StartTs < 500) return;
            _store.Add(_current);
            _current.StartTs = _current.EndTs;
        }
    }

    private void CommitCurrent()
    {
        lock (_gate)
        {
            if (_current == null) return;
            if (_current.EndTs - _current.StartTs >= 500)
            {
                _store.Add(_current);
            }
            _current = null;
        }
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
