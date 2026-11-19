'use client';

// Секция «How it works»: три шага. Номер тонкой антиквой, от него — линия к иконке (как в Figma)

import { FolderClosed, Sparkles, Sprout, type LucideIcon } from 'lucide-react';
import { useI18n } from '@/lib/i18n';
import type { SectionProps } from './theme';

const ICONS: LucideIcon[] = [FolderClosed, Sparkles, Sprout];

export default function HowItWorksSection({ theme }: SectionProps) {
  const { t } = useI18n();
  const h = t.howItWorks;

  return (
    <section id="how-it-works" className="mx-auto max-w-7xl px-4 py-16 sm:px-8 sm:py-24 lg:px-12">
      <div className="flex flex-col justify-between gap-6 md:flex-row md:items-end">
        <div className="max-w-2xl space-y-4">
          <div className="text-[11px] font-semibold uppercase tracking-[0.18em]" style={{ color: theme.textSecondary }}>{h.eyebrow}</div>
          <h2 className="font-serif-aesthetic text-[40px] leading-[1.05] sm:text-6xl" style={{ color: theme.textPrimary }}>{h.title}</h2>
        </div>
        <div className="font-serif-aesthetic text-xl italic" style={{ color: theme.textSecondary }}>{h.aside}</div>
      </div>

      <ol className="mt-12 grid grid-cols-1 gap-10 md:grid-cols-3 md:gap-8">
        {h.steps.map((step, index) => {
          const Icon = ICONS[index];
          return (
            <li key={step.title}>
              <div className="flex items-center gap-4">
                <span className="font-serif-aesthetic text-3xl" style={{ color: theme.textSecondary }}>{String(index + 1).padStart(2, '0')}</span>
                <span className="h-px flex-1" style={{ backgroundColor: theme.borderLight }} />
                <Icon className="h-4 w-4" style={{ color: theme.textSecondary }} strokeWidth={1.5} />
              </div>
              <h3 className="mt-6 font-serif-aesthetic text-2xl" style={{ color: theme.textPrimary }}>{step.title}</h3>
              <p className="mt-3 text-sm leading-relaxed" style={{ color: theme.textSecondary }}>{step.text}</p>
            </li>
          );
        })}
      </ol>
    </section>
  );
}
