import { useCallback, useEffect, useMemo, useState } from 'react';
import type { AppUsage, ChartData, Period, Summary } from './types';
import {
  formatDateCN,
  formatDuration,
  shiftDateStr,
  toDateStr,
} from './utils/format';
import { TimeChart } from './components/TimeChart';
import { AppUsageList } from './components/AppUsageList';

const PERIODS: { key: Period; label: string }[] = [
  { key: 'day', label: '日' },
  { key: 'week', label: '周' },
  { key: 'month', label: '月' },
];

function periodTitle(period: Period): string {
  if (period === 'day') return '屏幕使用时长';
  if (period === 'week') return '本周屏幕使用时长';
  return '本月屏幕使用时长';
}

function compareLabel(period: Period): string {
  if (period === 'day') return '较昨日';
  if (period === 'week') return '较上周';
  return '较上月';
}

function formatDeltaWithLabel(deltaMs: number, period: Period): string {
  const abs = Math.abs(deltaMs);
  const body = formatDuration(abs);
  const label = compareLabel(period);
  if (deltaMs > 0) return `${label}增加${body}`;
  if (deltaMs < 0) return `${label}减少${body}`;
  return `与${label.slice(1)}持平`;
}

function appListTitle(period: Period): string {
  if (period === 'day') return '今日应用使用情况';
  if (period === 'week') return '本周应用使用情况';
  return '本月应用使用情况';
}

export default function App() {
  const [period, setPeriod] = useState<Period>('day');
  const [dateStr, setDateStr] = useState(() => toDateStr(Date.now()));
  const [summary, setSummary] = useState<Summary | null>(null);
  const [chart, setChart] = useState<ChartData | null>(null);
  const [apps, setApps] = useState<AppUsage | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sortMode, setSortMode] = useState<'time' | 'name'>('time');

  const hasApi = typeof window !== 'undefined' && !!window.usetime;

  const load = useCallback(async () => {
    if (!hasApi) {
      setError('未检测到 Electron 环境');
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const [s, c, a] = await Promise.all([
        window.usetime.getSummary(period, dateStr),
        window.usetime.getChartData(period, dateStr),
        window.usetime.getAppUsage(period, dateStr),
      ]);
      setSummary(s);
      setChart(c);
      setApps(a);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }, [period, dateStr, hasApi]);

  useEffect(() => {
    void load();
  }, [load]);

  // 日视图下自动刷新（今日数据在增长）
  useEffect(() => {
    if (period !== 'day' || dateStr !== toDateStr(Date.now())) return;
    const t = setInterval(() => {
      void load();
    }, 30000);
    return () => clearInterval(t);
  }, [period, dateStr, load]);

  const dateLabel = useMemo(() => formatDateCN(
    // for week/month label based on dateStr noon
    (() => {
      const [y, m, d] = dateStr.split('-').map(Number);
      return new Date(y, m - 1, d, 12).getTime();
    })(),
    period
  ), [dateStr, period]);

  const isToday = dateStr === toDateStr(Date.now());

  return (
    <div className="app">
      <header className="app-header">
        <h1 className="app-title">屏幕使用时长统计</h1>
        <div className="header-actions">
          <button
            type="button"
            className="ghost-btn"
            onClick={() => void load()}
            disabled={loading}
          >
            {loading ? '刷新中…' : '刷新'}
          </button>
        </div>
      </header>

      <nav className="period-tabs" aria-label="统计粒度">
        {PERIODS.map((p) => (
          <button
            key={p.key}
            type="button"
            className={`period-tab${period === p.key ? ' active' : ''}`}
            onClick={() => setPeriod(p.key)}
          >
            {p.label}
          </button>
        ))}
      </nav>

      <section className="card">
        <div className="date-nav">
          <button
            type="button"
            aria-label="上一周期"
            onClick={() => setDateStr(shiftDateStr(dateStr, period, -1))}
          >
            ‹
          </button>
          <div className="date-label">{dateLabel}</div>
          <button
            type="button"
            aria-label="下一周期"
            onClick={() => setDateStr(shiftDateStr(dateStr, period, 1))}
            disabled={dateStr >= toDateStr(Date.now())}
            style={{ opacity: dateStr >= toDateStr(Date.now()) ? 0.35 : 1 }}
          >
            ›
          </button>
        </div>

        <div className="summary-sub" style={{ marginTop: -8 }}>
          {isToday && period === 'day' ? '今日屏幕使用时长' : periodTitle(period)}
        </div>
        <div className="summary-main">
          {summary ? formatDuration(summary.totalMs) : '—'}
        </div>
        <p className="summary-sub">
          {summary ? formatDeltaWithLabel(summary.deltaMs, period) : '暂无数据'}
        </p>

        {chart && chart.points.some((p) => p.ms > 0) ? (
          <div className="chart-wrap">
            <TimeChart points={chart.points} period={period} />
          </div>
        ) : (
          <div className="chart-empty">
            {loading ? '加载中…' : '该周期暂无使用记录'}
          </div>
        )}
      </section>

      <section className="card">
        <div className="section-head">
          <h2 className="section-title">{appListTitle(period)}</h2>
          <button
            type="button"
            className="sort-btn"
            onClick={() => setSortMode((s) => (s === 'time' ? 'name' : 'time'))}
          >
            {sortMode === 'time' ? '时长' : '名称'}
            <span aria-hidden>⇅</span>
          </button>
        </div>

        <AppUsageList
          apps={apps?.apps ?? []}
          sortMode={sortMode}
          getIcon={(exePath) => window.usetime.getIcon(exePath)}
        />
      </section>

      {error && <div className="footer-tip">错误：{error}</div>}
      {!error && !loading && apps && apps.apps.length === 0 && (
        <div className="footer-tip">该周期还没有采集到应用使用数据</div>
      )}
    </div>
  );
}
