'use client';

import { useEffect, useState, type FormEvent } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import AuthLayout from '@/components/layout/AuthLayout';
import { TextField } from '@/components/ui/Field';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import { useAuth } from '@/lib/auth';
import { getErrorMessage } from '@/lib/api';

export default function LoginPage() {
  const { user, login } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Уже вошли — сразу в кабинет
  useEffect(() => {
    if (user) {
      router.replace('/dashboard');
    }
  }, [user, router]);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await login({ email, password });
      router.push('/dashboard');
    } catch (err) {
      setError(getErrorMessage(err));
      setIsSubmitting(false);
    }
  };

  return (
    <AuthLayout title="Welcome back" subtitle="Log in to see your health records.">
      <form onSubmit={handleSubmit} className="space-y-4">
        <TextField label="Email" id="email" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        <TextField label="Password" id="password" type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
        {error && <Alert>{error}</Alert>}
        <Button type="submit" className="w-full" isLoading={isSubmitting}>Log in</Button>
      </form>
      <p className="mt-6 text-center text-sm text-[#6b756e]">
        New to Healthly?{' '}
        <Link href="/register" className="font-medium text-[#72826c] hover:underline">Create an account</Link>
      </p>
    </AuthLayout>
  );
}
