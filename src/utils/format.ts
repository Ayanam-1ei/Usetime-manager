export function formatDuration(ms: number): string {
  if (!Number.isFinite(ms) || ms < 0) ms = 0;
  const totalSec = Math.floor(ms / 1000);
  const h = Math.floor(totalSec / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = totalSec % 60;
  if (h > 0) {
    return m > 0 ? `${h}小时${m}分钟` : `${h}小时`;
  }
  if (m > 0) {
    return s > 0 && m < 5 ? `${m}分${s}秒` : `${m}分钟`;
  }
  return `${s}秒`;
}

export function formatDelta(deltaMs: number): string {
  const abs = Math.abs(deltaMs);
  const body = formatDuration(abs);
  if (deltaMs > 0) return `较上一周期增加${body}`;
  if (deltaMs < 0) return `较上一周期减少${body}`;
  return '与上一周期持平';
}

export function formatDateCN(ts: number, period: 'day' | 'week' | 'month'): string {
  const d = new Date(ts);
  const y = d.getFullYear();
  const m = d.getMonth() + 1;
  const day = d.getDate();
  if (period === 'day') return `${y}年${m}月${day}日`;
  if (period === 'month') return `${y}年${m}月`;
  // week
  const day2 = new Date(ts);
  const dow = day2.getDay();
  const diff = dow === 0 ? -6 : 1 - dow;
  day2.setDate(day2.getDate() + diff);
  const end = new Date(day2);
  end.setDate(end.getDate() + 6);
  return `${day2.getFullYear()}年${day2.getMonth() + 1}月${day2.getDate()}日 – ${end.getMonth() + 1}月${end.getDate()}日`;
}

export function toDateStr(ts: number): string {
  const d = new Date(ts);
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${m}-${day}`;
}

export function addDaysToDateStr(dateStr: string, n: number): string {
  const [y, m, d] = dateStr.split('-').map(Number);
  const dt = new Date(y, m - 1, d);
  dt.setDate(dt.getDate() + n);
  return toDateStr(dt.getTime());
}

export function shiftDateStr(dateStr: string, period: 'day' | 'week' | 'month', n: number): string {
  const [y, m, d] = dateStr.split('-').map(Number);
  const dt = new Date(y, m - 1, d);
  if (period === 'day') {
    dt.setDate(dt.getDate() + n);
  } else if (period === 'week') {
    dt.setDate(dt.getDate() + n * 7);
  } else {
    dt.setMonth(dt.getMonth() + n, 1);
  }
  return toDateStr(dt.getTime());
}
