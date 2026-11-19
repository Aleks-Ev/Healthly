'use client';

// Секция «A little insight. A world of possibility.» (в Figma — светлая, фото в «арке» слева).
// Вместо фотографии — спокойная иллюстрация лаборатории; список направлений анализов и кнопка.

import Link from 'next/link';
import { ArrowUpRight, FlaskConical } from 'lucide-react';
import { useI18n } from '@/lib/i18n';
import { OliveBranch } from './Botanicals';
import type { SectionProps } from './theme';

export default function TestingSection({ theme }: SectionProps) {
  const { t } = useI18n();
  const s = t.testing;

  return (
    <section id="testing" className="mx-auto max-w-7xl px-4 py-16 sm:px-8 sm:py-24 lg:px-12">
      <div className="grid grid-cols-1 items-center gap-10 lg:grid-cols-12 lg:gap-16">

        <div className="lg:col-span-5">
          <div className="relative mx-auto aspect-[5/4] w-full max-w-md overflow-hidden rounded-[36px] rounded-tl-[120px] shadow-[0_30px_60px_-34px_rgba(42,48,43,0.45)] lg:aspect-[4/5] lg:max-w-none"
               style={{ background: 'linear-gradient(160deg, #e9eee6 0%, #d9e2d6 55%, #c9d5c5 100%)' }}>
            <LabIllustration />
            <OliveBranch className="absolute -right-4 -top-2 h-40 w-auto rotate-[200deg] opacity-60" />
            <div className="absolute inset-x-4 bottom-4 flex items-center gap-3 rounded-2xl border border-white/60 bg-white/55 px-4 py-3 backdrop-blur-md sm:inset-x-6 sm:bottom-6">
              <FlaskConical className="h-4 w-4 shrink-0" style={{ color: theme.accentGreen }} />
              <div className="min-w-0">
                <div className="text-xs font-medium" style={{ color: theme.textPrimary }}>{s.badgeTitle}</div>
                <div className="text-[11px]" style={{ color: theme.textSecondary }}>{s.badgeText}</div>
              </div>
            </div>
          </div>
        </div>

        <div className="lg:col-span-7">
          <div className="text-[11px] font-semibold uppercase tracking-[0.18em]" style={{ color: theme.textSecondary }}>{s.eyebrow}</div>
          <h2 className="mt-4 font-serif-aesthetic text-[40px] leading-[1.05] sm:text-6xl" style={{ color: theme.textPrimary }}>
            {s.titleLine1}
            <br />
            {s.titleLine2}
          </h2>
          <p className="mt-6 max-w-xl text-base leading-relaxed" style={{ color: theme.textSecondary }}>{s.text}</p>

          <ul className="mt-8 border-t" style={{ borderColor: theme.borderLight }}>
            {s.categories.map(({ name, description }) => (
              <li key={name} className="flex flex-wrap items-center justify-between gap-x-4 gap-y-1 border-b py-4 text-sm" style={{ borderColor: theme.borderLight }}>
                <span className="font-medium" style={{ color: theme.textPrimary }}>{name}</span>
                <span className="text-xs" style={{ color: theme.textSecondary }}>{description}</span>
              </li>
            ))}
          </ul>

          <div className="mt-8 flex flex-col items-start gap-4 sm:flex-row sm:items-center">
            <Link
              href="/register"
              className="inline-flex items-center gap-2 rounded-full px-6 py-3.5 text-sm font-medium text-white shadow-[0_8px_20px_-10px_rgba(95,107,87,0.9)] transition hover:brightness-95 active:scale-[0.98]"
              style={{ backgroundColor: theme.accentGreen }}
            >
              {s.cta}
              <ArrowUpRight className="h-4 w-4" />
            </Link>
            <span className="text-xs" style={{ color: theme.textSecondary }}>{s.note}</span>
          </div>
        </div>

      </div>
    </section>
  );
}

// Штатив с пробирками и колба — тонкие линии, без «мультяшности»
function LabIllustration() {
  const tubes = [
    { x: 120, fill: '#c9a98c', level: 0.55 },
    { x: 165, fill: '#d8c4a4', level: 0.7 },
    { x: 210, fill: '#b07f6a', level: 0.45 },
    { x: 255, fill: '#d9cdb3', level: 0.62 },
  ];
  return (
    <svg viewBox="0 0 400 500" className="absolute inset-0 h-full w-full" preserveAspectRatio="xMidYMid slice" aria-hidden>
      <circle cx="290" cy="150" r="120" fill="#ffffff" opacity="0.35" />
      {tubes.map(({ x, fill, level }) => {
        const top = 170;
        const height = 160;
        const liquidTop = top + height * (1 - level);
        return (
          <g key={x}>
            <path d={`M${x} ${top} V${top + height - 14} a14 14 0 0 0 28 0 V${top}`} fill="#ffffff" fillOpacity="0.45" stroke="#7c8c76" strokeWidth="1.5" />
            <path d={`M${x + 2} ${liquidTop} V${top + height - 14} a12 12 0 0 0 24 0 V${liquidTop} Z`} fill={fill} opacity="0.85" />
            <rect x={x - 3} y={top - 10} width="34" height="10" rx="3" fill="#6f7f66" opacity="0.8" />
          </g>
        );
      })}
      <rect x="95" y="250" width="210" height="12" rx="6" fill="#8b9a84" opacity="0.8" />
      <rect x="105" y="262" width="8" height="110" rx="4" fill="#8b9a84" opacity="0.6" />
      <rect x="287" y="262" width="8" height="110" rx="4" fill="#8b9a84" opacity="0.6" />
      <path d="M300 300 h30 v40 l34 66 a14 14 0 0 1 -12 20 h-74 a14 14 0 0 1 -12 -20 l34 -66 z" fill="#ffffff" fillOpacity="0.5" stroke="#7c8c76" strokeWidth="1.5" />
      <path d="M287 380 h56 l14 26 a8 8 0 0 1 -7 12 h-70 a8 8 0 0 1 -7 -12 z" fill="#9fb39a" opacity="0.7" />
    </svg>
  );
}
