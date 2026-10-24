'use client';

import { useEffect } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import AuthLayout from '@/components/layout/AuthLayout';
import RegisterForm from '@/components/auth/RegisterForm';
import { useAuth } from '@/lib/auth';
import type { RegisterRequest } from '@/types/api';

export default function RegisterPage() {
  const { user, register } = useAuth();
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
    <AuthLayout wide title="Create your account" subtitle="Join Healthly to keep your lab results and health data in one place.">
      <RegisterForm onSubmit={handleRegister} />
      <p className="mt-8 text-center text-sm text-[#6b756e]">
        Already have an account?{' '}
        <Link href="/login" className="font-medium text-[#72826c] hover:underline">Log in</Link>
      </p>
    </AuthLayout>
  );
}
