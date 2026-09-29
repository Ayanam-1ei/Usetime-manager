using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Timer = System.Threading.Timer;

namespace UsetimeManager.Services;

public sealed class UsageSession
{
    [JsonPropertyName("startTs")]
    public long StartTs { get; set; }

    [JsonPropertyName("endTs")]
    public long EndTs { get; set; }

    [JsonPropertyName("processName")]
    public string ProcessName { get; set; } = "unknown";

    [JsonPropertyName("exePath")]
    public string ExePath { get; set; } = "";

    [JsonPropertyName("windowTitle")]
    public string WindowTitle { get; set; } = "";
}

/// <summary>JSONL 会话存储：%LocalAppData%\UsetimeManager\sessions.jsonl</summary>
public sealed class SessionStore
{
    private readonly object _gate = new();
    private readonly string _file;
    private readonly List<UsageSession> _sessions = new();
    private bool _dirty;
    private Timer? _saveTimer;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SessionStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UsetimeManager");
        Directory.CreateDirectory(dir);
        _file = Path.Combine(dir, "sessions.jsonl");
        Load();
    }

    public string FilePath => _file;

    private void Load()
    {
        if (!File.Exists(_file)) return;
        var seen = new HashSet<string>();
        foreach (var line in File.ReadLines(_file))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var s = JsonSerializer.Deserialize<UsageSession>(line, JsonOptions);
                if (s is null || s.EndTs <= s.StartTs) continue;
                var key = Key(s);
                if (!seen.Add(key)) continue;
                _sessions.Add(s);
            }
            catch
            {
                // skip bad line
            }
        }
        _sessions.Sort((a, b) => a.StartTs.CompareTo(b.StartTs));
    }

    private static string Key(UsageSession s) =>
        $"{s.StartTs}|{s.EndTs}|{s.ProcessName}|{s.ExePath}";

    public void Add(UsageSession session)
    {
        if (session.EndTs <= session.StartTs) return;
        lock (_gate)
        {
            var last = _sessions.Count > 0 ? _sessions[^1] : null;
            if (last != null && Key(last) == Key(session))
            {
                last.EndTs = Math.Max(last.EndTs, session.EndTs);
            }
            else
            {
                _sessions.Add(session);
            }
            _dirty = true;
        }
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        _saveTimer ??= new Timer(_ =>
        {
            Save();
        }, null, 2000, Timeout.Infinite);
    }

    public void Save()
    {
        List<UsageSession> snapshot;
        lock (_gate)
        {
            if (!_dirty) return;
            _dirty = false;
            snapshot = new List<UsageSession>(_sessions);
        }
        try
        {
            var sb = new StringBuilder();
            foreach (var s in snapshot)
            {
                sb.AppendLine(JsonSerializer.Serialize(s, JsonOptions));
            }
            File.WriteAllText(_file, sb.ToString());
        }
        catch
        {
            lock (_gate) { _dirty = true; }
        }
    }

    public void FlushAndDispose()
    {
        Save();
        _saveTimer?.Dispose();
        _saveTimer = null;
    }

    public IReadOnlyList<UsageSession> Snapshot()
    {
        lock (_gate) return _sessions.ToList();
    }
}
