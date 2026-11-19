'use client';

// Секция «Your health, with the dots connected» (мятный фон во всю ширину, как в Figma):
// три карточки возможностей с маленькими макетами интерфейса сверху.

import type { ReactNode } from 'react';
import { ArrowUpRight, Check, FileText, FolderClosed, Sparkles, TrendingUp } from 'lucide-react';
import { useI18n } from '@/lib/i18n';
import { CHART_COLORS, LevelBar, smoothPath } from '@/components/charts/primitives';
import type { LandingTheme, SectionProps } from './theme';

// Иллюстративная кривая «баланса» (июнь–октябрь)
const BALANCE = [0.55, 0.3, 0.6, 0.38, 0.5, 0.28, 0.42, 0.2, 0.32, 0.15];

export default function FeaturesSection({ theme }: SectionProps) {
  const { t, intlLocale } = useI18n();
  const f = t.features;

  const months = [5, 6, 7, 8, 9].map((month) =>
    new Date(Date.UTC(2026, month, 1)).toLocaleDateString(intlLocale, { month: 'short', timeZone: 'UTC' }).toUpperCase(),
  );
  const balancePath = smoothPath(BALANCE.map((y, index) => ({ x: (index / (BALANCE.length - 1)) * 200, y: 8 + y * 34 })));

  return (
    <section id="features" className="py-16 sm:py-24" style={{ backgroundColor: theme.bgMint }}>
      <div className="mx-auto max-w-7xl space-y-12 px-4 sm:space-y-14 sm:px-8 lg:px-12">

        <div className="flex flex-col justify-between gap-6 md:flex-row md:items-end">
          <div className="max-w-2xl space-y-4">
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em]" style={{ color: theme.textSecondary }}>{f.eyebrow}</div>
            <h2 className="font-serif-aesthetic text-[40px] leading-[1.05] sm:text-6xl" style={{ color: theme.textPrimary }}>{f.title}</h2>
          </div>
          <p className="max-w-xs text-sm leading-relaxed" style={{ color: theme.textSecondary }}>{f.aside}</p>
        </div>

        <div className="grid grid-cols-1 gap-5 md:grid-cols-3 lg:gap-6">
          <FeatureCard theme={theme} title={f.library.title} text={f.library.text} linkText={f.library.link} href="/register">
            <PreviewHeader label={f.library.label} icon={<FolderClosed className="h-3.5 w-3.5" />} />
            <div className="mt-3 space-y-2">
              <LibraryItem title={f.library.item1} subtitle={f.library.item1Meta} />
              <LibraryItem title={f.library.item2} subtitle={f.library.item2Meta} />
            </div>
          </FeatureCard>

          <FeatureCard theme={theme} title={f.results.title} text={f.results.text} linkText={f.results.link} href="#analyses">
            <PreviewHeader label={f.results.label} icon={<Sparkles className="h-3.5 w-3.5" />} />
            <div className="mt-3 flex items-baseline justify-between gap-2">
              <div className="font-serif-aesthetic text-3xl leading-none" style={{ color: theme.textPrimary }}>
                42 <span className="font-sans-aesthetic text-[11px]" style={{ color: theme.textMuted }}>ng/mL</span>
              </div>
              <span className="rounded-full px-2.5 py-1 text-[10px] font-medium" style={{ backgroundColor: theme.accentMint, color: theme.accentMintText }}>{f.results.badge}</span>
            </div>
            <LevelBar value={0.62} color={theme.accentGreen} className="mt-4" />
            <p className="mt-3 text-[11px] leading-snug" style={{ color: theme.textSecondary }}>{f.results.caption}</p>
          </FeatureCard>

          <FeatureCard theme={theme} title={f.trends.title} text={f.trends.text} linkText={f.trends.link} href="#insights">
            <PreviewHeader label={f.trends.label} icon={<TrendingUp className="h-3.5 w-3.5" />} />
            <div className="mt-2 font-serif-aesthetic text-xl" style={{ color: theme.textPrimary }}>{f.trends.heading}</div>
            <svg viewBox="0 0 200 50" className="mt-2 h-12 w-full overflow-visible" preserveAspectRatio="none" aria-hidden>
              <path d={balancePath} fill="none" stroke={CHART_COLORS.line} strokeWidth="1.6" strokeLinecap="round" vectorEffect="non-scaling-stroke" />
            </svg>
            <div className="mt-2 flex justify-between text-[9px] tracking-wider" style={{ color: theme.textMuted }}>
              {months.map((month) => <span key={month}>{month}</span>)}
            </div>
          </FeatureCard>
        </div>

        <div className="text-right text-[11px]" style={{ color: theme.textSecondary }}>{f.note}</div>
      </div>
    </section>
  );
}

interface FeatureCardProps {
  theme: LandingTheme;
  title: string;
  text: string;
  linkText: string;
  href: string;
  children: ReactNode;
}

function FeatureCard({ theme, title, text, linkText, href, children }: FeatureCardProps) {
  return (
    <article
      className="flex flex-col justify-between gap-8 rounded-[28px] p-4 shadow-[0_1px_2px_rgba(42,48,43,0.04),0_12px_30px_-18px_rgba(42,48,43,0.2)] transition duration-300 hover:-translate-y-0.5 sm:p-5"
      style={{ backgroundColor: theme.bgPanel }}
    >
      {/* Макет интерфейса */}
      <div className="rounded-[20px] border border-white/70 bg-white/75 p-4 sm:p-5">{children}</div>

      <div className="space-y-3 px-2 pb-2">
        <h3 className="font-serif-aesthetic text-[28px] leading-tight" style={{ color: theme.textPrimary }}>{title}</h3>
        <p className="text-sm leading-relaxed" style={{ color: theme.textSecondary }}>{text}</p>
        <a href={href} className="inline-flex items-center gap-1.5 pt-3 text-xs font-medium transition hover:opacity-70" style={{ color: theme.textPrimary }}>
          {linkText}
          <ArrowUpRight className="h-3.5 w-3.5" />
        </a>
      </div>
    </article>
  );
}

function PreviewHeader({ label, icon }: { label: string; icon: ReactNode }) {
  return (
    <div className="flex items-center justify-between text-[11px] text-[#8a918a]">
      <span>{label}</span>
      {icon}
    </div>
  );
}

function LibraryItem({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <div className="flex items-center gap-3 rounded-xl bg-[#f6f7f3] px-3 py-2.5 text-xs">
      <FileText className="h-4 w-4 shrink-0 text-[#8a918a]" />
      <div className="min-w-0 flex-1">
        <div className="truncate font-medium text-[#2a302b]">{title}</div>
        <div className="truncate text-[10px] text-[#9aa19a]">{subtitle}</div>
      </div>
      <Check className="h-3.5 w-3.5 shrink-0 text-[#737f69]" />
    </div>
  );
}
