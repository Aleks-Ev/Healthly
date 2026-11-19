'use client';

// Первый экран (Figma «Healthily home»): заголовок с засечками, две кнопки и «арка» со сценой рассвета.
// На телефоне — как в мобильном макете: всё по центру, главная кнопка во всю ширину, под ней веточки.

import Link from 'next/link';
import { ArrowRight, ArrowUpRight, ChevronDown, ShieldCheck, Sparkles } from 'lucide-react';
import { useI18n } from '@/lib/i18n';
import { BranchPair, OliveBranch, SunriseScene } from './Botanicals';
import type { SectionProps } from './theme';

export default function HeroSection({ theme }: SectionProps) {
  const { t } = useI18n();

  return (
    <section className="mx-auto max-w-7xl px-4 pb-16 pt-8 sm:px-8 sm:pt-12 lg:px-12 lg:pb-24">
      <div className="grid grid-cols-1 items-center gap-10 lg:grid-cols-12 lg:gap-10">

        <div className="text-center lg:col-span-6 lg:text-left">
          <div className="inline-flex items-center gap-2 text-[11px] font-semibold uppercase tracking-[0.18em]" style={{ color: theme.textSecondary }}>
            <span className="hidden h-1.5 w-1.5 rounded-full lg:inline-block" style={{ backgroundColor: theme.accentGreen }} />
            {t.hero.eyebrow}
          </div>

          <h1 className="mt-5 font-serif-aesthetic text-[52px] leading-[1.02] sm:text-7xl lg:text-[88px]" style={{ color: theme.textPrimary }}>
            {t.hero.titleLine1}
            <br />
            {t.hero.titleLine2}
          </h1>

          <p className="mx-auto mt-6 max-w-md text-base leading-relaxed sm:text-[17px] lg:mx-0" style={{ color: theme.textSecondary }}>
            {t.hero.text}
          </p>

          {/* Телефон: одна крупная тёмно-зелёная кнопка во всю ширину */}
          <div className="mt-8 sm:hidden">
            <Link
              href="/register"
              className="flex w-full items-center justify-between rounded-full px-6 py-4 text-[15px] font-medium text-white shadow-[0_10px_24px_-12px_rgba(70,90,74,0.8)] transition active:scale-[0.98]"
              style={{ backgroundColor: theme.bgDark }}
            >
              {t.hero.primaryCta}
              <ArrowRight className="h-4 w-4" />
            </Link>
          </div>

          <div className="mt-9 hidden flex-wrap items-center justify-center gap-3 sm:flex lg:justify-start">
            <Link
              href="/register"
              className="inline-flex items-center gap-2 rounded-full px-6 py-3.5 text-sm font-medium text-white shadow-[0_8px_20px_-10px_rgba(95,107,87,0.9)] transition hover:brightness-95 active:scale-[0.98]"
              style={{ backgroundColor: theme.accentGreen }}
            >
              {t.hero.primaryCta}
              <ArrowUpRight className="h-4 w-4" />
            </Link>
            <Link
              href="/dashboard"
              className="inline-flex items-center gap-2 rounded-full px-6 py-3.5 text-sm font-medium shadow-[0_1px_2px_rgba(42,48,43,0.06),0_6px_16px_-8px_rgba(42,48,43,0.15)] transition hover:bg-[#fbfbf8] active:scale-[0.98]"
              style={{ backgroundColor: theme.bgCard, color: theme.textPrimary }}
            >
              {t.hero.secondaryCta}
              <ArrowUpRight className="h-4 w-4" />
            </Link>
          </div>

          <div className="mt-5 flex items-center justify-center gap-2 text-xs lg:mt-8 lg:justify-start" style={{ color: theme.textSecondary }}>
            <ShieldCheck className="h-4 w-4" style={{ color: theme.accentGreen }} />
            <span className="sm:hidden">{t.hero.trust}</span>
            <span className="hidden sm:inline">{t.common.privacyTagline}</span>
          </div>

          <a href="#features" className="mt-14 hidden items-center gap-1.5 text-[11px] transition hover:opacity-70 lg:inline-flex" style={{ color: theme.textSecondary }}>
            {t.hero.explore}
            <ChevronDown className="h-3.5 w-3.5" />
          </a>
        </div>

        {/* Телефон: веточки вместо большой иллюстрации (как в мобильном макете) */}
        <BranchPair className="py-2 sm:hidden" />

        <div className="hidden sm:block lg:col-span-6">
          <div className="relative mx-auto aspect-[5/4] w-full max-w-xl overflow-hidden rounded-[40px] rounded-tl-[140px] rounded-br-[140px] shadow-[0_30px_60px_-30px_rgba(70,60,40,0.45)] lg:aspect-[11/12] lg:max-w-none">
            <SunriseScene className="absolute inset-0 h-full w-full" />
            <OliveBranch className="absolute -bottom-4 left-6 h-56 w-auto opacity-90" />

            {/* «Стеклянная» плашка поверх сцены */}
            <div className="absolute bottom-8 right-6 flex max-w-[78%] items-center gap-3 rounded-2xl border border-white/50 bg-white/45 px-4 py-3 shadow-[0_8px_30px_-12px_rgba(42,48,43,0.35)] backdrop-blur-md lg:bottom-14 lg:right-8">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-white/80">
                <Sparkles className="h-4 w-4" style={{ color: theme.accentGreen }} />
              </span>
              <div className="min-w-0 text-left">
                <div className="text-[9px] font-semibold uppercase tracking-[0.16em]" style={{ color: theme.textSecondary }}>{t.hero.badgeEyebrow}</div>
                <div className="font-serif-aesthetic text-lg leading-tight lg:text-xl" style={{ color: theme.textPrimary }}>{t.hero.badgeTitle}</div>
              </div>
            </div>
          </div>
        </div>

      </div>
    </section>
  );
}
