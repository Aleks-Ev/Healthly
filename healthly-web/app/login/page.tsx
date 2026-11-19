'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowRight } from 'lucide-react';
import AuthLayout, { AuthSwitchLink } from '@/components/layout/AuthLayout';
import { PasswordField, TextField } from '@/components/ui/Field';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import { homePath, useAuth } from '@/lib/auth';
import { getErrorMessage } from '@/lib/api';
import { useI18n } from '@/lib/i18n';

export default function LoginPage() {
  const { user, login } = useAuth();
  const { t } = useI18n();
  const l = t.auth.login;
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Уже вошли — сразу в кабинет
  useEffect(() => {
    if (user) {
      router.replace(homePath(user.role));
    }
  }, [user, router]);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      const signedIn = await login({ email, password });
      router.push(homePath(signedIn.role));
    } catch (err) {
      setError(getErrorMessage(err));
      setIsSubmitting(false);
    }
  };

  return (
    <AuthLayout
      title={l.title}
      subtitle={l.subtitle}
      below={
        <>
          <span>{l.noAccount}</span>
          <AuthSwitchLink href="/register">{l.createAccount}</AuthSwitchLink>
        </>
      }
    >
      <form onSubmit={handleSubmit} className="space-y-5">
        <TextField label={l.email} id="email" type="email" autoComplete="email" placeholder={l.emailPlaceholder} required value={email} onChange={(e) => setEmail(e.target.value)} />
        <PasswordField
          label={l.password}
          id="password"
          autoComplete="current-password"
          placeholder={l.passwordPlaceholder}
          showLabel={t.auth.showPassword}
          hideLabel={t.auth.hidePassword}
          required
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        {error && <Alert>{error}</Alert>}
        <Button type="submit" className="w-full rounded-xl! py-4! text-[15px]! shadow-[0_10px_22px_-14px_rgba(70,82,64,0.9)]" isLoading={isSubmitting}>
          {l.submit}
          {!isSubmitting && <ArrowRight className="h-4 w-4" />}
        </Button>
      </form>
    </AuthLayout>
  );
}
