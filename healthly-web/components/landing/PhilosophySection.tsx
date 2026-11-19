'use client';

// Секция «The Healthly philosophy»: главный тезис и три принципа (иконки в светлых кругах, как в Figma)

import { Leaf, ShieldCheck, Sparkles, Sprout, type LucideIcon } from 'lucide-react';
import { useI18n } from '@/lib/i18n';
import type { SectionProps } from './theme';

const ICONS: LucideIcon[] = [Sparkles, ShieldCheck, Leaf];

export default function PhilosophySection({ theme }: SectionProps) {
  const { t } = useI18n();
  const p = t.philosophy;

  return (
    <section id="philosophy" className="relative overflow-hidden py-16 sm:py-24" style={{ backgroundColor: theme.bgSecondary }}>
      {/* Тонкие окружности на фоне — декоративные */}
      <div className="pointer-events-none absolute -right-24 -top-24 h-96 w-96 rounded-full border" style={{ borderColor: theme.borderLight }} aria-hidden />
      <div className="pointer-events-none absolute -right-8 -top-8 h-64 w-64 rounded-full border" style={{ borderColor: theme.borderLight }} aria-hidden />

      <div className="relative mx-auto max-w-7xl px-4 sm:px-8 lg:px-12">
        <div className="grid grid-cols-1 gap-8 md:grid-cols-12">
          <div className="space-y-5 md:col-span-3">
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em]" style={{ color: theme.textSecondary }}>{p.eyebrow}</div>
            <Sprout className="hidden h-7 w-7 md:block" style={{ color: theme.accentGreen }} strokeWidth={1.25} />
          </div>
          <div className="space-y-6 md:col-span-9">
            <h2 className="font-serif-aesthetic text-[40px] leading-[1.05] sm:text-6xl" style={{ color: theme.textPrimary }}>
              {p.titleLine1}
              <br className="hidden sm:block" /> {p.titleLine2}
            </h2>
            <p className="max-w-2xl text-base leading-relaxed" style={{ color: theme.textSecondary }}>{p.text}</p>
          </div>
        </div>

        <ul className="mt-12 grid grid-cols-1 gap-7 md:mt-16 md:grid-cols-3 md:gap-8">
          {p.pillars.map(({ title, text }, index) => {
            const Icon = ICONS[index];
            return (
              <li key={title} className="flex gap-4 md:block">
                <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-white/80 md:mb-4">
                  <Icon className="h-4 w-4" style={{ color: theme.accentGreen }} strokeWidth={1.75} />
                </span>
                <div>
                  <div className="text-sm font-semibold" style={{ color: theme.textPrimary }}>{title}</div>
                  <p className="mt-1.5 text-sm leading-relaxed" style={{ color: theme.textSecondary }}>{text}</p>
                </div>
              </li>
            );
          })}
        </ul>
      </div>
    </section>
  );
}
