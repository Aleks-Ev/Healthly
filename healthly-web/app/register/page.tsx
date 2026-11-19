'use client';

import { useEffect, useRef } from 'react';
import { useRouter } from 'next/navigation';
import AuthLayout, { AuthSwitchLink } from '@/components/layout/AuthLayout';
import RegisterForm from '@/components/auth/RegisterForm';
import { useAuth } from '@/lib/auth';
import { useI18n } from '@/lib/i18n';
import type { RegisterRequest } from '@/types/api';

export default function RegisterPage() {
  const { user, register } = useAuth();
  const { t } = useI18n();
  const router = useRouter();

  // true, пока идёт регистрация: появившийся user — это только что созданный аккаунт, его ведём на /care,
  // а не в кабинет (иначе эффект ниже перебил бы переход)
  const isRegistering = useRef(false);

  // Уже вошли (открыли /register с активной сессией) — сразу в кабинет
  useEffect(() => {
    if (user && !isRegistering.current) {
      router.replace('/dashboard');
    }
  }, [user, router]);

  // После регистрации — сразу выбор врача и лаборатории (/care); там же можно «Skip for now»
  const handleRegister = async (data: RegisterRequest) => {
    isRegistering.current = true;
    try {
      await register(data);
    } catch (err) {
      isRegistering.current = false;
      throw err;
    }
    router.push('/care');
  };

  return (
    <AuthLayout
      wide
      title={t.auth.register.title}
      subtitle={t.auth.register.subtitle}
      below={
        <>
          <span>{t.auth.register.haveAccount}</span>
          <AuthSwitchLink href="/login">{t.auth.register.signIn}</AuthSwitchLink>
        </>
      }
    >
      <RegisterForm onSubmit={handleRegister} />
    </AuthLayout>
  );
}
