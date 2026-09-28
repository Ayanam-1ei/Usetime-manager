export type Period = 'day' | 'week' | 'month';

export interface Summary {
  period: Period;
  dateStr: string | null;
  start: number;
  end: number;
  totalMs: number;
  prevTotalMs: number;
  deltaMs: number;
}

export interface ChartPoint {
  label: string;
  ms: number;
}

export interface ChartData {
  period: Period;
  start: number;
  end: number;
  points: ChartPoint[];
}

export interface AppUsageItem {
  processName: string;
  exePath: string;
  totalMs: number;
  ratio: number;
}

export interface AppUsage {
  period: Period;
  start: number;
  end: number;
  totalMs: number;
  apps: AppUsageItem[];
}

export interface UsetimeApi {
  getSummary(period: Period, dateStr: string): Promise<Summary>;
  getChartData(period: Period, dateStr: string): Promise<ChartData>;
  getAppUsage(period: Period, dateStr: string): Promise<AppUsage>;
  getIcon(exePath: string): Promise<string | null>;
  showWindow(): Promise<void>;
  hideWindow(): Promise<void>;
  quitApp(): Promise<void>;
  getTrackerStatus(): Promise<{
    running: boolean;
    apiReady: boolean;
    pollMs: number;
    current: { processName: string; startTs: number; endTs: number } | null;
  }>;
  getAutostart(): Promise<boolean>;
  setAutostart(enabled: boolean): Promise<{ ok: boolean; reason: string | null }>;
}

declare global {
  interface Window {
    usetime: UsetimeApi;
  }
}

export {};
