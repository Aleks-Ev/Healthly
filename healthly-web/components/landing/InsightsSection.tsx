'use client';

// Секция «A little insight. A healthier you.» — три виджета из мобильного макета Figma:
//   «Your blood, decoded»      — горизонтальные шкалы показателей;
//   «A moment for you»         — скруглённые столбцы по дням недели (последний — акцентный);
//   «Progress, not perfection» — плавная линия тренда с выбором периода и подсказкой при наведении.
// Все данные иллюстративные (подпись «Sample data»).

import { useId, useMemo, useState, type ReactNode, type PointerEvent } from 'react';
import { ArrowUpRight, CircleCheck, Droplet, LineChart, Sun, TrendingUp, type LucideIcon } from 'lucide-react';
import { useI18n } from '@/lib/i18n';
import { CHART_COLORS, LevelBar, smoothPath } from '@/components/charts/primitives';
import type { LandingTheme, SectionProps } from './theme';

const WEEK = [38, 46, 40, 58, 52, 66, 82];

// Средний сон по месяцам, часы (июль 2025 — июнь 2026)
const SLEEP_HOURS = [6.9, 7.05, 6.8, 7.1, 7.0, 6.95, 7.25, 7.15, 7.4, 7.35, 7.75, 7.9];
type Period = '3m' | '6m' | '12m';
const PERIOD_MONTHS: Record<Period, number> = { '3m': 3, '6m': 6, '12m': 12 };

export default function InsightsSection({ theme }: SectionProps) {
  const { t } = useI18n();
  const s = t.insights;

  return (
    <section id="insights" className="mx-auto max-w-7xl px-4 py-16 sm:px-8 sm:py-24 lg:px-12">
      <div className="max-w-2xl space-y-4">
        <div className="text-[11px] font-semibold uppercase tracking-[0.18em]" style={{ color: theme.textSecondary }}>{s.eyebrow}</div>
        <h2 className="font-serif-aesthetic text-[40px] leading-[1.05] sm:text-6xl" style={{ color: theme.textPrimary }}>{s.title}</h2>
        <p className="max-w-md text-base leading-relaxed" style={{ color: theme.textSecondary }}>{s.text}</p>
      </div>

      <div className="mt-10 grid grid-cols-1 gap-5 md:grid-cols-2 lg:grid-cols-3 lg:gap-6">
        <BloodWidget theme={theme} />
        <MomentWidget theme={theme} />
        <ProgressWidget theme={theme} />
      </div>

      <div className="mt-4 text-right text-[11px]" style={{ color: theme.textSecondary }}>{s.sample}</div>
    </section>
  );
}

function Widget({ theme, icon: Icon, title, meta, footer, children }: { theme: LandingTheme; icon: LucideIcon; title: string; meta: string; footer: string; children: ReactNode }) {
  return (
    <article className="flex flex-col rounded-[28px] p-5 shadow-[0_1px_2px_rgba(42,48,43,0.04),0_14px_34px_-22px_rgba(42,48,43,0.25)] sm:p-6" style={{ backgroundColor: theme.bgSecondary }}>
      <header className="flex items-start gap-3">
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-white/90">
          <Icon className="h-4 w-4" style={{ color: theme.textPrimary }} strokeWidth={1.75} />
        </span>
        <div className="min-w-0 flex-1">
          <h3 className="text-[15px] font-semibold leading-tight" style={{ color: theme.textPrimary }}>{title}</h3>
          <div className="mt-0.5 text-[10px] font-medium uppercase tracking-[0.12em]" style={{ color: theme.textSecondary }}>{meta}</div>
        </div>
        <ArrowUpRight className="h-4 w-4 shrink-0" style={{ color: theme.textSecondary }} />
      </header>
      <div className="flex-1 pt-5">{children}</div>
      <p className="mt-5 text-[13px] leading-relaxed" style={{ color: theme.textSecondary }}>{footer}</p>
    </article>
  );
}

