import { useEffect, useMemo, useState } from 'react';
import type { AppUsageItem } from '../types';
import { formatDuration } from '../utils/format';

interface Props {
  apps: AppUsageItem[];
  sortMode: 'time' | 'name';
  getIcon: (exePath: string) => Promise<string | null>;
}

function AppIcon({
  exePath,
  processName,
  getIcon,
}: {
  exePath: string;
  processName: string;
  getIcon: Props['getIcon'];
}) {
  const [src, setSrc] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setSrc(null);
    if (!exePath) return;
    void getIcon(exePath).then((url) => {
      if (!cancelled) setSrc(url);
    });
    return () => {
      cancelled = true;
    };
  }, [exePath, getIcon]);

  const letter = (processName || '?').slice(0, 1).toUpperCase();

  return (
    <div className="app-icon" title={processName}>
      {src ? <img src={src} alt="" /> : letter}
    </div>
  );
}

export function AppUsageList({ apps, sortMode, getIcon }: Props) {
  const sorted = useMemo(() => {
    const list = [...apps];
    if (sortMode === 'name') {
      list.sort((a, b) => a.processName.localeCompare(b.processName, 'zh-CN'));
    } else {
      list.sort((a, b) => b.totalMs - a.totalMs);
    }
    return list;
  }, [apps, sortMode]);

  if (sorted.length === 0) {
    return <div className="empty-tip">暂无应用使用数据</div>;
  }

  return (
    <div>
      {sorted.map((item) => (
        <div className="app-row" key={item.processName}>
          <AppIcon
            exePath={item.exePath}
            processName={item.processName}
            getIcon={getIcon}
          />
          <div className="app-meta">
            <div className="app-top">
              <div className="app-name">{item.processName}</div>
              <div className="app-time">{formatDuration(item.totalMs)}</div>
            </div>
            <div className="bar-track">
              <div
                className="bar-fill"
                style={{ width: `${Math.max(4, Math.round(item.ratio * 100))}%` }}
              />
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
