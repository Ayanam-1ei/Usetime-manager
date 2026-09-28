/**
 * 本地 JSON 存储：会话按天分文件，启动时加载，增量落盘。
 * 避免 better-sqlite3 原生编译问题，规模对屏幕使用统计足够。
 */
const path = require('path');
const fs = require('fs');
const { app } = require('electron');

let dataDir = null;
let sessions = []; // { startTs, endTs, processName, exePath, windowTitle }
let dirty = false;
let saveTimer = null;

function getDataDir() {
  if (!dataDir) {
    const base = app && app.getPath ? app.getPath('userData') : path.join(process.cwd(), 'data');
    dataDir = path.join(base, 'usetime-data');
    fs.mkdirSync(dataDir, { recursive: true });
  }
  return dataDir;
}

function sessionsFile() {
  return path.join(getDataDir(), 'sessions.jsonl');
}

function sessionKey(s) {
  return `${s.startTs}|${s.endTs}|${s.processName}|${s.exePath}`;
}

function init() {
  const dir = getDataDir();
  sessions = [];
  // 只读取权威源 sessions.jsonl（避免与按天文件重复加载）
  const p = sessionsFile();
  if (fs.existsSync(p)) {
    try {
      const text = fs.readFileSync(p, 'utf8');
      const seen = new Set();
      for (const line of text.split(/\r?\n/)) {
        const t = line.trim();
        if (!t) continue;
        try {
          const obj = JSON.parse(t);
          if (obj && typeof obj.startTs === 'number' && typeof obj.endTs === 'number' && obj.endTs > obj.startTs) {
            const rec = {
              startTs: obj.startTs,
              endTs: obj.endTs,
              processName: obj.processName || 'unknown',
              exePath: obj.exePath || '',
              windowTitle: obj.windowTitle || '',
            };
            const key = sessionKey(rec);
            if (seen.has(key)) continue;
            seen.add(key);
            sessions.push(rec);
          }
        } catch {
          /* skip bad line */
        }
      }
    } catch (err) {
      console.error('[db] load failed:', err);
    }
  }
  sessions.sort((a, b) => a.startTs - b.startTs);
  console.log('[db] loaded sessions:', sessions.length);
}

function persistNow() {
  if (!dirty) return;
  dirty = false;
  try {
    const lines = sessions.map((s) => JSON.stringify(s)).join('\n') + (sessions.length ? '\n' : '');
    fs.writeFileSync(sessionsFile(), lines, 'utf8');
  } catch (err) {
    console.error('[db] persist failed:', err);
    dirty = true;
  }
}

function scheduleSave() {
  dirty = true;
  if (saveTimer) return;
  saveTimer = setTimeout(() => {
    saveTimer = null;
    persistNow();
  }, 2000);
}

function addSession({ startTs, endTs, processName, exePath, windowTitle }) {
  if (!(endTs > startTs)) return;
  const rec = {
    startTs,
    endTs,
    processName: processName || 'unknown',
    exePath: exePath || '',
    windowTitle: windowTitle || '',
  };
  // 与上一条完全重合则忽略，防止 flush/切换双写
  const last = sessions[sessions.length - 1];
  if (last && sessionKey(last) === sessionKey(rec)) {
    last.endTs = Math.max(last.endTs, rec.endTs);
  } else {
    sessions.push(rec);
  }
  scheduleSave();
}

function close() {
  persistNow();
}

// ---------- 时间工具（本地时区） ----------
function startOfDay(ts) {
  const d = new Date(ts);
  d.setHours(0, 0, 0, 0);
  return d.getTime();
}

function endOfDay(ts) {
  const d = new Date(ts);
  d.setHours(23, 59, 59, 999);
  return d.getTime();
}

function addDays(ts, n) {
  const d = new Date(ts);
  d.setDate(d.getDate() + n);
  return d.getTime();
}

function startOfWeek(ts) {
  const d = new Date(startOfDay(ts));
  const day = d.getDay();
  const diff = day === 0 ? -6 : 1 - day;
  d.setDate(d.getDate() + diff);
  return d.getTime();
}

function endOfWeek(ts) {
  return endOfDay(addDays(startOfWeek(ts), 6));
}

function startOfMonth(ts) {
  const d = new Date(ts);
  d.setDate(1);
  d.setHours(0, 0, 0, 0);
  return d.getTime();
}

function endOfMonth(ts) {
  const d = new Date(ts);
  d.setMonth(d.getMonth() + 1, 0);
  d.setHours(23, 59, 59, 999);
  return d.getTime();
}