function BloodWidget({ theme }: SectionProps) {
  const { t } = useI18n();
  const b = t.insights.blood;
  const rows = [
    { label: b.vitaminD, value: '78', unit: 'nmol/L', level: 0.62 },
    { label: b.cholesterol, value: '4.2', unit: 'mmol/L', level: 0.5 },
  ];

  return (
    <Widget theme={theme} icon={Droplet} title={b.title} meta={b.meta} footer={b.footer}>
      <div className="font-serif-aesthetic text-[30px] leading-[1.1]" style={{ color: theme.textPrimary }}>{b.heading}</div>
      <div className="mt-2 flex items-center gap-1.5 text-xs" style={{ color: theme.textSecondary }}>
        <CircleCheck className="h-3.5 w-3.5" style={{ color: theme.accentGreen }} />
        {b.summary}
      </div>
      <div className="mt-5 space-y-5 rounded-2xl bg-white/85 p-4">
        {rows.map((row) => (
          <div key={row.label}>
            <div className="flex items-baseline justify-between gap-3 text-xs" style={{ color: theme.textPrimary }}>
              <span>{row.label}</span>
              <span>
                <span className="text-lg font-semibold tabular-nums">{row.value}</span>{' '}
                <span className="text-[10px]" style={{ color: theme.textMuted }}>{row.unit}</span>
              </span>
            </div>
            <LevelBar value={row.level} color={theme.accentSoft} className="mt-2" />
          </div>
        ))}
      </div>
    </Widget>
  );
}

function MomentWidget({ theme }: SectionProps) {
  const { t } = useI18n();
  const m = t.insights.moment;
  const max = Math.max(...WEEK);

  return (
    <Widget theme={theme} icon={Sun} title={m.title} meta={m.meta} footer={m.footer}>
      <div className="flex items-end justify-between gap-3">
        <div className="font-serif-aesthetic text-6xl leading-none" style={{ color: theme.textPrimary }}>
          82<span className="font-sans-aesthetic text-sm" style={{ color: theme.textMuted }}>/100</span>
        </div>
        <span className="mb-1 inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-[11px] font-medium" style={{ backgroundColor: theme.accentMint, color: theme.accentMintText }}>
          <TrendingUp className="h-3 w-3" />
          {m.delta}
        </span>
      </div>
      <div className="mt-2 text-xs" style={{ color: theme.textSecondary }}>{m.caption}</div>

      {/* Столбцы: мягкие скругления, одинаковый шаг, акцент только на сегодняшнем дне */}
      <div className="mt-6 grid grid-cols-7 items-end gap-2.5" style={{ height: 96 }} role="img" aria-label={WEEK.map((v, i) => `${m.days[i]} ${v}`).join(', ')}>
        {WEEK.map((value, index) => {
          const isToday = index === WEEK.length - 1;
          return (
            <div
              key={index}
              className="w-full rounded-[7px] transition-[height] duration-500"
              style={{ height: `${(value / max) * 100}%`, backgroundColor: isToday ? theme.bgDark : theme.accentSoft, opacity: isToday ? 1 : 0.85 }}
            />
          );
        })}
      </div>
      <div className="mt-2 grid grid-cols-7 gap-2.5 text-center text-[10px]" style={{ color: theme.textMuted }} aria-hidden>
        {m.days.map((day, index) => <span key={index}>{day}</span>)}
      </div>
    </Widget>
  );
}

const CHART_W = 300;
const CHART_H = 110;

