'use client';

// Шапка лендинга (как в Figma): светлая, логотип слева, ссылки и «Sign In» справа.
// Лупа открывает поиск по странице (SearchDialog; состояние и горячие клавиши — в app/page.tsx).

import { useState } from 'react';
import Link from 'next/link';
import { Menu, Search, X } from 'lucide-react';
import Logo from '@/components/layout/Logo';
import LanguageSwitcher from '@/components/ui/LanguageSwitcher';
import { useAuth } from '@/lib/auth';
import { useI18n } from '@/lib/i18n';

export default function Navbar({ onOpenSearch }: { onOpenSearch: () => void }) {
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const { user, logout } = useAuth();
  const { t } = useI18n();

  const links = [
    { label: t.nav.howItWorks, href: '#how-it-works' },
    { label: t.nav.labTests, href: '#analyses' },
    { label: t.nav.testing, href: '#testing' },
    { label: t.nav.faq, href: '#faq' },
    { label: t.nav.about, href: '#philosophy' },
  ];

  const closeMobileMenu = () => setIsMobileMenuOpen(false);
  const openSearch = () => {
    closeMobileMenu();
    onOpenSearch();
  };

  const pillClass =
    'items-center justify-center rounded-full bg-white px-5 py-2 text-sm font-medium text-[#2a302b] shadow-[0_1px_2px_rgba(42,48,43,0.06),0_4px_14px_-6px_rgba(42,48,43,0.12)] transition hover:bg-[#fbfbf8] active:scale-[0.98]';

  return (
    <header className="sticky top-0 z-40 border-b border-transparent bg-[#f7f7f2]/80 font-sans-aesthetic backdrop-blur-md supports-[backdrop-filter]:bg-[#f7f7f2]/70">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-4 px-4 sm:h-[72px] sm:px-8 lg:px-12">
          <Logo />

          <nav className="hidden items-center gap-7 text-[13px] text-[#4a524c] lg:flex" aria-label="Main">
            {links.map((link) => (
              <a key={link.href} href={link.href} className="transition hover:text-[#2a302b]">
                {link.label}
              </a>
            ))}
          </nav>

          <div className="flex items-center gap-1.5 sm:gap-2.5">
            <button
              type="button"
              onClick={openSearch}
              aria-label={t.common.search}
              title={`${t.common.search} ( / )`}
              className="rounded-full p-2 text-[#4a524c] transition hover:bg-white hover:text-[#2a302b]"
            >
              <Search className="h-[18px] w-[18px]" />
            </button>

            <LanguageSwitcher className="hidden sm:block" />

            {user ? (
              <>
                <button type="button" onClick={logout} className="hidden px-2 text-sm text-[#4a524c] transition hover:text-[#2a302b] md:inline-flex">
                  {t.common.logOut}
                </button>
                <Link href="/dashboard" className={`hidden sm:inline-flex ${pillClass}`}>
                  {t.common.myDashboard}
                </Link>
              </>
            ) : (
              <Link href="/login" className={`hidden sm:inline-flex ${pillClass}`}>
                {t.common.signIn}
              </Link>
            )}

            {/* Круглая кнопка меню, как в мобильном макете */}
            <button
              type="button"
              aria-label={isMobileMenuOpen ? t.common.closeMenu : t.common.openMenu}
              aria-expanded={isMobileMenuOpen}
              onClick={() => setIsMobileMenuOpen((open) => !open)}
              className="flex h-10 w-10 items-center justify-center rounded-full border border-[#dfe3d9] text-[#4a524c] transition hover:bg-white lg:hidden"
            >
              {isMobileMenuOpen ? <X className="h-[18px] w-[18px]" /> : <Menu className="h-[18px] w-[18px]" />}
            </button>
          </div>
        </div>

        {isMobileMenuOpen && (
          <div className="border-t border-[#e3e6dd] bg-[#f7f7f2] px-4 pb-5 pt-3 sm:px-8 lg:hidden">
            <nav className="space-y-0.5" aria-label="Main">
              {links.map((link) => (
                <a
                  key={link.href}
                  href={link.href}
                  onClick={closeMobileMenu}
                  className="block rounded-xl px-3 py-2.5 text-[15px] text-[#2a302b] transition hover:bg-white"
                >
                  {link.label}
                </a>
              ))}
            </nav>
            <div className="mt-4 flex items-center justify-between gap-3 border-t border-[#e3e6dd] pt-4">
              <LanguageSwitcher />
              {user ? (
                <div className="flex items-center gap-2">
                  <button type="button" onClick={() => { logout(); closeMobileMenu(); }} className="px-2 text-sm text-[#4a524c]">
                    {t.common.logOut}
                  </button>
                  <Link href="/dashboard" className={`inline-flex ${pillClass}`}>{t.common.myDashboard}</Link>
                </div>
              ) : (
                <Link href="/login" className={`inline-flex ${pillClass}`}>{t.common.signIn}</Link>
              )}
            </div>
          </div>
        )}
    </header>
  );
}
