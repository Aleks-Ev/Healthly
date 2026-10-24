'use client';

// Модальная форма пользователя.
// - user = null  → создание (только Admin): все поля + роль + пароль
// - user задан   → редактирование; роль и новый пароль видит только Admin (isAdmin)

import { useState, type FormEvent } from 'react';
import Modal from '@/components/ui/Modal';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import { SelectField, TextField, fieldError } from '@/components/ui/Field';
import { ApiError, getErrorMessage, usersApi } from '@/lib/api';
import { toNumberOrNull } from '@/lib/format';
import { ROLES, type Role, type User } from '@/types/api';

export const GENDER_OPTIONS = [
  { value: '', label: 'Not specified' },
  { value: 'Female', label: 'Female' },
  { value: 'Male', label: 'Male' },
  { value: 'Other', label: 'Other' },
];

interface UserFormModalProps {
  isOpen: boolean;
  user: User | null;
  isAdmin: boolean;
  onClose: () => void;
  onSaved: (user: User) => void;
}

export default function UserFormModal(props: UserFormModalProps) {
  // key пересоздаёт форму при открытии для другого пользователя — поля заполняются заново
  if (!props.isOpen) {
    return null;
  }
  return <UserForm key={props.user?.id ?? 'new'} {...props} />;
}

function UserForm({ user, isAdmin, onClose, onSaved }: UserFormModalProps) {
  const isEdit = user !== null;
  const [form, setForm] = useState({
    fullName: user?.fullName ?? '',
    email: user?.email ?? '',
    role: (user?.role ?? 'Patient') as Role,
    password: '',
    height: user?.height?.toString() ?? '',
    weight: user?.weight?.toString() ?? '',
    gender: user?.gender ?? '',
  });
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [isSaving, setIsSaving] = useState(false);

  const update = (field: keyof typeof form) => (event: { target: { value: string } }) =>
    setForm((prev) => ({ ...prev, [field]: event.target.value }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setFieldErrors({});
    setIsSaving(true);

    const profile = {
      fullName: form.fullName,
      email: form.email,
      height: toNumberOrNull(form.height),
      weight: toNumberOrNull(form.weight),
      gender: form.gender || null,
    };

    try {
      const saved = isEdit
        ? await usersApi.update(user.id, {
            ...profile,
            // Не-админ не отправляет role/password совсем — иначе API вернёт 403
            role: isAdmin ? form.role : null,
            password: isAdmin && form.password ? form.password : null,
          })
        : await usersApi.create({ ...profile, role: form.role, password: form.password });
      onSaved(saved);
      onClose();
    } catch (err) {
      setError(getErrorMessage(err));
      setFieldErrors(err instanceof ApiError ? err.fieldErrors : {});
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Modal
      isOpen
      title={isEdit ? 'Edit profile' : 'Create user'}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button type="submit" form="user-form" isLoading={isSaving}>{isEdit ? 'Save changes' : 'Create user'}</Button>
        </>
      }
    >
      <form id="user-form" onSubmit={handleSubmit} className="space-y-4">
        <TextField label="Full name" id="user-fullName" required value={form.fullName} onChange={update('fullName')} error={fieldError(fieldErrors, 'fullName')} />
        <TextField label="Email" id="user-email" type="email" required value={form.email} onChange={update('email')} error={fieldError(fieldErrors, 'email')} />

        {isAdmin && (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <SelectField
              label="Role"
              id="user-role"
              value={form.role}
              onChange={update('role')}
              options={ROLES.map((role) => ({ value: role, label: role }))}
              error={fieldError(fieldErrors, 'role')}
            />
            <TextField
              label={isEdit ? 'New password' : 'Password'}
              id="user-password"
              type="password"
              autoComplete="new-password"
              required={!isEdit}
              minLength={8}
              hint={isEdit ? 'Leave empty to keep the current one' : 'At least 8 characters'}
              value={form.password}
              onChange={update('password')}
              error={fieldError(fieldErrors, 'password')}
            />
          </div>
        )}

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <TextField label="Height (cm)" id="user-height" inputMode="decimal" value={form.height} onChange={update('height')} error={fieldError(fieldErrors, 'height')} />
          <TextField label="Weight (kg)" id="user-weight" inputMode="decimal" value={form.weight} onChange={update('weight')} error={fieldError(fieldErrors, 'weight')} />
          <SelectField label="Gender" id="user-gender" value={form.gender} onChange={update('gender')} options={GENDER_OPTIONS} />
        </div>

        {error && <Alert>{error}</Alert>}
      </form>
    </Modal>
  );
}
