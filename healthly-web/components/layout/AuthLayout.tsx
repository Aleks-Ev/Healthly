'use client';

// Каркас страниц входа и регистрации (макеты Figma «Healthily registration», десктоп и телефон):
// шапка (логотип, «Need a little help?», язык), бейдж, заголовок, белая карточка формы,
// блок под карточкой («Already have an account? Sign In») и подвал. Фон — тёплый крем с тихими декорациями.
// На телефоне карточки нет: поля лежат прямо на фоне, заголовок выровнен влево, логотип — по центру.

import type { ReactNode } from 'react';
import Link from 'next/link';
import { ArrowUpRight, ShieldCheck, Sparkles } from 'lucide-react';
import Logo from './Logo';
import LanguageSwitcher from '@/components/ui/LanguageSwitcher';
import { useI18n } from '@/lib/i18n';

// Константа, а не new Date(): страница пререндерится (cacheComponents)
const COPYRIGHT_YEAR = 2026;

interface AuthLayoutProps {
  title: string;
  subtitle: string;
  wide?: boolean; // широкая карточка для длинных форм (регистрация)
  children: ReactNode;
  // Под карточкой: ссылка на вход / регистрацию
  below?: ReactNode;
}

export default function AuthLayout({ title, subtitle, wide = false, children, below }: AuthLayoutProps) {
  const { t } = useI18n();

  return (
    <div className="relative flex min-h-screen flex-col overflow-x-clip bg-[#f7f7f2] text-[#2a302b]">
      <Decorations />

      <header className="relative z-10 mx-auto flex w-full max-w-7xl items-center justify-between gap-4 px-4 pt-5 sm:px-8 sm:pt-8 lg:px-14">
        {/* На телефоне логотип по центру над заголовком (см. ниже), здесь — только переключатель языка */}
        <div className="hidden sm:block">
          <Logo size="lg" />
        </div>
        <div className="ml-auto flex items-center gap-4">
          <span className="hidden text-sm text-[#6c736c] md:inline">
            {t.auth.help}{' '}
            <Link href="/#faq" className="inline-flex items-center gap-1 font-medium text-[#2f3530] transition hover:text-[#5f6b57]">
              {t.common.contactUs}
              <ArrowUpRight className="h-3.5 w-3.5" />
            </Link>
          </span>
          <LanguageSwitcher />
        </div>
      </header>

      <main className="relative z-10 flex flex-1 flex-col items-center px-4 pb-12 pt-6 sm:px-6 sm:pt-12">
        <div className="mb-8 sm:hidden">
          <Logo size="lg" />
        </div>

        <div className={`w-full ${wide ? 'max-w-[520px]' : 'max-w-[480px]'}`}>
          <div className="sm:text-center">
            <span className="hidden items-center gap-2 rounded-full bg-[#eaeee5] px-3 py-1 text-xs font-medium text-[#56634f] sm:inline-flex">
              <Sparkles className="h-3.5 w-3.5" />
              {t.auth.badge}
            </span>
            <h1 className="mt-0 text-[32px] font-medium leading-tight tracking-[-0.02em] text-[#2a302b] sm:mt-5 sm:text-[42px]">{title}</h1>
            <p className="mx-auto mt-3 max-w-md text-[15px] leading-relaxed text-[#6c736c] sm:text-base">{subtitle}</p>
          </div>

          <div className="mt-8 sm:rounded-[28px] sm:border sm:border-[#ebede6] sm:bg-white sm:p-8 sm:shadow-[0_1px_2px_rgba(42,48,43,0.04),0_24px_48px_-28px_rgba(42,48,43,0.22)]">
            {children}
          </div>

          {below && <div className="mt-6 flex flex-wrap items-center justify-center gap-3 text-sm text-[#6c736c] sm:mt-8">{below}</div>}
        </div>
      </main>

      <footer className="relative z-10 mx-auto flex w-full max-w-7xl flex-col items-center justify-between gap-3 px-4 pb-6 text-xs text-[#6c736c] sm:flex-row sm:px-8 lg:px-14">
        <span className="order-2 sm:order-1">{t.common.copyright(COPYRIGHT_YEAR)}</span>
        <span className="order-1 flex items-center gap-1.5 sm:order-2">
          <ShieldCheck className="h-3.5 w-3.5" />
          {t.common.privacyTagline}
        </span>
        <span className="order-3 flex items-center gap-5">
          <Link href="/#faq-privacy" className="transition hover:text-[#2a302b]">{t.common.privacy}</Link>
          <Link href="/#faq-diagnosis" className="transition hover:text-[#2a302b]">{t.common.terms}</Link>
        </span>
      </footer>
    </div>
  );
}

// Белая «таблетка» под карточкой: «Sign In» / «Create Account»
export function AuthSwitchLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <Link
      href={href}
      className="rounded-xl bg-white px-5 py-2.5 font-medium text-[#2f3530] shadow-[0_1px_2px_rgba(42,48,43,0.06),0_6px_16px_-10px_rgba(42,48,43,0.18)] transition hover:bg-[#fbfbf8] active:scale-[0.98]"
    >
      {children}
    </Link>
  );
}

// Фон: размытые пятна, линия пульса слева и тонкие окружности справа (как в макете), только от планшета
function Decorations() {
  return (
    <div className="pointer-events-none absolute inset-0" aria-hidden>
      <div className="absolute -left-40 top-40 h-[520px] w-[520px] rounded-full bg-[#eceee6] blur-3xl" />
      <div className="absolute -right-32 top-24 h-[440px] w-[440px] rounded-full bg-[#eef0ea] blur-3xl" />
      <svg viewBox="0 0 130 50" className="absolute left-[12%] top-[58%] hidden w-32 lg:block">
        <path d="M0 25 H38 l8 -18 l10 34 l8 -16 H130" fill="none" stroke="#dcdfd6" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
      <div className="absolute -right-20 top-[52%] hidden h-72 w-72 rounded-full border border-[#e4e6df] lg:block" />
      <div className="absolute -right-2 top-[56%] hidden h-52 w-52 rounded-full border border-[#e4e6df] lg:block" />
    </div>
  );
}
