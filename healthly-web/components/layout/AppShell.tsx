'use client';

// Общий каркас страниц личного кабинета: шапка с навигацией по ролям + контейнер содержимого.

import { Suspense, useState, type ReactNode } from 'react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { BookOpenCheck, Building2, FlaskConical, HeartHandshake, LayoutDashboard, LogOut, Menu, Users, X } from 'lucide-react';
import { useAuth } from '@/lib/auth';
import { RoleBadge } from '@/components/ui/Badge';
import LanguageSwitcher from '@/components/ui/LanguageSwitcher';
import { useI18n } from '@/lib/i18n';
import type { Dictionary } from '@/locales/en';
import Logo from './Logo';
import NotificationBell from './NotificationBell';
import RequireAuth from './RequireAuth';
import type { Role } from '@/types/api';

interface NavItem {
  href: string;
  label: keyof Dictionary['shell'];
  icon: typeof Users;
  roles?: Role[];
}

const NAV_ITEMS: NavItem[] = [
  { href: '/dashboard', label: 'myHealth', icon: LayoutDashboard, roles: ['Patient', 'Doctor', 'Admin'] },
  // «Команда здоровья»: пациент выбирает врача/лабораторию, врач отвечает на запросы, админ видит все связи
  { href: '/care', label: 'careTeam', icon: HeartHandshake, roles: ['Patient'] },
  { href: '/care', label: 'patientRequests', icon: HeartHandshake, roles: ['Doctor'] },
  { href: '/lab', label: 'laboratory', icon: FlaskConical, roles: ['LabStaff'] },
  // Врач видит в списке только своих пациентов (API фильтрует по активным связям)
  { href: '/admin/users', label: 'myPatients', icon: Users, roles: ['Doctor'] },
  { href: '/admin/users', label: 'users', icon: Users, roles: ['Admin'] },
  { href: '/care', label: 'careConnections', icon: HeartHandshake, roles: ['Admin'] },
  { href: '/admin/reference-values', label: 'referenceValues', icon: BookOpenCheck, roles: ['Doctor', 'Admin'] },
  { href: '/admin/laboratories', label: 'laboratories', icon: Building2, roles: ['Admin'] },
];

interface AppShellProps {
  // Роли, которым доступна страница (по умолчанию — любой вошедший пользователь)
  roles?: Role[];
  children: ReactNode;
}

export default function AppShell({ roles, children }: AppShellProps) {
  const { user, logout } = useAuth();
  const { t } = useI18n();
  const router = useRouter();
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  // После выхода — на страницу входа (туда же отправляет RequireAuth, поэтому редиректы не конфликтуют)
  const handleLogout = () => {
    logout();
    router.replace('/login');
  };

  // usePathname() на динамических маршрутах известен только во время запроса,
  // поэтому при cacheComponents ссылки должны быть внутри Suspense
  const navLinks = (
    <Suspense fallback={null}>
      <NavLinks role={user?.role} onNavigate={() => setIsMenuOpen(false)} />
    </Suspense>
  );

  return (
    <div className="min-h-screen bg-[#f7f7f2] text-[#2a302b]">
      <header className="sticky top-0 z-40 border-b border-[#e3e6dd] bg-[#f7f7f2]/85 backdrop-blur-md">
        <div className="mx-auto max-w-6xl h-16 px-4 sm:px-6 flex items-center justify-between gap-4">
          <div className="flex items-center gap-6">
            <Logo />
            {user && <nav className="hidden lg:flex items-center gap-1">{navLinks}</nav>}
          </div>

          {user && (
            <div className="flex items-center gap-2">
              <NotificationBell />
              <LanguageSwitcher className="hidden sm:block" />
              <div className="hidden xl:flex items-center gap-2 text-sm">
                <span className="whitespace-nowrap font-medium">{user.fullName}</span>
                <RoleBadge role={user.role} />
              </div>
              <button
                onClick={handleLogout}
                aria-label={t.common.logOut}
                title={t.common.logOut}
                className="hidden lg:inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-3 py-2 text-sm text-[#6b756e] hover:bg-[#ebefe6] hover:text-[#242b26] transition"
              >
                <LogOut className="w-4 h-4" />
                <span className="hidden xl:inline">{t.common.logOut}</span>
              </button>
              <button
                aria-label={isMenuOpen ? t.common.closeMenu : t.common.openMenu}
                aria-expanded={isMenuOpen}
                onClick={() => setIsMenuOpen((open) => !open)}
                className="lg:hidden p-2 rounded-full text-[#6b756e] hover:bg-[#ebefe6] transition"
              >
                {isMenuOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
              </button>
            </div>
          )}
        </div>

        {user && isMenuOpen && (
          <div className="lg:hidden border-t border-[#e3e6dd] px-4 py-3 space-y-1">
            <div className="flex items-center justify-between gap-3 px-1 pb-2 text-sm">
              <span className="truncate font-medium">{user.fullName}</span>
              <RoleBadge role={user.role} />
            </div>
            {navLinks}
            <button
              onClick={handleLogout}
              className="flex w-full items-center gap-2 rounded-full px-3.5 py-2 text-sm text-[#6b756e] hover:bg-[#ebefe6] transition"
            >
              <LogOut className="w-4 h-4" />
              {t.common.logOut}
            </button>
            <LanguageSwitcher className="px-1 pt-2 sm:hidden" />
          </div>
        )}
      </header>

      <main className="mx-auto max-w-6xl px-4 sm:px-6 py-8">
        <RequireAuth roles={roles}>{children}</RequireAuth>
      </main>
    </div>
  );
}

// Ссылки навигации с подсветкой активного раздела
function NavLinks({ role, onNavigate }: { role?: Role; onNavigate: () => void }) {
  const pathname = usePathname();
  const { t } = useI18n();
  const visibleItems = NAV_ITEMS.filter((item) => !item.roles || (role && item.roles.includes(role)));

  return visibleItems.map(({ href, label, icon: Icon }) => {
    const isActive = pathname === href || pathname.startsWith(`${href}/`);
    return (
      <Link
        key={`${href}-${label}`}
        href={href}
        onClick={onNavigate}
        className={`flex items-center gap-2 whitespace-nowrap rounded-full px-3 py-2 text-sm transition ${isActive ? 'bg-[#dce8d8] text-[#3a5235] font-medium' : 'text-[#6b756e] hover:bg-[#ebefe6] hover:text-[#242b26]'}`}
      >
        <Icon className="w-4 h-4" />
        {t.shell[label]}
      </Link>
    );
  });
}
