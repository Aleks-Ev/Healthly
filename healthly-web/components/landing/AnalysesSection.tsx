'use client';

// Секция #analyses («Lab tests»): какие группы анализов читает Healthly.
// Список групп и показателей — в словаре (locales/*.ts), названия совпадают с reference-values.csv.
// У каждой группы id `analyses-<id>` — на неё ведёт поиск в шапке.

import Link from 'next/link';
import { ArrowUpRight, Info } from 'lucide-react';
import { useI18n } from '@/lib/i18n';
import type { SectionProps } from './theme';

export default function AnalysesSection({ theme }: SectionProps) {
  const { t } = useI18n();
  const a = t.analyses;

  return (
    <section id="analyses" className="py-16 sm:py-24" style={{ backgroundColor: theme.bgMint }}>
      <div className="mx-auto max-w-7xl px-4 sm:px-8 lg:px-12">
        <div className="flex flex-col justify-between gap-6 md:flex-row md:items-end">
          <div className="max-w-2xl space-y-4">
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em]" style={{ color: theme.textSecondary }}>{a.eyebrow}</div>
            <h2 className="font-serif-aesthetic text-[40px] leading-[1.05] sm:text-6xl" style={{ color: theme.textPrimary }}>{a.title}</h2>
          </div>
          <p className="max-w-sm text-sm leading-relaxed" style={{ color: theme.textSecondary }}>{a.text}</p>
        </div>

        <div className="mt-12 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 lg:gap-5">
          {a.groups.map((group, index) => (
            <article
              key={group.id}
              id={`analyses-${group.id}`}
              className="group flex flex-col rounded-[24px] p-5 shadow-[0_1px_2px_rgba(42,48,43,0.04)] transition duration-300 target:ring-2 target:ring-[#737f69]/40 hover:shadow-[0_14px_30px_-20px_rgba(42,48,43,0.3)] sm:p-6"
              style={{ backgroundColor: theme.bgPanel }}
            >
              <div className="flex items-baseline justify-between gap-3">
                <span className="font-serif-aesthetic text-lg" style={{ color: theme.textMuted }}>{String(index + 1).padStart(2, '0')}</span>
                <span className="text-[10px] font-medium uppercase tracking-[0.12em]" style={{ color: theme.textSecondary }}>{a.biomarkers(group.tests.length)}</span>
              </div>
              <h3 className="mt-3 font-serif-aesthetic text-[26px] leading-tight" style={{ color: theme.textPrimary }}>{group.name}</h3>
              <p className="mt-2 text-[13px] leading-relaxed" style={{ color: theme.textSecondary }}>{group.text}</p>
              <ul className="mt-5 flex flex-wrap gap-1.5">
                {group.tests.map((test) => (
                  <li key={test} className="rounded-full bg-white/85 px-2.5 py-1 text-[11px]" style={{ color: theme.textPrimary }}>{test}</li>
                ))}
              </ul>
            </article>
          ))}
        </div>

        <div className="mt-10 flex flex-col items-start justify-between gap-4 sm:flex-row sm:items-center">
          <p className="flex items-start gap-2 text-xs" style={{ color: theme.textSecondary }}>
            <Info className="mt-0.5 h-3.5 w-3.5 shrink-0" />
            {a.note}
          </p>
          <Link
            href="/register"
            className="inline-flex items-center gap-2 rounded-full bg-white px-5 py-3 text-sm font-medium shadow-[0_1px_2px_rgba(42,48,43,0.06),0_6px_16px_-8px_rgba(42,48,43,0.15)] transition hover:bg-[#fbfbf8] active:scale-[0.98]"
            style={{ color: theme.textPrimary }}
          >
            {a.cta}
            <ArrowUpRight className="h-4 w-4" />
          </Link>
        </div>
      </div>
    </section>
  );
}
