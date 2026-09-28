import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import type { ChartPoint, Period } from '../types';
import { formatDuration } from '../utils/format';

interface Props {
  points: ChartPoint[];
  period: Period;
}

function bucketLabel(period: Period): string {
  if (period === 'day') return '小时';
  if (period === 'week') return '天';
  return '日';
}

export function TimeChart({ points, period }: Props) {
  const data = points.map((p) => ({
    name: p.label,
    minutes: Math.round(p.ms / 60000),
    ms: p.ms,
  }));

  const maxMin = Math.max(1, ...data.map((d) => d.minutes));
  // 日视图 x 轴标签稀疏显示
  const interval =
    period === 'day' ? 5 : period === 'month' ? (data.length > 20 ? 4 : 2) : 0;

  return (
    <ResponsiveContainer width="100%" height="100%">
      <BarChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 4 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
        <XAxis
          dataKey="name"
          tick={{ fill: '#9ca3af', fontSize: 12 }}
          tickLine={false}
          axisLine={{ stroke: '#e5e7eb' }}
          interval={interval}
        />
        <YAxis
          tick={{ fill: '#9ca3af', fontSize: 12 }}
          tickLine={false}
          axisLine={false}
          width={48}
          tickFormatter={(v: number) => (v >= 60 ? `${Math.round(v / 60)}h` : `${v}m`)}
          domain={[0, Math.ceil(maxMin * 1.15)]}
        />
        <Tooltip
          cursor={{ fill: 'rgba(43, 108, 255, 0.08)' }}
          contentStyle={{
            borderRadius: 12,
            border: '1px solid #e5e7eb',
            boxShadow: '0 8px 24px rgba(17,24,39,0.08)',
          }}
          formatter={(value: number, _name, item) => {
            const ms = (item?.payload as { ms?: number } | undefined)?.ms ?? value * 60000;
            return [formatDuration(ms), `${bucketLabel(period)}用量`];
          }}
        />
        <Bar
          dataKey="minutes"
          fill="#2b6cff"
          radius={[6, 6, 2, 2]}
          maxBarSize={period === 'day' ? 18 : 36}
        />
      </BarChart>
    </ResponsiveContainer>
  );
}
