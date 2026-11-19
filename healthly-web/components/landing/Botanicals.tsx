// Декоративная графика лендинга: оливковые веточки (как в мобильном макете Figma) и сцена «рассвет над холмами»
// для первого экрана вместо фотографии. Чистый SVG — без картинок, чётко на любом экране.

interface BranchProps {
  className?: string;
  // Зеркально — для пары веточек
  flip?: boolean;
  leaves?: number;
  colors?: [string, string];
}

// Точка и направление на квадратичной кривой Безье (стебель)
function pointOnCurve(t: number, p0: [number, number], c: [number, number], p1: [number, number]) {
  const x = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * c[0] + t ** 2 * p1[0];
  const y = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * c[1] + t ** 2 * p1[1];
  const dx = 2 * (1 - t) * (c[0] - p0[0]) + 2 * t * (p1[0] - c[0]);
  const dy = 2 * (1 - t) * (c[1] - p0[1]) + 2 * t * (p1[1] - c[1]);
  return { x, y, angle: (Math.atan2(dy, dx) * 180) / Math.PI };
}

export function OliveBranch({ className = '', flip = false, leaves = 9, colors = ['#8c9d87', '#566b59'] }: BranchProps) {
  const p0: [number, number] = [20, 190];
  const c: [number, number] = [40, 90];
  const p1: [number, number] = [120, 14];

  const leafShapes = Array.from({ length: leaves }, (_, index) => {
    const t = 0.14 + (index / (leaves - 1)) * 0.82;
    const { x, y, angle } = pointOnCurve(t, p0, c, p1);
    const side = index % 2 === 0 ? 1 : -1;
    // Листья к верхушке мельче
    const scale = 1.15 - t * 0.45;
    return { x, y, rotate: angle + side * 48, scale, fill: colors[index % 3 === 0 ? 1 : 0], key: index };
  });

  return (
    <svg viewBox="0 0 140 200" className={className} style={flip ? { transform: 'scaleX(-1)' } : undefined} aria-hidden>
      <path d={`M${p0[0]} ${p0[1]} Q ${c[0]} ${c[1]} ${p1[0]} ${p1[1]}`} stroke="#5a6d5c" strokeWidth="2" fill="none" strokeLinecap="round" />
      {leafShapes.map(({ x, y, rotate, scale, fill, key }) => (
        <path
          key={key}
          d="M0 0 C 8 -9, 26 -9, 36 0 C 26 9, 8 9, 0 0 Z"
          fill={fill}
          transform={`translate(${x.toFixed(1)} ${y.toFixed(1)}) rotate(${rotate.toFixed(1)}) scale(${scale.toFixed(2)})`}
        />
      ))}
    </svg>
  );
}

// Пара веточек с «ягодками» — мобильный первый экран и финальный блок
export function BranchPair({ className = '' }: { className?: string }) {
  return (
    <div className={`relative flex items-end justify-center gap-6 ${className}`} aria-hidden>
      <OliveBranch className="h-36 w-auto -rotate-12" />
      <span className="absolute left-[38%] top-[62%] h-1.5 w-1.5 rounded-full bg-[#b39b7a]" />
      <span className="absolute left-[44%] top-[70%] h-1 w-1 rounded-full bg-[#b39b7a]" />
      <span className="absolute right-[30%] top-[38%] h-1.5 w-1.5 rounded-full bg-[#b39b7a]" />
      <OliveBranch className="h-36 w-auto rotate-12" flip colors={['#7f9279', '#4c604f']} />
    </div>
  );
}

// Сцена для первого экрана: тёплое небо, солнце, мягкие холмы и веточка на переднем плане
export function SunriseScene({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 600 640" preserveAspectRatio="xMidYMid slice" className={className} aria-hidden>
      <defs>
        <linearGradient id="sky" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#efe6d6" />
          <stop offset="0.45" stopColor="#f3dcb6" />
          <stop offset="0.75" stopColor="#e9d3ae" />
          <stop offset="1" stopColor="#dfe3d2" />
        </linearGradient>
        <radialGradient id="sun" cx="0.5" cy="0.5" r="0.5">
          <stop offset="0" stopColor="#fff6e3" />
          <stop offset="0.35" stopColor="#f8deaa" stopOpacity="0.95" />
          <stop offset="1" stopColor="#f3d29a" stopOpacity="0" />
        </radialGradient>
        <linearGradient id="hillFar" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#c9cdb6" />
          <stop offset="1" stopColor="#b7bea6" />
        </linearGradient>
        <linearGradient id="hillMid" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#a5b096" />
          <stop offset="1" stopColor="#8e9c82" />
        </linearGradient>
        <linearGradient id="hillNear" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#7d8d72" />
          <stop offset="1" stopColor="#5f6f58" />
        </linearGradient>
      </defs>

      <rect width="600" height="640" fill="url(#sky)" />
      <circle cx="380" cy="330" r="190" fill="url(#sun)" />
      <circle cx="380" cy="330" r="46" fill="#fff4dc" opacity="0.9" />

      <path d="M0 400 C 110 350, 210 380, 300 360 S 480 320, 600 350 V640 H0Z" fill="url(#hillFar)" opacity="0.9" />
      <path d="M0 460 C 120 420, 230 470, 340 440 S 520 400, 600 430 V640 H0Z" fill="url(#hillMid)" />
      <path d="M0 540 C 140 500, 260 540, 380 520 S 540 490, 600 510 V640 H0Z" fill="url(#hillNear)" />

      {/* Тонкая «линия пульса» на горизонте — отсылка к медицине, почти незаметная */}
      <path d="M40 470 H190 l12 -22 l14 40 l12 -18 H330" stroke="#ffffff" strokeOpacity="0.35" strokeWidth="2" fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
