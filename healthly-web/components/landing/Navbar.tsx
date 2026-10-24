'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Search, Menu, X } from 'lucide-react';
import Logo from '@/components/layout/Logo';
import { useAuth } from '@/lib/auth';

interface NavLink {
  label: string;
  href: string;
}

const NAV_LINKS: NavLink[] = [
  { label: 'How it works', href: '#how-it-works' },
  { label: 'What we test', href: '#testing' },
  { label: 'Lab tests', href: '#analyses' },
  { label: 'FAQ', href: '#faq' },
  { label: 'About us', href: '#philosophy' },
];

export default function Navbar() {
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const { user, logout } = useAuth();
  const router = useRouter();

  const closeMobileMenu = () => setIsMobileMenuOpen(false);

  // Кнопки справа зависят от того, вошёл ли пользователь
  const secondaryAction = user
    ? { label: 'Log out', onClick: () => { logout(); closeMobileMenu(); } }
    : { label: 'Log in', onClick: () => router.push('/login') };
  const primaryAction = user
    ? { label: 'My dashboard', href: '/dashboard' }
    : { label: 'Start testing', href: '/register' };

  return (
    <header className="sticky top-4 z-40 mx-auto w-[92%] max-w-7xl font-sans-aesthetic">
      <div className="rounded-2xl bg-slate-900/60 backdrop-blur-lg border border-white/10 shadow-2xl text-white">
        <div className="h-16 px-4 sm:px-6 flex items-center justify-between gap-4">
          <Logo tone="light" />

          {/* Desktop links */}
          <nav className="hidden lg:flex items-center gap-7 text-sm text-white/80">
            {NAV_LINKS.map((link) => (
              <a key={link.href} href={link.href} className="hover:text-emerald-400 transition">
                {link.label}
              </a>
            ))}
          </nav>

          {/* Actions */}
          <div className="flex items-center gap-1 sm:gap-2">
            <button
              aria-label="Search"
              className="p-2 rounded-full text-white/80 hover:text-emerald-400 hover:bg-white/5 transition"
            >
              <Search className="w-5 h-5" />
            </button>

            <button
              onClick={secondaryAction.onClick}
              className="hidden sm:inline-flex px-4 py-2 text-sm font-medium text-white/90 hover:text-emerald-400 transition"
            >
              {secondaryAction.label}
            </button>

            <Link
              href={primaryAction.href}
              className="hidden sm:inline-flex px-5 py-2 rounded-full bg-[#f5f1e8] text-slate-900 text-sm font-semibold shadow-sm hover:bg-white active:scale-95 transition"
            >
              {primaryAction.label}
            </Link>

            <button
              aria-label={isMobileMenuOpen ? 'Close menu' : 'Open menu'}
              aria-expanded={isMobileMenuOpen}
              onClick={() => setIsMobileMenuOpen((open) => !open)}
              className="lg:hidden p-2 rounded-full text-white/80 hover:text-emerald-400 hover:bg-white/5 transition"
            >
              {isMobileMenuOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
            </button>
          </div>
        </div>

        {/* Mobile menu */}
        {isMobileMenuOpen && (
          <div className="lg:hidden border-t border-white/10 px-4 sm:px-6 py-4 space-y-1">
            {NAV_LINKS.map((link) => (
              <a
                key={link.href}
                href={link.href}
                onClick={closeMobileMenu}
                className="block px-3 py-2 rounded-lg text-sm text-white/80 hover:text-emerald-400 hover:bg-white/5 transition"
              >
                {link.label}
              </a>
            ))}
            <div className="flex flex-col sm:hidden gap-2 pt-3">
              <button
                onClick={secondaryAction.onClick}
                className="w-full px-4 py-2.5 rounded-full border border-white/15 text-sm font-medium hover:text-emerald-400 transition"
              >
                {secondaryAction.label}
              </button>
              <Link
                href={primaryAction.href}
                className="w-full text-center px-4 py-2.5 rounded-full bg-[#f5f1e8] text-slate-900 text-sm font-semibold hover:bg-white transition"
              >
                {primaryAction.label}
              </Link>
            </div>
          </div>
        )}
      </div>
    </header>
  );
}