function ProgressWidget({ theme }: SectionProps) {
  const { t, intlLocale } = useI18n();
  const p = t.insights.progress;
  const [period, setPeriod] = useState<Period>('6m');
  const [hover, setHover] = useState<number | null>(null);
  const gradientId = useId();

  const { points, path, area, labels, values, average } = useMemo(() => {
    const count = PERIOD_MONTHS[period];
    const series = SLEEP_HOURS.slice(-count);
    // Ось Y с запасом, чтобы линия не прилипала к краям
    const low = Math.min(...series) - 0.25;
    const high = Math.max(...series) + 0.2;
    const pts = series.map((value, index) => ({
      x: count === 1 ? CHART_W / 2 : (index / (count - 1)) * CHART_W,
      y: CHART_H - ((value - low) / (high - low)) * CHART_H,
    }));
    const line = smoothPath(pts);
    const monthLabels = series.map((_, index) => {
      const monthIndex = 5 - (count - 1 - index); // последний месяц — июнь 2026
      return new Date(Date.UTC(2026, monthIndex, 1)).toLocaleDateString(intlLocale, { month: 'short', timeZone: 'UTC' });
    });
    return {
      points: pts,
      path: line,
      area: `${line} L${CHART_W},${CHART_H} L0,${CHART_H} Z`,
      labels: monthLabels,
      values: series,
      average: series.reduce((sum, v) => sum + v, 0) / series.length,
    };
  }, [period, intlLocale]);

  const formatSleep = (hours: number) => `${Math.floor(hours)}h ${String(Math.round((hours % 1) * 60)).padStart(2, '0')}m`;

  // Ближайшая точка к курсору (подсказка)
  const handlePointer = (event: PointerEvent<SVGSVGElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * CHART_W;
    let nearest = 0;
    points.forEach((point, index) => {
      if (Math.abs(point.x - x) < Math.abs(points[nearest].x - x)) {
        nearest = index;
      }
    });
    setHover(nearest);
  };

  const active = hover !== null && hover < points.length ? hover : null;
  // Метки оси: для 12 месяцев — через одну, чтобы не слипались
  const labelStep = labels.length > 6 ? 2 : 1;

  return (
    <Widget theme={theme} icon={LineChart} title={p.title} meta={p.meta} footer={p.footer}>
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="text-xs" style={{ color: theme.textSecondary }}>{p.label}</div>
          <div className="mt-1 font-serif-aesthetic text-[40px] leading-none tabular-nums" style={{ color: theme.textPrimary }}>
            {formatSleep(active !== null ? values[active] : average)}
          </div>
        </div>
        <label className="relative">
          <span className="sr-only">{p.periodLabel}</span>
          <select
            value={period}
            onChange={(event) => {
              setPeriod(event.target.value as Period);
              setHover(null);
            }}
            className="appearance-none rounded-full bg-white py-1.5 pl-3.5 pr-8 text-[11px] font-medium shadow-[0_1px_2px_rgba(42,48,43,0.08)] outline-none focus-visible:ring-2 focus-visible:ring-[#737f69]/30"
            style={{ color: theme.textPrimary }}
          >
            {(Object.keys(PERIOD_MONTHS) as Period[]).map((key) => (
              <option key={key} value={key}>{p.periods[key]}</option>
            ))}
          </select>
          <svg viewBox="0 0 12 12" className="pointer-events-none absolute right-3 top-1/2 h-2.5 w-2.5 -translate-y-1/2" aria-hidden>
            <path d="M2.5 4.5 6 8l3.5-3.5" fill="none" stroke={theme.textSecondary} strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </label>
      </div>

      <div className="relative mt-5">
        <svg
          viewBox={`0 -8 ${CHART_W} ${CHART_H + 16}`}
          className="h-[124px] w-full touch-none overflow-visible"
          preserveAspectRatio="none"
          onPointerMove={handlePointer}
          onPointerDown={handlePointer}
          onPointerLeave={() => setHover(null)}
          role="img"
          aria-label={labels.map((label, index) => `${label} ${formatSleep(values[index])}`).join(', ')}
        >
          <defs>
            <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stopColor={CHART_COLORS.area} stopOpacity="0.18" />
              <stop offset="1" stopColor={CHART_COLORS.area} stopOpacity="0" />
            </linearGradient>
          </defs>
          {/* Только три тихие горизонтальные линии — без рамки и вертикальной сетки */}
          {[0.15, 0.5, 0.85].map((ratio) => (
            <line key={ratio} x1="0" x2={CHART_W} y1={CHART_H * ratio} y2={CHART_H * ratio} stroke={CHART_COLORS.grid} strokeWidth="1" vectorEffect="non-scaling-stroke" />
          ))}
          <path d={area} fill={`url(#${gradientId})`} />
          <path d={path} fill="none" stroke={CHART_COLORS.line} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" vectorEffect="non-scaling-stroke" />
          {active !== null && (
            <line x1={points[active].x} x2={points[active].x} y1="-8" y2={CHART_H + 8} stroke={CHART_COLORS.axis} strokeOpacity="0.5" strokeDasharray="2 3" vectorEffect="non-scaling-stroke" />
          )}
        </svg>
        {/* Точка поверх SVG (не растягивается вместе с preserveAspectRatio="none") */}
        {active !== null && (
          <span
            className="pointer-events-none absolute h-3 w-3 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white shadow-[0_1px_4px_rgba(42,48,43,0.3)]"
            style={{ left: `${(points[active].x / CHART_W) * 100}%`, top: `${((points[active].y + 8) / (CHART_H + 16)) * 100}%`, backgroundColor: CHART_COLORS.line }}
          />
        )}
      </div>

      <div className="mt-2 flex justify-between text-[10px]" style={{ color: theme.textMuted }} aria-hidden>
        {labels.map((label, index) => (
          <span key={`${label}-${index}`} className={(labels.length - 1 - index) % labelStep === 0 ? '' : 'invisible'}>{label}</span>
        ))}
      </div>
    </Widget>
  );
}
