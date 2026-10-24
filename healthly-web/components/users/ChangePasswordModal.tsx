'use client';

// Смена пароля самим пользователем (PUT /api/auth/me/password).
// После успеха остальные сессии пользователя завершаются, а эта продолжается с новыми токенами (см. useAuth().changePassword).

import { useState, type FormEvent } from 'react';
import Modal from '@/components/ui/Modal';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import { TextField, fieldError } from '@/components/ui/Field';
import { ApiError, getErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth';

interface ChangePasswordModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function ChangePasswordModal({ isOpen, onClose }: ChangePasswordModalProps) {
  // Форма создаётся заново при каждом открытии — поля и сообщения сбрасываются
  if (!isOpen) {
    return null;
  }
  return <ChangePasswordForm onClose={onClose} />;
}

function ChangePasswordForm({ onClose }: { onClose: () => void }) {
  const { changePassword } = useAuth();
  const [form, setForm] = useState({ currentPassword: '', newPassword: '', confirmPassword: '' });
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [isSaving, setIsSaving] = useState(false);
  const [isDone, setIsDone] = useState(false);

  const update = (field: keyof typeof form) => (event: { target: { value: string } }) =>
    setForm((prev) => ({ ...prev, [field]: event.target.value }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setFieldErrors({});

    if (form.newPassword !== form.confirmPassword) {
      setFieldErrors({ confirmPassword: ['The passwords do not match'] });
      return;
    }

    setIsSaving(true);
    try {
      await changePassword({ currentPassword: form.currentPassword, newPassword: form.newPassword });
      setIsDone(true);
    } catch (err) {
      setError(getErrorMessage(err));
      setFieldErrors(err instanceof ApiError ? err.fieldErrors : {});
    } finally {
      setIsSaving(false);
    }
  };

  if (isDone) {
    return (
      <Modal isOpen title="Change password" onClose={onClose} footer={<Button onClick={onClose}>Done</Button>}>
        <Alert variant="success">Your password has been changed. You have been signed out on all other devices.</Alert>
      </Modal>
    );
  }

  return (
    <Modal
      isOpen
      title="Change password"
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button type="submit" form="change-password-form" isLoading={isSaving}>Change password</Button>
        </>
      }
    >
      <form id="change-password-form" onSubmit={handleSubmit} className="space-y-4">
        <TextField
          label="Current password"
          id="currentPassword"
          type="password"
          autoComplete="current-password"
          required
          value={form.currentPassword}
          onChange={update('currentPassword')}
          error={fieldError(fieldErrors, 'currentPassword')}
        />
        <TextField
          label="New password"
          id="newPassword"
          type="password"
          autoComplete="new-password"
          required
          minLength={8}
          hint="At least 8 characters"
          value={form.newPassword}
          onChange={update('newPassword')}
          error={fieldError(fieldErrors, 'newPassword')}
        />
        <TextField
          label="Confirm new password"
          id="confirmPassword"
          type="password"
          autoComplete="new-password"
          required
          value={form.confirmPassword}
          onChange={update('confirmPassword')}
          error={fieldError(fieldErrors, 'confirmPassword')}
        />
        <p className="text-xs text-[#8e9890]">Changing your password signs you out on all other devices.</p>
        {error && !Object.keys(fieldErrors).length && <Alert>{error}</Alert>}
      </form>
    </Modal>
  );
}
