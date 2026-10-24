'use client';

// Секция «Health Trends»: линейный график одного показателя во времени.
// У показателей разные единицы и масштабы (глюкоза ~5 ммоль/л, гемоглобин ~140 г/л), поэтому
// на графике всегда один показатель (одна ось Y), а переключение — кнопками над графиком.
// Если ни у одного показателя нет хотя бы двух измерений, показываем демо-график с призывом загрузить анализы.

import { useState } from 'react';
import { AlertTriangle, CheckCircle2, Table2, TrendingDown, TrendingUp, Upload } from 'lucide-react';
import { CartesianGrid, Line, LineChart, ReferenceArea, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import Card from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import { formatDate } from '@/lib/format';
import type { IndicatorTrend } from '@/types/api';

// Цвета графика (линия — синий из проверенной палитры, отклонение — статусный «critical»)
const COLORS = {
  line: '#2a78d6',
  abnormal: '#d03b3b',
  surface: '#ffffff',
  band: '#f1ece2',
  grid: '#ece7dd',
  axis: '#8e9890',
};

interface ChartPoint {
  time: number;
  value: number;
  isAbnormal: boolean;
}

// Демо-данные для пустого состояния (выдуманные, помечены как «Sample data»)
const DEMO_TREND: IndicatorTrend = {
  testName: 'Glucose',
  unit: 'mmol/L',
  referenceRange: '3.9-5.6',
  referenceMin: 3.9,
  referenceMax: 5.6,
  points: [
    ['2026-01-15', 5.9, true],
    ['2026-03-12', 5.7, true],
    ['2026-05-20', 5.4, false],
    ['2026-07-18', 5.1, false],
    ['2026-09-30', 4.9, false],
  ].map(([date, value, isAbnormal], index) => ({
    documentId: index,
    measuredAt: `${date}T00:00:00Z`,
    value: value as number,
    isAbnormal: isAbnormal as boolean,
  })),
};

interface HealthTrendsProps {
  trends: IndicatorTrend[];
  onUpload: () => void;
  isUploading: boolean;
}

export default function HealthTrends({ trends, onUpload, isUploading }: HealthTrendsProps) {
  // Для графика нужны минимум две точки
  const chartable = trends.filter((trend) => trend.points.length >= 2);
  const [selectedName, setSelectedName] = useState<string | null>(null);
  const [isTableView, setIsTableView] = useState(false);

  if (chartable.length === 0) {
    return <TrendsEmptyState singlePointCount={trends.length} onUpload={onUpload} isUploading={isUploading} />;
  }

  // Если выбранный показатель пропал (например, удалили документ) — берём первый
  const selected = chartable.find((trend) => trend.testName === selectedName) ?? chartable[0];

  return (
    <Card
      title="Health Trends"
      actions={
        <Button variant="ghost" size="sm" onClick={() => setIsTableView((value) => !value)} aria-pressed={isTableView}>
          <Table2 className="w-3.5 h-3.5" />
          {isTableView ? 'Show chart' : 'Show table'}
        </Button>
      }
    >
      <p className="-mt-2 mb-4 text-xs text-[#8e9890]">How your key indicators change over time. Only numeric results are shown.</p>

      {chartable.length > 1 && (
        <div className="mb-5 flex flex-wrap gap-2" role="tablist" aria-label="Indicator">
          {chartable.map((trend) => {
            const isActive = trend.testName === selected.testName;
            return (
              <button
                key={trend.testName}
                type="button"
                role="tab"
                aria-selected={isActive}
                onClick={() => setSelectedName(trend.testName)}
                className={`rounded-full border px-3 py-1.5 text-xs font-medium transition ${
                  isActive ? 'border-[#242b26] bg-[#242b26] text-white' : 'border-[#e1e7dc] bg-white text-[#4a524c] hover:bg-[#f6f7f2]'
                }`}
              >
                {trend.testName}
              </button>
            );
          })}
        </div>
      )}

      <TrendSummary trend={selected} />
      {isTableView ? <TrendTable trend={selected} /> : <TrendChart trend={selected} />}
    </Card>
  );
}

function TrendsEmptyState({ singlePointCount, onUpload, isUploading }: { singlePointCount: number; onUpload: () => void; isUploading: boolean }) {
  const hasSomeResults = singlePointCount > 0;

  return (
    <Card title="Health Trends">
      <div className="relative">
        {/* Демо-график приглушён и недоступен для мыши, чтобы его не приняли за настоящие данные */}
        <div className="pointer-events-none select-none opacity-40 blur-[1px]" aria-hidden>
          <TrendChart trend={DEMO_TREND} />
        </div>
        <div className="absolute inset-0 flex items-center justify-center p-4">
          <div className="max-w-sm rounded-2xl border border-[#ebe4d6] bg-[#fffdf9]/95 px-6 py-5 text-center shadow-lg shadow-[#8a7a5c]/10">
            <span className="inline-flex rounded-full bg-[#f1ece2] px-2.5 py-0.5 text-[11px] font-medium uppercase tracking-wide text-[#6b756e]">Sample data</span>
            <h3 className="mt-3 text-base font-semibold text-[#242b26]">
              {hasSomeResults ? 'Add one more lab result to see your trends' : 'See how your health changes over time'}
            </h3>
            <p className="mt-1.5 text-sm text-[#6b756e]">
              {hasSomeResults
                ? `You have results for ${singlePointCount} ${singlePointCount === 1 ? 'indicator' : 'indicators'}. A trend needs at least two measurements of the same test.`
                : 'Upload your lab results and track Glucose, Cholesterol, Hemoglobin and more.'}
            </p>
            <Button className="mt-4" onClick={onUpload} isLoading={isUploading}>
              {!isUploading && <Upload className="w-4 h-4" />}
              {hasSomeResults ? 'Upload lab result' : 'Upload first lab result'}
            </Button>
          </div>
        </div>
      </div>
    </Card>
  );
}

// Последнее значение, изменение относительно предыдущего и референсный диапазон
function TrendSummary({ trend }: { trend: IndicatorTrend }) {
  const latest = trend.points[trend.points.length - 1];
  const previous = trend.points[trend.points.length - 2];
  const delta = latest.value - previous.value;
  const DeltaIcon = delta >= 0 ? TrendingUp : TrendingDown;

  return (
    <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
      <div>
        <div className="text-sm font-medium text-[#242b26]">{trend.testName}</div>
        <div className="mt-1 flex items-baseline gap-2">
          <span className="text-3xl font-semibold tabular-nums text-[#242b26]">{formatValue(latest.value)}</span>
          <span className="text-sm text-[#6b756e]">{trend.unit}</span>
          <StatusLabel isAbnormal={latest.isAbnormal} />
        </div>
        <div className="mt-1 flex items-center gap-1 text-xs text-[#6b756e]">
          <DeltaIcon className="w-3.5 h-3.5" />
          {delta === 0 ? 'No change' : `${delta > 0 ? '+' : '−'}${formatValue(Math.abs(delta))} ${trend.unit}`} since {formatDate(previous.measuredAt)}
        </div>
      </div>
      {trend.referenceRange && (
        <div className="flex items-center gap-2 text-xs text-[#6b756e]">
          <span className="inline-block h-3 w-5 rounded-sm" style={{ backgroundColor: COLORS.band }} />
          Reference range: {trend.referenceRange} {trend.unit}
        </div>
      )}
    </div>
  );
}

function StatusLabel({ isAbnormal }: { isAbnormal: boolean }) {
  return isAbnormal ? (
    <span className="inline-flex items-center gap-1 text-xs font-medium text-red-700">
      <AlertTriangle className="w-3.5 h-3.5" />
      Out of range
    </span>
  ) : (
    <span className="inline-flex items-center gap-1 text-xs font-medium text-[#3a5235]">
      <CheckCircle2 className="w-3.5 h-3.5" />
      Normal
    </span>
  );
}

function TrendChart({ trend }: { trend: IndicatorTrend }) {
  const data: ChartPoint[] = trend.points.map((point) => ({
    time: new Date(point.measuredAt).getTime(),
    value: point.value,
    isAbnormal: point.isAbnormal,
  }));

  // Ось Y охватывает и значения, и границы нормы; деления — «круглые» числа
  const values = [...data.map((point) => point.value), trend.referenceMin, trend.referenceMax].filter((v): v is number => v !== null);
  const yTicks = niceTicks(Math.min(...values), Math.max(...values));
  const yDomain: [number, number] = [yTicks[0], yTicks[yTicks.length - 1]];

  return (
    <div className="h-64 w-full">
      <ResponsiveContainer width="100%" height="100%" initialDimension={{ width: 600, height: 256 }}>
        <LineChart data={data} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
          <CartesianGrid vertical={false} stroke={COLORS.grid} />
          {(trend.referenceMin !== null || trend.referenceMax !== null) && (
            <ReferenceArea
              y1={trend.referenceMin ?? yDomain[0]}
              y2={trend.referenceMax ?? yDomain[1]}
              fill={COLORS.band}
              fillOpacity={1}
              stroke="none"
              ifOverflow="hidden"
            />
          )}
          <XAxis
            dataKey="time"
            type="number"
            scale="time"
            domain={['dataMin', 'dataMax']}
            padding={{ left: 16, right: 16 }}
            tickFormatter={(time: number) => new Date(time).toLocaleDateString('en-US', { month: 'short', year: 'numeric' })}
            tick={{ fontSize: 11, fill: COLORS.axis }}
            tickLine={false}
            axisLine={{ stroke: COLORS.grid }}
            minTickGap={24}
          />
          <YAxis
            domain={yDomain}
            ticks={yTicks}
            tickFormatter={formatValue}
            tick={{ fontSize: 11, fill: COLORS.axis }}
            tickLine={false}
            axisLine={false}
            width={44}
          />
          <Tooltip
            content={<TrendTooltip unit={trend.unit} />}
            cursor={{ stroke: COLORS.axis, strokeWidth: 1 }}
          />
          <Line
            type="monotone"
            dataKey="value"
            stroke={COLORS.line}
            strokeWidth={2}
            strokeLinecap="round"
            strokeLinejoin="round"
            dot={renderDot}
            activeDot={renderActiveDot}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

interface DotProps {
  cx?: number;
  cy?: number;
  index?: number;
  payload?: ChartPoint;
}

// Точка с белым кольцом; отклонения от нормы — красные
function renderDot({ cx, cy, index, payload }: DotProps) {
  return (
    <circle
      key={`dot-${index}`}
      cx={cx}
      cy={cy}
      r={4.5}
      fill={payload?.isAbnormal ? COLORS.abnormal : COLORS.line}
      stroke={COLORS.surface}
      strokeWidth={2}
    />
  );
}

function renderActiveDot({ cx, cy, payload }: DotProps) {
  return <circle cx={cx} cy={cy} r={6.5} fill={payload?.isAbnormal ? COLORS.abnormal : COLORS.line} stroke={COLORS.surface} strokeWidth={2} />;
}

interface TrendTooltipProps {
  unit: string;
  active?: boolean;
  payload?: ReadonlyArray<{ payload?: ChartPoint }>;
}

function TrendTooltip({ unit, active, payload }: TrendTooltipProps) {
  const point = payload?.[0]?.payload;
  if (!active || !point) {
    return null;
  }

  return (
    <div className="rounded-xl border border-[#e1e7dc] bg-white px-3 py-2 text-xs shadow-lg shadow-black/5">
      <div className="text-[#6b756e]">{formatDate(new Date(point.time).toISOString())}</div>
      <div className="mt-0.5 text-sm font-semibold text-[#242b26]">
        {formatValue(point.value)} <span className="font-normal text-[#6b756e]">{unit}</span>
      </div>
      <div className="mt-1">
        <StatusLabel isAbnormal={point.isAbnormal} />
      </div>
    </div>
  );
}

// Табличный вид тех же данных (доступность: можно прочитать без графика)
function TrendTable({ trend }: { trend: IndicatorTrend }) {
  return (
    <div className="overflow-x-auto -mx-5 sm:-mx-6">
      <table className="w-full min-w-[420px] text-sm">
        <thead>
          <tr className="text-left text-xs text-[#6b756e] border-b border-[#e1e7dc]">
            <th className="font-medium px-5 sm:px-6 py-2">Date</th>
            <th className="font-medium px-3 py-2">Value</th>
            <th className="font-medium px-5 sm:px-6 py-2">Status</th>
          </tr>
        </thead>
        <tbody>
          {[...trend.points].reverse().map((point, index) => (
            <tr key={`${point.documentId}-${index}`} className="border-b border-[#e1e7dc] last:border-0">
              <td className="px-5 sm:px-6 py-2.5">{formatDate(point.measuredAt)}</td>
              <td className="px-3 py-2.5 tabular-nums">
                {formatValue(point.value)} <span className="text-[#6b756e]">{trend.unit}</span>
              </td>
              <td className="px-5 sm:px-6 py-2.5"><StatusLabel isAbnormal={point.isAbnormal} /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// Деления оси с шагом 1/2/2.5/5 × 10^n, с запасом сверху и снизу: (4.9, 6.4) → [4.5, 5, 5.5, 6, 6.5]
function niceTicks(min: number, max: number, count = 4): number[] {
  const span = max - min || Math.abs(max) || 1;
  const rawStep = span / count;
  const magnitude = 10 ** Math.floor(Math.log10(rawStep));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * magnitude).find((s) => s >= rawStep) ?? 10 * magnitude;
  const start = Math.max(0, Math.floor((min - step * 0.25) / step) * step);
  const end = Math.ceil((max + step * 0.25) / step) * step;
  const ticks: number[] = [];
  for (let tick = start; tick <= end + step / 2; tick += step) {
    ticks.push(Number(tick.toFixed(10)));
  }
  return ticks;
}

// 5 → "5", 5.123 → "5.12"
function formatValue(value: number): string {
  return value.toLocaleString('en-US', { maximumFractionDigits: 2 });
}
