'use client';

// Секция «Health Trends»: линейный график одного показателя во времени (стиль виджета Figma «Progress, not perfection»).
// У показателей разные единицы и масштабы (глюкоза ~5 ммоль/л, гемоглобин ~140 г/л), поэтому
// на графике всегда один показатель (одна ось Y), а переключение — сегментированными кнопками над графиком.
// Если ни у одного показателя нет хотя бы двух измерений, показываем демо-график с призывом загрузить анализы.
//
// Принципы оформления (чтобы график выглядел спокойно и читался как медицинский):
//   • линия — глубокий шалфей с мягкой заливкой вниз, без рамок и вертикальной сетки;
//   • полоса нормы — светло-мятная, с тонкими пунктирными границами и подписью «Normal»;
//   • обычные точки — полые (белые с обводкой), вне нормы — терракота, критические — тёмно-красные с ореолом;
//   • подсказка — карточка с датой, крупным значением и статусом; курсор — тонкая пунктирная вертикаль;
//   • выбор периода (All / 12 / 6 месяцев) и табличный вид тех же данных для доступности.

import { useId, useState } from 'react';
import { AlertTriangle, CheckCircle2, LineChart as LineChartIcon, Table2, TrendingDown, TrendingUp, Upload } from 'lucide-react';
import { Area, AreaChart, CartesianGrid, ReferenceArea, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import Card from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import { formatDate } from '@/lib/format';
import { CriticalBadge } from '@/components/ui/Badge';
import { CHART_COLORS, RangeScale } from '@/components/charts/primitives';
import type { IndicatorTrend } from '@/types/api';

interface ChartPoint {
  time: number;
  value: number;
  isAbnormal: boolean;
  isCritical: boolean;
}

type Period = 'all' | '12m' | '6m';
const PERIODS: { value: Period; label: string; months: number | null }[] = [
  { value: 'all', label: 'All time', months: null },
  { value: '12m', label: '12 months', months: 12 },
  { value: '6m', label: '6 months', months: 6 },
];

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
    isCritical: false,
  })),
};

interface HealthTrendsProps {
  trends: IndicatorTrend[];
  onUpload: () => void;
  isUploading: boolean;
}

// Точки за последние N месяцев (считая от последнего измерения, а не от сегодняшнего дня)
function pointsInPeriod(trend: IndicatorTrend, months: number | null) {
  if (months === null || trend.points.length === 0) {
    return trend.points;
  }
  const last = new Date(trend.points[trend.points.length - 1].measuredAt);
  const from = new Date(last);
  from.setMonth(from.getMonth() - months);
  return trend.points.filter((point) => new Date(point.measuredAt) >= from);
}