function parseDateStr(dateStr) {
  if (!dateStr) return Date.now();
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateStr);
  if (m) {
    return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]), 12, 0, 0, 0).getTime();
  }
  const t = Date.parse(dateStr);
  return Number.isNaN(t) ? Date.now() : t;
}

function rangeForPeriod(period, dateStr) {
  const t = parseDateStr(dateStr);
  if (period === 'day') return { start: startOfDay(t), end: endOfDay(t) };
  if (period === 'week') return { start: startOfWeek(t), end: endOfWeek(t) };
  return { start: startOfMonth(t), end: endOfMonth(t) };
}

function prevRangeForPeriod(period, dateStr) {
  const t = parseDateStr(dateStr);
  if (period === 'day') {
    const y = addDays(t, -1);
    return { start: startOfDay(y), end: endOfDay(y) };
  }
  if (period === 'week') {
    const w = addDays(startOfWeek(t), -7);
    return { start: w, end: endOfDay(addDays(w, 6)) };
  }
  const d = new Date(t);
  d.setMonth(d.getMonth() - 1, 1);
  d.setHours(0, 0, 0, 0);
  return { start: d.getTime(), end: endOfMonth(d.getTime()) };
}

function overlapMs(sStart, sEnd, rStart, rEnd) {
  const a = Math.max(sStart, rStart);
  const b = Math.min(sEnd, rEnd);
  return b > a ? b - a : 0;
}

function sumRange(start, end) {
  let total = 0;
  for (const s of sessions) {
    total += overlapMs(s.startTs, s.endTs, start, end);
  }
  return total;
}

function getSummary(period, dateStr) {
  const cur = rangeForPeriod(period, dateStr);
  const prev = prevRangeForPeriod(period, dateStr);
  const totalMs = sumRange(cur.start, cur.end);
  const prevMs = sumRange(prev.start, prev.end);
  return {
    period,
    dateStr: dateStr || null,
    start: cur.start,
    end: cur.end,
    totalMs,
    prevTotalMs: prevMs,
    deltaMs: totalMs - prevMs,
  };
}

function getChartData(period, dateStr) {
  const { start, end } = rangeForPeriod(period, dateStr);
  const buckets = [];

  if (period === 'day') {
    for (let h = 0; h < 24; h++) {
      const bStart = startOfDay(start) + h * 3600000;
      buckets.push({
        label: `${String(h).padStart(2, '0')}:00`,
        start: bStart,
        end: bStart + 3600000,
        ms: 0,
      });
    }
  } else if (period === 'week') {
    const names = ['日', '一', '二', '三', '四', '五', '六'];
    for (let i = 0; i < 7; i++) {
      const dayStart = addDays(start, i);
      const d = new Date(dayStart);
      buckets.push({
        label: `周${names[d.getDay()]}`,
        start: dayStart,
        end: endOfDay(dayStart) + 1,
        ms: 0,
      });
    }
  } else {
    const d0 = new Date(start);
    const daysInMonth = new Date(d0.getFullYear(), d0.getMonth() + 1, 0).getDate();
    for (let i = 0; i < daysInMonth; i++) {
      const dayStart = addDays(start, i);
      buckets.push({
        label: String(i + 1),
        start: dayStart,
        end: endOfDay(dayStart) + 1,
        ms: 0,
      });
    }
  }

  for (const r of sessions) {
    for (const b of buckets) {
      b.ms += overlapMs(r.startTs, r.endTs, b.start, b.end);
    }
  }

  return {
    period,
    start,
    end,
    points: buckets.map((b) => ({ label: b.label, ms: b.ms })),
  };
}

function getAppUsage(period, dateStr) {
  const { start, end } = rangeForPeriod(period, dateStr);
  const map = new Map();
  let totalMs = 0;

  for (const r of sessions) {
    const ms = overlapMs(r.startTs, r.endTs, start, end);
    if (ms <= 0) continue;
    totalMs += ms;
    const key = r.processName || 'unknown';
    const cur = map.get(key) || {
      processName: key,
      exePath: r.exePath || '',
      totalMs: 0,
    };
    cur.totalMs += ms;
    if (!cur.exePath && r.exePath) cur.exePath = r.exePath;
    map.set(key, cur);
  }

  const list = [...map.values()].sort((a, b) => b.totalMs - a.totalMs);
  return {
    period,
    start,
    end,
    totalMs,
    apps: list.map((x) => ({
      processName: x.processName,
      exePath: x.exePath,
      totalMs: x.totalMs,
      ratio: totalMs > 0 ? x.totalMs / totalMs : 0,
    })),
  };
}

module.exports = {
  init,
  close,
  addSession,
  getSummary,
  getChartData,
  getAppUsage,
};
