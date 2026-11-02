'use client';

// Модальная форма пользователя.
// - user = null  → создание (только Admin): все поля + роль + пароль
// - user задан   → редактирование; роль и новый пароль видит только Admin (isAdmin)
// Телефон, дата рождения и город — те же правила, что при регистрации (проверяет сервер). Для пациента дата рождения и пол
// обязательны при создании: по ним выбираются нормы анализов. При редактировании пустой телефон/город — очистить.

import { useCallback, useState, type FormEvent } from 'react';
import Modal from '@/components/ui/Modal';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import { SelectField, TextField, fieldError } from '@/components/ui/Field';
import { ApiError, getErrorMessage, laboratoriesApi, usersApi } from '@/lib/api';
import { useApiData } from '@/lib/useApiData';
import { toNumberOrNull } from '@/lib/format';
import { ROLES, TESTING_CITIES, type Role, type User } from '@/types/api';

const CITY_OPTIONS = [{ value: '', label: 'Not specified' }, ...TESTING_CITIES.map((city) => ({ value: city, label: city }))];

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
    phoneNumber: user?.phoneNumber ?? '',
    dateOfBirth: user?.dateOfBirth ?? '',
    city: user?.city ?? '',
    laboratoryId: user?.laboratoryId?.toString() ?? '',
  });
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [isSaving, setIsSaving] = useState(false);

  // Для пациента дата рождения и пол нужны для норм анализов; обязательны только при создании —
  // у существующих аккаунтов их можно дописать позже
  const isPatient = (isEdit ? user.role : form.role) === 'Patient';
  const requireBirthData = !isEdit && isPatient;
  // Сотруднику лаборатории админ выбирает лабораторию (список нужен только админу)
  const isLabStaff = form.role === 'LabStaff';
  const loadLaboratories = useCallback(() => (isAdmin ? laboratoriesApi.list() : Promise.resolve([])), [isAdmin]);
  const { data: laboratories } = useApiData(loadLaboratories);

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
      dateOfBirth: form.dateOfBirth || null,
      // Пусто: при создании — не указано; при редактировании — очистить, если раньше было, иначе не менять
      phoneNumber: form.phoneNumber.trim() || (user?.phoneNumber ? '' : null),
      city: form.city || (user?.city ? '' : null),
    };

    try {
      const saved = isEdit
        ? await usersApi.update(user.id, {
            ...profile,
            // Не-админ не отправляет role/password совсем — иначе API вернёт 403
            role: isAdmin ? form.role : null,
            password: isAdmin && form.password ? form.password : null,
            laboratoryId: isAdmin && isLabStaff ? toNumberOrNull(form.laboratoryId) : null,
          })
        : await usersApi.create({
            ...profile,
            role: form.role,
            password: form.password,
            laboratoryId: isLabStaff ? toNumberOrNull(form.laboratoryId) : null,
          });
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
              options={ROLES.map((role) => ({ value: role, label: role === 'LabStaff' ? 'Lab staff' : role }))}
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

        {isAdmin && isLabStaff && (
          <SelectField
            label="Laboratory"
            id="user-laboratory"
            required
            value={form.laboratoryId}
            onChange={update('laboratoryId')}
            options={[
              { value: '', label: laboratories && laboratories.length === 0 ? 'No laboratories — add one first' : 'Select a laboratory' },
              ...(laboratories ?? []).map((laboratory) => ({ value: laboratory.id.toString(), label: ` (${laboratory.city})` })),
            ]}
            error={fieldError(fieldErrors, 'laboratoryId')}
          />
        )}

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <TextField label="Height (cm)" id="user-height" inputMode="decimal" value={form.height} onChange={update('height')} error={fieldError(fieldErrors, 'height')} />
          <TextField label="Weight (kg)" id="user-weight" inputMode="decimal" value={form.weight} onChange={update('weight')} error={fieldError(fieldErrors, 'weight')} />
          <SelectField
            label="Gender"
            id="user-gender"
            required={requireBirthData}
            value={form.gender}
            onChange={update('gender')}
            options={GENDER_OPTIONS}
            error={fieldError(fieldErrors, 'gender')}
          />
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <TextField
            label="Date of birth"
            id="user-dateOfBirth"
            type="date"
            required={requireBirthData}
            value={form.dateOfBirth}
            onChange={update('dateOfBirth')}
            error={fieldError(fieldErrors, 'dateOfBirth')}
          />
          <TextField
            label="Phone"
            id="user-phoneNumber"
            type="tel"
            placeholder="+90 5XX XXX XX XX"
            value={form.phoneNumber}
            onChange={update('phoneNumber')}
            error={fieldError(fieldErrors, 'phoneNumber')}
          />
          <SelectField
            label="Testing city"
            id="user-city"
            value={form.city}
            onChange={update('city')}
            options={CITY_OPTIONS}
            error={fieldError(fieldErrors, 'city')}
          />
        </div>
        {isPatient && (
          <p className="text-xs text-[#6b756e]">
            Date of birth and gender are used to choose the right reference ranges for lab results.
          </p>
        )}

        {error && <Alert>{error}</Alert>}
      </form>
    </Modal>
  );
}
