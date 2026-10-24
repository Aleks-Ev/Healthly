'use client';

// Защита страницы: без входа — редирект на /login, без нужной роли — сообщение об отказе.
// Это удобство интерфейса; настоящую защиту данных обеспечивает API (401/403).

import { useEffect, type ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import { ShieldAlert } from 'lucide-react';
import { useAuth } from '@/lib/auth';
import { LoadingState } from '@/components/ui/Card';
import type { Role } from '@/types/api';

interface RequireAuthProps {
  roles?: Role[];
  children: ReactNode;
}

export default function RequireAuth({ roles, children }: RequireAuthProps) {
  const { user, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && !user) {
      router.replace('/login');
    }
  }, [isLoading, user, router]);

  if (isLoading || !user) {
    return <LoadingState />;
  }

  if (roles && !roles.includes(user.role)) {
    return (
      <div className="flex flex-col items-center gap-3 py-16 text-center">
        <ShieldAlert className="w-10 h-10 text-[#72826c]" />
        <h1 className="text-lg font-semibold text-[#242b26]">Access denied</h1>
        <p className="text-sm text-[#6b756e]">This page is available to: {roles.join(', ')}.</p>
      </div>
    );
  }

  return <>{children}</>;
}
