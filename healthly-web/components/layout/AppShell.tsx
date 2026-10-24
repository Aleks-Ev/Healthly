'use client';

// Общий каркас страниц личного кабинета: шапка с навигацией по ролям + контейнер содержимого.

import { Suspense, useState, type ReactNode } from 'react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { LayoutDashboard, LogOut, Menu, Users, X } from 'lucide-react';
import { useAuth } from '@/lib/auth';
import { RoleBadge } from '@/components/ui/Badge';
import Logo from './Logo';
import RequireAuth from './RequireAuth';
import type { Role } from '@/types/api';

interface NavItem {
  href: string;
  label: string;
  icon: typeof Users;
  roles?: Role[];
}

const NAV_ITEMS: NavItem[] = [
  { href: '/dashboard', label: 'My health', icon: LayoutDashboard },
  { href: '/admin/users', label: 'Users', icon: Users, roles: ['Doctor', 'Admin'] },
];

interface AppShellProps {
  // Роли, которым доступна страница (по умолчанию — любой вошедший пользователь)
  roles?: Role[];
  children: ReactNode;
}

export default function AppShell({ roles, children }: AppShellProps) {
  const { user, logout } = useAuth();
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
    <div className="min-h-screen bg-[#f6f7f2] text-[#242b26]">
      <header className="sticky top-0 z-40 border-b border-[#e1e7dc] bg-[#f6f7f2]/85 backdrop-blur-md">
        <div className="mx-auto max-w-6xl h-16 px-4 sm:px-6 flex items-center justify-between gap-4">
          <div className="flex items-center gap-6">
            <Logo />
            {user && <nav className="hidden md:flex items-center gap-1">{navLinks}</nav>}
          </div>

          {user && (
            <div className="flex items-center gap-2">
              <div className="hidden sm:flex items-center gap-2 text-sm">
                <span className="font-medium">{user.fullName}</span>
                <RoleBadge role={user.role} />
              </div>
              <button
                onClick={handleLogout}
                className="hidden md:inline-flex items-center gap-1.5 rounded-full px-3 py-2 text-sm text-[#6b756e] hover:bg-[#ebefe6] hover:text-[#242b26] transition"
              >
                <LogOut className="w-4 h-4" />
                Log out
              </button>
              <button
                aria-label={isMenuOpen ? 'Close menu' : 'Open menu'}
                aria-expanded={isMenuOpen}
                onClick={() => setIsMenuOpen((open) => !open)}
                className="md:hidden p-2 rounded-full text-[#6b756e] hover:bg-[#ebefe6] transition"
              >
                {isMenuOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
              </button>
            </div>
          )}
        </div>

        {user && isMenuOpen && (
          <div className="md:hidden border-t border-[#e1e7dc] px-4 py-3 space-y-1">
            {navLinks}
            <button
              onClick={handleLogout}
              className="flex w-full items-center gap-2 rounded-full px-3.5 py-2 text-sm text-[#6b756e] hover:bg-[#ebefe6] transition"
            >
              <LogOut className="w-4 h-4" />
              Log out
            </button>
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
  const visibleItems = NAV_ITEMS.filter((item) => !item.roles || (role && item.roles.includes(role)));

  return visibleItems.map(({ href, label, icon: Icon }) => {
    const isActive = pathname === href || pathname.startsWith(`${href}/`);
    return (
      <Link
        key={href}
        href={href}
        onClick={onNavigate}
        className={`flex items-center gap-2 rounded-full px-3.5 py-2 text-sm transition ${isActive ? 'bg-[#dce8d8] text-[#3a5235] font-medium' : 'text-[#6b756e] hover:bg-[#ebefe6] hover:text-[#242b26]'}`}
      >
        <Icon className="w-4 h-4" />
        {label}
      </Link>
    );
  });
}
