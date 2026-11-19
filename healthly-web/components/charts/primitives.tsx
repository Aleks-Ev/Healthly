// Общие «примитивы» графиков в стиле макетов Figma — для лендинга и личного кабинета:
//   smoothPath — плавная кривая через точки (без «перелётов» за значения, как у monotone в Recharts);
//   LevelBar   — горизонтальная полоса уровня (виджет «Your blood, decoded»);
//   RangeScale — медицинская шкала: зона нормы, критические края и маркер значения.
// Цвета спокойные: шалфей для нормы, тёплая терракота для отклонения, глубокий красный — только для критического.

export const CHART_COLORS = {
  ink: '#2a302b',
  line: '#4f6352',       // основная линия — глубокий шалфей, а не «системный» синий
  lineSoft: '#9fb09a',
  area: '#7f9479',       // заливка под линией (через градиент с прозрачностью)
  track: '#e7ebe3',      // фон шкал
  band: '#dfe8db',       // зона нормы
  bandEdge: '#b9c8b3',
  grid: '#e9ece4',
  axis: '#9aa19a',
  abnormal: '#c0703f',   // вне нормы — терракота (заметно, но без тревожного «алого»)
  critical: '#a4302a',   // критическое — глубокий красный
  surface: '#ffffff',
};

type Point = { x: number; y: number };

// Монотонная кубическая интерполяция (Fritsch–Carlson): кривая проходит через все точки и не выходит
// за соседние значения — для медицинских данных это важно (нет «выдуманных» пиков между измерениями)
export function smoothPath(points: Point[]): string {
  if (points.length === 0) {
    return '';
  }
  if (points.length === 1) {
    return `M${points[0].x},${points[0].y}`;
  }

  const n = points.length;
  const dx: number[] = [];
  const slopes: number[] = [];
  for (let i = 0; i < n - 1; i++) {
    dx.push(points[i + 1].x - points[i].x);
    slopes.push((points[i + 1].y - points[i].y) / (dx[i] || 1));
  }

  const tangents: number[] = [slopes[0]];
  for (let i = 1; i < n - 1; i++) {
    tangents.push(slopes[i - 1] * slopes[i] <= 0 ? 0 : (slopes[i - 1] + slopes[i]) / 2);
  }
  tangents.push(slopes[n - 2]);

  for (let i = 0; i < n - 1; i++) {
    if (slopes[i] === 0) {
      tangents[i] = 0;
      tangents[i + 1] = 0;
      continue;
    }
    const a = tangents[i] / slopes[i];
    const b = tangents[i + 1] / slopes[i];
    const sum = a * a + b * b;
    if (sum > 9) {
      const k = 3 / Math.sqrt(sum);
      tangents[i] = k * a * slopes[i];
      tangents[i + 1] = k * b * slopes[i];
    }
  }

  let path = `M${points[0].x.toFixed(2)},${points[0].y.toFixed(2)}`;
  for (let i = 0; i < n - 1; i++) {
    const h = dx[i] / 3;
    const c1x = points[i].x + h;
    const c1y = points[i].y + tangents[i] * h;
    const c2x = points[i + 1].x - h;
    const c2y = points[i + 1].y - tangents[i + 1] * h;
    path += ` C${c1x.toFixed(2)},${c1y.toFixed(2)} ${c2x.toFixed(2)},${c2y.toFixed(2)} ${points[i + 1].x.toFixed(2)},${points[i + 1].y.toFixed(2)}`;
  }
  return path;
}

// Полоса уровня: заполнено value от 0 до 1 (виджеты лендинга)
export function LevelBar({ value, color = CHART_COLORS.lineSoft, className = '' }: { value: number; color?: string; className?: string }) {
  const percent = Math.max(0, Math.min(1, value)) * 100;
  return (
    <div className={`h-1.5 w-full overflow-hidden rounded-full ${className}`} style={{ backgroundColor: CHART_COLORS.track }}>
      <div className="h-full rounded-full" style={{ width: `${percent}%`, backgroundColor: color }} />
    </div>
  );
}

interface RangeScaleProps {
  value?: number | null;
  min: number | null;
  max: number | null;
  criticalLow?: number | null;
  criticalHigh?: number | null;
  isAbnormal?: boolean;
  isCritical?: boolean;
  className?: string;
  // Подпись для экранных дикторов, например «Glucose 5.4 mmol/L, normal 3.9–5.6»
  label?: string;
}

// Шкала нормы. Масштаб — от критических порогов (если есть) или с запасом вокруг нормы;
// значение за пределами шкалы прижимается к краю (маркер показывает «далеко за нормой»)
export function RangeScale({ value, min, max, criticalLow, criticalHigh, isAbnormal = false, isCritical = false, className = '', label }: RangeScaleProps) {
  if (min === null && max === null) {
    return null;
  }

  const span = min !== null && max !== null ? max - min || Math.abs(max) || 1 : Math.abs((max ?? min) as number) || 1;
  const low = criticalLow ?? (min !== null ? min - span * 0.6 : 0);
  const high = criticalHigh ?? (max !== null ? max + span * 0.6 : (min as number) + span * 1.6);
  const scaleMin = Math.min(low, value ?? low, min ?? low);
  const scaleMax = Math.max(high, value ?? high, max ?? high);
  const toPercent = (v: number) => ((v - scaleMin) / (scaleMax - scaleMin || 1)) * 100;

  const bandStart = toPercent(min ?? scaleMin);
  const bandEnd = toPercent(max ?? scaleMax);
  const markerColor = isCritical ? CHART_COLORS.critical : isAbnormal ? CHART_COLORS.abnormal : CHART_COLORS.line;

  return (
    <div className={`relative h-3 ${className}`} role={label ? 'img' : undefined} aria-label={label} aria-hidden={label ? undefined : true}>
      <div className="absolute inset-x-0 top-1/2 h-1.5 -translate-y-1/2 rounded-full" style={{ backgroundColor: CHART_COLORS.track }} />
      {criticalLow !== null && criticalLow !== undefined && (
        <div className="absolute top-1/2 h-1.5 -translate-y-1/2 rounded-l-full" style={{ left: 0, width: `${toPercent(criticalLow)}%`, backgroundColor: '#f0dcd6' }} />
      )}
      {criticalHigh !== null && criticalHigh !== undefined && (
        <div className="absolute top-1/2 h-1.5 -translate-y-1/2 rounded-r-full" style={{ left: `${toPercent(criticalHigh)}%`, right: 0, backgroundColor: '#f0dcd6' }} />
      )}
      <div
        className="absolute top-1/2 h-1.5 -translate-y-1/2 rounded-full"
        style={{ left: `${bandStart}%`, width: `${Math.max(bandEnd - bandStart, 1.5)}%`, backgroundColor: CHART_COLORS.band, boxShadow: `inset 0 0 0 1px ${CHART_COLORS.bandEdge}` }}
      />
      {value !== null && value !== undefined && (
        <div
          className="absolute top-1/2 h-3 w-3 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white"
          style={{ left: `${Math.max(2, Math.min(98, toPercent(value)))}%`, backgroundColor: markerColor, boxShadow: '0 1px 3px rgba(42,48,43,0.25)' }}
        />
      )}
    </div>
  );
}
