'use client';

// Финальный призыв «More understanding. More you.» и подвал сайта (дисклеймер, ссылки, копирайт)

import Link from 'next/link';
import { ArrowRight, ArrowUpRight, ShieldCheck, Sprout } from 'lucide-react';
import Logo from '@/components/layout/Logo';
import { useI18n } from '@/lib/i18n';
import type { SectionProps } from './theme';

// Год в копирайте — константа: new Date() при пререндере с cacheComponents делает страницу динамической
const COPYRIGHT_YEAR = 2026;

export default function ClosingSection({ theme }: SectionProps) {
  const { t } = useI18n();
  const c = t.closing;

  const footerLinks = [
    { href: '#how-it-works', label: t.nav.howItWorks },
    { href: '#analyses', label: t.nav.labTests },
    { href: '#faq', label: t.nav.faq },
    { href: '#philosophy', label: t.nav.about },
  ];

  return (
    <section className="mx-auto max-w-7xl px-4 pb-10 pt-16 sm:px-8 sm:pt-24 lg:px-12">

      <div className="relative overflow-hidden rounded-[32px] px-6 py-14 text-center sm:rounded-[44px] sm:px-16 sm:py-20" style={{ backgroundColor: theme.bgMint }}>
        <div className="pointer-events-none absolute -left-24 -top-24 h-80 w-80 rounded-full border border-white/60" aria-hidden />
        <div className="pointer-events-none absolute -bottom-20 -right-10 h-64 w-64 rounded-full bg-[#cfdccb]/60 blur-3xl" aria-hidden />

        <div className="relative space-y-6">
          <Sprout className="mx-auto h-7 w-7" style={{ color: theme.accentGreen }} strokeWidth={1.25} />
          <div className="text-[11px] font-semibold uppercase tracking-[0.18em]" style={{ color: theme.textSecondary }}>{c.eyebrow}</div>
          <h2 className="font-serif-aesthetic text-[40px] leading-[1.05] sm:text-6xl" style={{ color: theme.textPrimary }}>{c.title}</h2>
          <p className="mx-auto max-w-lg text-base leading-relaxed" style={{ color: theme.textSecondary }}>{c.text}</p>
          <div className="pt-2">
            <Link
              href="/register"
              className="flex w-full items-center justify-between rounded-full px-6 py-4 text-[15px] font-medium text-white shadow-[0_10px_24px_-12px_rgba(70,90,74,0.8)] transition active:scale-[0.98] sm:inline-flex sm:w-auto sm:gap-2 sm:py-3.5 sm:text-sm"
              style={{ backgroundColor: theme.accentGreen }}
            >
              {c.cta}
              <ArrowRight className="h-4 w-4 sm:hidden" />
              <ArrowUpRight className="hidden h-4 w-4 sm:block" />
            </Link>
          </div>
        </div>
      </div>

      <footer className="mt-16 space-y-10 border-t pt-10" style={{ borderColor: theme.borderLight }}>
        <div className="flex flex-col items-start justify-between gap-6 md:flex-row md:items-center">
          <Logo />
          <nav className="flex flex-wrap items-center gap-x-7 gap-y-3 text-xs" style={{ color: theme.textSecondary }} aria-label="Footer">
            {footerLinks.map(({ href, label }) => (
              <a key={href} href={href} className="transition hover:text-[#2a302b]">{label}</a>
            ))}
          </nav>
        </div>

        <p className="max-w-3xl text-[11px] leading-relaxed" style={{ color: theme.textSecondary }}>{c.disclaimer}</p>

        <div className="flex flex-col items-start justify-between gap-4 border-t pt-6 text-[11px] sm:flex-row sm:items-center" style={{ borderColor: theme.borderLight, color: theme.textSecondary }}>
          <div>{t.common.copyright(COPYRIGHT_YEAR)}</div>
          <div className="flex items-center gap-1.5">
            <ShieldCheck className="h-3.5 w-3.5" style={{ color: theme.accentGreen }} />
            {t.common.privacyTagline}
          </div>
          <div className="flex items-center gap-6">
            <a href="#faq-privacy" className="transition hover:text-[#2a302b]">{t.common.privacy}</a>
            <a href="#faq-diagnosis" className="transition hover:text-[#2a302b]">{t.common.terms}</a>
          </div>
        </div>
      </footer>

    </section>
  );
}
