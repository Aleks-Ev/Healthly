'use client';

import { useEffect } from 'react';
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

  useEffect(() => {
    if (user) {
      router.replace('/dashboard');
    }
  }, [user, router]);

  const handleRegister = async (data: RegisterRequest) => {
    await register(data);
    router.push('/dashboard');
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