export default function HealthTrends({ trends, onUpload, isUploading }: HealthTrendsProps) {
  // Для графика нужны минимум две точки
  const chartable = trends.filter((trend) => trend.points.length >= 2);
  const [selectedName, setSelectedName] = useState<string | null>(null);
  const [isTableView, setIsTableView] = useState(false);
  const [period, setPeriod] = useState<Period>('all');

  if (chartable.length === 0) {
    return <TrendsEmptyState singlePointCount={trends.length} onUpload={onUpload} isUploading={isUploading} />;
  }

  // Если выбранный показатель пропал (например, удалили документ) — берём первый
  const selected = chartable.find((trend) => trend.testName === selectedName) ?? chartable[0];

  // Период, в котором осталось меньше двух точек, недоступен; выбранный «пустой» период → All time
  const availablePeriods = PERIODS.filter((option) => pointsInPeriod(selected, option.months).length >= 2);
  const activePeriod = availablePeriods.find((option) => option.value === period) ?? PERIODS[0];
  const visibleTrend: IndicatorTrend = { ...selected, points: pointsInPeriod(selected, activePeriod.months) };

  return (
    <Card>
      <div className="mb-5 flex flex-wrap items-start justify-between gap-3">
        <TrendsHeading />
        <div className="flex items-center gap-2">
          {availablePeriods.length > 1 && (
            <label className="relative">
              <span className="sr-only">Period</span>
              <select
                value={activePeriod.value}
                onChange={(event) => setPeriod(event.target.value as Period)}
                className="appearance-none rounded-full border border-[#e3e6dd] bg-white py-1.5 pl-3.5 pr-8 text-xs font-medium text-[#2a302b] outline-none transition hover:bg-[#fbfbf8] focus-visible:ring-2 focus-visible:ring-[#737f69]/30"
              >
                {availablePeriods.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
              <svg viewBox="0 0 12 12" className="pointer-events-none absolute right-3 top-1/2 h-2.5 w-2.5 -translate-y-1/2" aria-hidden>
                <path d="M2.5 4.5 6 8l3.5-3.5" fill="none" stroke="#6c736c" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </label>
          )}
          <Button variant="ghost" size="sm" onClick={() => setIsTableView((value) => !value)} aria-pressed={isTableView}>
            <Table2 className="w-3.5 h-3.5" />
            {isTableView ? 'Chart' : 'Table'}
          </Button>
        </div>
      </div>

      {chartable.length > 1 && (
        <div className="no-scrollbar -mx-1 mb-6 overflow-x-auto px-1 pb-1">
          <div className="inline-flex gap-1 rounded-full bg-[#eef0e9] p-1" role="tablist" aria-label="Indicator">
            {chartable.map((trend) => {
              const isActive = trend.testName === selected.testName;
              const latest = trend.points[trend.points.length - 1];
              return (
                <button
                  key={trend.testName}
                  type="button"
                  role="tab"
                  aria-selected={isActive}
                  onClick={() => setSelectedName(trend.testName)}
                  className={`inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-full px-3.5 py-1.5 text-xs font-medium transition ${
                    isActive ? 'bg-white text-[#2a302b] shadow-[0_1px_2px_rgba(42,48,43,0.08),0_2px_8px_-4px_rgba(42,48,43,0.15)]' : 'text-[#6c736c] hover:text-[#2a302b]'
                  }`}
                >
                  {/* Цвет точки — статус последнего измерения */}
                  <span className="h-1.5 w-1.5 rounded-full" style={{ backgroundColor: latest.isCritical ? CHART_COLORS.critical : latest.isAbnormal ? CHART_COLORS.abnormal : CHART_COLORS.lineSoft }} />
                  {trend.testName}
                </button>
              );
            })}
          </div>
        </div>
      )}

      <TrendSummary trend={visibleTrend} />
      {isTableView ? <TrendTable trend={visibleTrend} /> : <TrendChart trend={visibleTrend} />}
    </Card>
  );
}

function TrendsHeading() {
  return (
    <div className="flex items-start gap-3">
      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-[#eef0e9]">
        <LineChartIcon className="h-4 w-4 text-[#2a302b]" strokeWidth={1.75} />
      </span>
      <div>
        <h2 className="text-base font-semibold leading-tight text-[#2a302b]">Health Trends</h2>
        <p className="mt-0.5 text-[10px] font-medium uppercase tracking-[0.12em] text-[#8e9890]">Your results over time · numeric only</p>
      </div>
    </div>
  );
}

function TrendsEmptyState({ singlePointCount, onUpload, isUploading }: { singlePointCount: number; onUpload: () => void; isUploading: boolean }) {
  const hasSomeResults = singlePointCount > 0;

  return (
    <Card>
      <div className="mb-5">
        <TrendsHeading />
      </div>
      <div className="relative">
        {/* Демо-график приглушён и недоступен для мыши, чтобы его не приняли за настоящие данные */}
        <div className="pointer-events-none select-none opacity-35 blur-[1px]" aria-hidden>
          <TrendChart trend={DEMO_TREND} />
        </div>
        <div className="absolute inset-0 flex items-center justify-center p-4">
          <div className="max-w-sm rounded-3xl border border-[#ebede6] bg-white/95 px-6 py-5 text-center shadow-[0_24px_48px_-28px_rgba(42,48,43,0.35)]">
            <span className="inline-flex rounded-full bg-[#eef0e9] px-2.5 py-0.5 text-[11px] font-medium uppercase tracking-wide text-[#6b756e]">Sample data</span>
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

// Последнее значение крупно, изменение относительно предыдущего и шкала нормы
function TrendSummary({ trend }: { trend: IndicatorTrend }) {
  const latest = trend.points[trend.points.length - 1];
  const previous = trend.points[trend.points.length - 2];
  const delta = previous ? latest.value - previous.value : 0;
  const DeltaIcon = delta >= 0 ? TrendingUp : TrendingDown;
  const hasRange = trend.referenceMin !== null || trend.referenceMax !== null;

  return (
    <div className="mb-5 grid grid-cols-1 gap-5 sm:grid-cols-[1fr_minmax(200px,260px)] sm:items-end">
      <div className="min-w-0">
        <div className="text-xs text-[#6b756e]">{trend.testName} · latest</div>
        <div className="mt-1 flex flex-wrap items-baseline gap-x-2 gap-y-1">
          <span className="font-serif-aesthetic text-[44px] leading-none tabular-nums text-[#2a302b]">{formatValue(latest.value)}</span>
          <span className="text-sm text-[#6b756e]">{trend.unit}</span>
          <StatusLabel isAbnormal={latest.isAbnormal} isCritical={latest.isCritical} />
        </div>
        {previous && (
          <div className="mt-2 inline-flex items-center gap-1 rounded-full bg-[#eef0e9] px-2.5 py-1 text-[11px] text-[#4a524c]">
            <DeltaIcon className="h-3 w-3" />
            {delta === 0 ? 'No change' : `${delta > 0 ? '+' : '−'}${formatValue(Math.abs(delta))} ${trend.unit}`} since {formatDate(previous.measuredAt)}
          </div>
        )}
      </div>

      {hasRange && (
        <div className="text-xs text-[#6b756e]">
          <div className="mb-2 flex items-center justify-between gap-2">
            <span>Reference range</span>
            <span className="tabular-nums text-[#2a302b]">{trend.referenceRange} {trend.unit}</span>
          </div>
          <RangeScale
            value={latest.value}
            min={trend.referenceMin}
            max={trend.referenceMax}
            isAbnormal={latest.isAbnormal}
            isCritical={latest.isCritical}
            label={`Latest ${formatValue(latest.value)} ${trend.unit}, reference ${trend.referenceRange}`}
          />
        </div>
      )}
    </div>
  );
}

function StatusLabel({ isAbnormal, isCritical }: { isAbnormal: boolean; isCritical: boolean }) {
  if (isCritical) {
    return <CriticalBadge />;
  }
  return isAbnormal ? (
    <span className="inline-flex items-center gap-1 rounded-full bg-[#f6e6da] px-2 py-0.5 text-[11px] font-medium text-[#8a4a23]">
      <AlertTriangle className="h-3 w-3" />
      Out of range
    </span>
  ) : (
    <span className="inline-flex items-center gap-1 rounded-full bg-[#dfe8db] px-2 py-0.5 text-[11px] font-medium text-[#3d5340]">
      <CheckCircle2 className="h-3 w-3" />
      Normal
    </span>
  );
}

function TrendChart({ trend }: { trend: IndicatorTrend }) {
  const gradientId = useId().replace(/:/g, '');
  const data: ChartPoint[] = trend.points.map((point) => ({
    time: new Date(point.measuredAt).getTime(),
    value: point.value,
    isAbnormal: point.isAbnormal,
    isCritical: point.isCritical,
  }));

  // Ось Y охватывает и значения, и границы нормы; деления — «круглые» числа
  const values = [...data.map((point) => point.value), trend.referenceMin, trend.referenceMax].filter((v): v is number => v !== null);
  const yTicks = niceTicks(Math.min(...values), Math.max(...values));
  const yDomain: [number, number] = [yTicks[0], yTicks[yTicks.length - 1]];
  const hasBand = trend.referenceMin !== null || trend.referenceMax !== null;
  // Если все точки в пределах одного года — без года в подписях
  const sameYear = new Date(data[0].time).getFullYear() === new Date(data[data.length - 1].time).getFullYear();

  return (
    <div className="h-60 w-full sm:h-64">
      <ResponsiveContainer width="100%" height="100%" initialDimension={{ width: 600, height: 256 }}>
        <AreaChart data={data} margin={{ top: 12, right: 12, bottom: 0, left: -8 }}>
          <defs>
            <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={CHART_COLORS.area} stopOpacity={0.2} />
              <stop offset="100%" stopColor={CHART_COLORS.area} stopOpacity={0} />
            </linearGradient>
          </defs>
          <CartesianGrid vertical={false} stroke={CHART_COLORS.grid} strokeDasharray="3 5" />
          {hasBand && (
            <ReferenceArea
              y1={trend.referenceMin ?? yDomain[0]}
              y2={trend.referenceMax ?? yDomain[1]}
              fill={CHART_COLORS.band}
              fillOpacity={0.55}
              stroke="none"
              ifOverflow="hidden"
            />
          )}
          {trend.referenceMax !== null && (
            <ReferenceLine
              y={trend.referenceMax}
              stroke={CHART_COLORS.bandEdge}
              strokeDasharray="4 4"
              ifOverflow="hidden"
              label={{ value: 'Normal', position: 'insideTopRight', fill: '#7d8f78', fontSize: 10 }}
            />
          )}
          {trend.referenceMin !== null && (
            <ReferenceLine
              y={trend.referenceMin}
              stroke={CHART_COLORS.bandEdge}
              strokeDasharray="4 4"
              ifOverflow="hidden"
              label={trend.referenceMax === null ? { value: 'Normal', position: 'insideBottomRight', fill: '#7d8f78', fontSize: 10 } : undefined}
            />
          )}
          <XAxis
            dataKey="time"
            type="number"
            scale="time"
            domain={['dataMin', 'dataMax']}
            padding={{ left: 18, right: 18 }}
            tickFormatter={(time: number) => new Date(time).toLocaleDateString('en-US', sameYear ? { month: 'short' } : { month: 'short', year: '2-digit' })}
            tick={{ fontSize: 11, fill: CHART_COLORS.axis }}
            tickLine={false}
            axisLine={false}
            tickMargin={10}
            minTickGap={28}
          />
          <YAxis
            domain={yDomain}
            ticks={yTicks}
            tickFormatter={formatValue}
            tick={{ fontSize: 11, fill: CHART_COLORS.axis }}
            tickLine={false}
            axisLine={false}
            width={48}
          />
          <Tooltip
            content={<TrendTooltip unit={trend.unit} />}
            cursor={{ stroke: CHART_COLORS.axis, strokeWidth: 1, strokeDasharray: '3 4' }}
          />
          <Area
            type="monotone"
            dataKey="value"
            stroke={CHART_COLORS.line}
            strokeWidth={2.25}
            strokeLinecap="round"
            strokeLinejoin="round"
            fill={`url(#${gradientId})`}
            dot={renderDot}
            activeDot={renderActiveDot}
            isAnimationActive={false}
          />
        </AreaChart>
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

// Обычная точка — полая (не спорит с линией); вне нормы — терракотовая; критическая — тёмно-красная с ореолом
function renderDot({ cx, cy, index, payload }: DotProps) {
  if (cx === undefined || cy === undefined) {
    return <g key={`dot-${index}`} />;
  }
  if (payload?.isCritical) {
    return (
      <g key={`dot-${index}`}>
        <circle cx={cx} cy={cy} r={10} fill={CHART_COLORS.critical} fillOpacity={0.14} />
        <circle cx={cx} cy={cy} r={5} fill={CHART_COLORS.critical} stroke={CHART_COLORS.surface} strokeWidth={2} />
      </g>
    );
  }
  if (payload?.isAbnormal) {
    return <circle key={`dot-${index}`} cx={cx} cy={cy} r={4.5} fill={CHART_COLORS.abnormal} stroke={CHART_COLORS.surface} strokeWidth={2} />;
  }
  return <circle key={`dot-${index}`} cx={cx} cy={cy} r={3.75} fill={CHART_COLORS.surface} stroke={CHART_COLORS.line} strokeWidth={2} />;
}

function renderActiveDot({ cx, cy, payload }: DotProps) {
  const color = payload?.isCritical ? CHART_COLORS.critical : payload?.isAbnormal ? CHART_COLORS.abnormal : CHART_COLORS.line;
  return (
    <g>
      <circle cx={cx} cy={cy} r={11} fill={color} fillOpacity={0.12} />
      <circle cx={cx} cy={cy} r={5.5} fill={color} stroke={CHART_COLORS.surface} strokeWidth={2.5} />
    </g>
  );
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
    <div className="min-w-[132px] rounded-2xl border border-[#ebede6] bg-white/95 px-3.5 py-2.5 shadow-[0_12px_30px_-14px_rgba(42,48,43,0.35)] backdrop-blur">
      <div className="text-[11px] text-[#8e9890]">{formatDate(new Date(point.time).toISOString())}</div>
      <div className="mt-0.5 flex items-baseline gap-1">
        <span className="font-serif-aesthetic text-2xl leading-none tabular-nums text-[#2a302b]">{formatValue(point.value)}</span>
        <span className="text-xs text-[#6b756e]">{unit}</span>
      </div>
      <div className="mt-1.5">
        <StatusLabel isAbnormal={point.isAbnormal} isCritical={point.isCritical} />
      </div>
    </div>
  );
}

// Табличный вид тех же данных (доступность: можно прочитать без графика)
function TrendTable({ trend }: { trend: IndicatorTrend }) {
  return (
    <div className="overflow-x-auto -mx-5 sm:-mx-6">
      <table className="w-full min-w-[320px] text-sm">
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
              <td className="px-5 sm:px-6 py-2.5 whitespace-nowrap">{formatDate(point.measuredAt)}</td>
              <td className="px-3 py-2.5 tabular-nums whitespace-nowrap">
                {formatValue(point.value)} <span className="text-[#6b756e]">{trend.unit}</span>
              </td>
              <td className="px-5 sm:px-6 py-2.5"><StatusLabel isAbnormal={point.isAbnormal} isCritical={point.isCritical} /></td>
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
