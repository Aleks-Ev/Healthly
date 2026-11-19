'use client';

// Форма регистрации пациента (макет Figma «Healthily registration»).
// В макете три поля (имя, e-mail, пароль); API требует ещё телефон, дату рождения, пол, город и согласия —
// они идут вторым блоком «A few details for your lab tests» в том же стиле.
// Клиентская валидация показывает ошибки сразу; ошибки сервера (ValidationProblem) выводятся под теми же полями.

import { useState, type ChangeEvent, type FormEvent, type ReactNode } from 'react';
import { ArrowRight, Check, ChevronDown } from 'lucide-react';
import { CONTROL_CLASS, LABEL_CLASS, PasswordField, SelectField, TextField, fieldError } from '@/components/ui/Field';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import { ApiError, getErrorMessage } from '@/lib/api';
import { useI18n } from '@/lib/i18n';
import type { Dictionary } from '@/locales/en';
import { GENDERS, TESTING_CITIES, type Gender, type RegisterRequest, type TestingCity } from '@/types/api';

interface RegisterFormProps {
  onSubmit: (data: RegisterRequest) => Promise<void>;
}

interface FormState {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  phone: string; // 10 цифр после +90, отформатированы "5XX XXX XX XX"
  dateOfBirth: string; // "MM/DD/YYYY"
  gender: Gender | '';
  city: TestingCity | '';
  accessCode: string;
  marketingOptIn: boolean;
  acceptTerms: boolean;
  acceptMedicalAuthorization: boolean;
}

type FieldName = keyof FormState;
type Errors = Partial<Record<FieldName, string>>;

const INITIAL_STATE: FormState = {
  email: '',
  password: '',
  firstName: '',
  lastName: '',
  phone: '',
  dateOfBirth: '',
  gender: '',
  city: '',
  accessCode: '',
  marketingOptIn: false,
  acceptTerms: false,
  acceptMedicalAuthorization: false,
};

// Поля формы → ключи ошибок API (ASP.NET возвращает имена свойств RegisterRequest)
const SERVER_FIELDS: Partial<Record<FieldName, string>> = { phone: 'phoneNumber' };

// "5321234567" → "532 123 45 67"
function formatPhone(value: string): string {
  const digits = value.replace(/\D/g, '').slice(0, 10);
  const parts = [digits.slice(0, 3), digits.slice(3, 6), digits.slice(6, 8), digits.slice(8, 10)];
  return parts.filter(Boolean).join(' ');
}

// "05141990" → "05/14/1990"
function formatDateInput(value: string): string {
  const digits = value.replace(/\D/g, '').slice(0, 8);
  const parts = [digits.slice(0, 2), digits.slice(2, 4), digits.slice(4, 8)];
  return parts.filter(Boolean).join('/');
}

// "MM/DD/YYYY" → "YYYY-MM-DD" или null, если такой даты не существует
function toIsoDate(value: string): string | null {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value);
  if (!match) {
    return null;
  }
  const [, month, day, year] = match.map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  const isValid = date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
  return isValid ? `${match[3]}-${match[1]}-${match[2]}` : null;
}

function ageOn(isoDate: string, today: Date): number {
  const [year, month, day] = isoDate.split('-').map(Number);
  let age = today.getFullYear() - year;
  if (today.getMonth() + 1 < month || (today.getMonth() + 1 === month && today.getDate() < day)) {
    age--;
  }
  return age;
}

type ErrorMessages = Dictionary['auth']['register']['errors'];

function validate(form: FormState, m: ErrorMessages): Errors {
  const errors: Errors = {};

  if (!form.email.trim()) {
    errors.email = m.emailRequired;
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) {
    errors.email = m.emailInvalid;
  }

  if (form.password.length < 8) {
    errors.password = m.passwordShort;
  }

  if (!form.firstName.trim()) {
    errors.firstName = m.firstNameRequired;
  }
  if (!form.lastName.trim()) {
    errors.lastName = m.lastNameRequired;
  }

  if (!form.phone) {
    errors.phone = m.phoneRequired;
  } else if (form.phone.replace(/\D/g, '').length !== 10) {
    errors.phone = m.phoneDigits;
  }

  const isoDate = toIsoDate(form.dateOfBirth);
  if (!form.dateOfBirth) {
    errors.dateOfBirth = m.dateRequired;
  } else if (!isoDate) {
    errors.dateOfBirth = m.dateFormat;
  } else {
    const age = ageOn(isoDate, new Date());
    if (age < 18) {
      errors.dateOfBirth = m.dateTooYoung;
    } else if (age > 120) {
      errors.dateOfBirth = m.dateInvalid;
    }
  }

  if (!form.gender) {
    errors.gender = m.genderRequired;
  }
  if (!form.city) {
    errors.city = m.cityRequired;
  }
  if (!form.acceptTerms) {
    errors.acceptTerms = m.consentRequired;
  }
  if (!form.acceptMedicalAuthorization) {
    errors.acceptMedicalAuthorization = m.consentRequired;
  }

  return errors;
}

export default function RegisterForm({ onSubmit }: RegisterFormProps) {
  const { t } = useI18n();
  const r = t.auth.register;
  const [form, setForm] = useState<FormState>(INITIAL_STATE);
  const [errors, setErrors] = useState<Errors>({});
  const [serverErrors, setServerErrors] = useState<Record<string, string[]>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isAccessCodeOpen, setIsAccessCodeOpen] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Ошибка поля: сначала клиентская, затем серверная
  const errorFor = (field: FieldName) => errors[field] ?? fieldError(serverErrors, SERVER_FIELDS[field] ?? field);

  const setField = <K extends FieldName>(field: K, value: FormState[K]) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    // Исправленное поле перестаёт подсвечиваться
    setErrors((prev) => ({ ...prev, [field]: undefined }));
    setServerErrors((prev) => {
      const key = SERVER_FIELDS[field] ?? field;
      return Object.fromEntries(Object.entries(prev).filter(([name]) => name.replace('$.', '').toLowerCase() !== key.toLowerCase()));
    });
  };

  const text = (field: 'email' | 'password' | 'firstName' | 'lastName' | 'accessCode') => (event: ChangeEvent<HTMLInputElement>) =>
    setField(field, event.target.value);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    const validationErrors = validate(form, r.errors);
    setErrors(validationErrors);
    if (Object.keys(validationErrors).length > 0) {
      setFormError(r.errors.fixFields);
      return;
    }

    setIsSubmitting(true);
    setServerErrors({});
    try {
      await onSubmit({
        email: form.email.trim(),
        password: form.password,
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        phoneNumber: `+90 ${form.phone}`,
        dateOfBirth: toIsoDate(form.dateOfBirth)!,
        gender: form.gender as Gender,
        city: form.city as TestingCity,
        accessCode: form.accessCode.trim() || null,
        marketingOptIn: form.marketingOptIn,
        acceptTerms: form.acceptTerms,
        acceptMedicalAuthorization: form.acceptMedicalAuthorization,
      });
    } catch (err) {
      setFormError(getErrorMessage(err));
      setServerErrors(err instanceof ApiError ? err.fieldErrors : {});
      setIsSubmitting(false);
    }
  };

  const cityOptions = [{ value: '', label: r.cityPlaceholder }, ...TESTING_CITIES.map((city) => ({ value: city, label: city }))];

  return (
    <form onSubmit={handleSubmit} noValidate className="space-y-5">
      <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 sm:gap-4">
        <TextField label={r.firstName} id="firstName" autoComplete="given-name" placeholder={r.namePlaceholder} value={form.firstName} onChange={text('firstName')} error={errorFor('firstName')} />
        <TextField label={r.lastName} id="lastName" autoComplete="family-name" placeholder={r.namePlaceholder} value={form.lastName} onChange={text('lastName')} error={errorFor('lastName')} />
      </div>

      <TextField label={r.email} id="email" type="email" autoComplete="email" placeholder={r.emailPlaceholder} value={form.email} onChange={text('email')} error={errorFor('email')} />

      <PasswordField
        label={r.password}
        id="password"
        autoComplete="new-password"
        placeholder={r.passwordPlaceholder}
        hint={r.passwordHint}
        showLabel={t.auth.showPassword}
        hideLabel={t.auth.hidePassword}
        value={form.password}
        onChange={text('password')}
        error={errorFor('password')}
      />

      {/* Второй блок: то, что нужно для норм (пол, возраст) и для связи лаборатории */}
      <div className="border-t border-[#eceee7] pt-6">
        <h2 className="text-[15px] font-semibold text-[#2a302b]">{r.detailsTitle}</h2>
        <p className="mt-1 text-[13px] leading-relaxed text-[#6c736c]">{r.detailsText}</p>
      </div>

      <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 sm:gap-4">
        <PhoneField label={r.phone} value={form.phone} onChange={(value) => setField('phone', formatPhone(value))} error={errorFor('phone')} />
        <TextField
          label={r.dateOfBirth}
          id="dateOfBirth"
          inputMode="numeric"
          autoComplete="bday"
          placeholder="MM/DD/YYYY"
          value={form.dateOfBirth}
          onChange={(event) => setField('dateOfBirth', formatDateInput(event.target.value))}
          error={errorFor('dateOfBirth')}
        />
      </div>

      <fieldset>
        <legend className={`mb-2 ${LABEL_CLASS}`}>{r.gender}</legend>
        <div role="radiogroup" className="grid grid-cols-3 gap-2">
          {GENDERS.map((gender) => {
            const isSelected = form.gender === gender;
            return (
              <label
                key={gender}
                className={`flex min-w-0 cursor-pointer items-center justify-center gap-2 rounded-xl border px-2 py-3 text-sm transition has-focus-visible:ring-4 has-focus-visible:ring-[#737f69]/15 ${
                  isSelected
                    ? 'border-[#8d9a84] bg-[#eef2ea] font-medium text-[#2a302b]'
                    : `bg-[#fdfdf9] text-[#4a524c] hover:border-[#cfd4c7] ${errorFor('gender') ? 'border-red-300' : 'border-[#e3e6dc]'}`
                }`}
              >
                <input type="radio" name="gender" value={gender} checked={isSelected} onChange={() => setField('gender', gender)} className="sr-only" />
                <span className={`flex h-4 w-4 shrink-0 items-center justify-center rounded-full border ${isSelected ? 'border-[#737f69]' : 'border-[#c9cdc2]'}`}>
                  {isSelected && <span className="h-2 w-2 rounded-full bg-[#737f69]" />}
                </span>
                <span className="truncate">{r.genders[gender] ?? gender}</span>
              </label>
            );
          })}
        </div>
        <FieldErrorText error={errorFor('gender')} />
      </fieldset>

      <SelectField
        label={r.city}
        id="city"
        value={form.city}
        onChange={(event) => setField('city', event.target.value as TestingCity | '')}
        options={cityOptions}
        error={errorFor('city')}
      />

      <div className="rounded-xl border border-[#e3e6dc] bg-[#fafaf6]">
        <button
          type="button"
          onClick={() => setIsAccessCodeOpen((open) => !open)}
          aria-expanded={isAccessCodeOpen}
          aria-controls="access-code-panel"
          className="flex w-full items-center justify-between px-4 py-3 text-sm font-medium text-[#2f3530]"
        >
          {r.accessCode}
          <ChevronDown className={`h-4 w-4 text-[#6c736c] transition-transform ${isAccessCodeOpen ? 'rotate-180' : ''}`} />
        </button>
        {isAccessCodeOpen && (
          <div id="access-code-panel" className="px-4 pb-4">
            <TextField label={r.accessCodeLabel} id="accessCode" autoComplete="off" value={form.accessCode} onChange={text('accessCode')} error={errorFor('accessCode')} />
          </div>
        )}
      </div>

      <div className="space-y-4 pt-1">
        <Checkbox id="acceptTerms" checked={form.acceptTerms} onChange={(checked) => setField('acceptTerms', checked)} error={errorFor('acceptTerms')}>
          {r.termsPrefix} <DocumentName>{r.privacyPolicy}</DocumentName>, <DocumentName>{r.termsOfService}</DocumentName> {r.and}{' '}
          <DocumentName>{r.labResultsRequest}</DocumentName>.
        </Checkbox>
        <Checkbox
          id="acceptMedicalAuthorization"
          checked={form.acceptMedicalAuthorization}
          onChange={(checked) => setField('acceptMedicalAuthorization', checked)}
          error={errorFor('acceptMedicalAuthorization')}
        >
          {r.termsPrefix} <DocumentName>{r.medicalAuthorization}</DocumentName>.
        </Checkbox>
        <Checkbox id="marketingOptIn" checked={form.marketingOptIn} onChange={(checked) => setField('marketingOptIn', checked)}>
          {r.marketing}
        </Checkbox>
      </div>

      {formError && <Alert>{formError}</Alert>}

      <Button type="submit" className="w-full rounded-xl! py-4! text-[15px]! shadow-[0_10px_22px_-14px_rgba(70,82,64,0.9)]" isLoading={isSubmitting}>
        {r.submit}
        {!isSubmitting && <ArrowRight className="h-4 w-4" />}
      </Button>
    </form>
  );
}

function FieldErrorText({ error }: { error?: string }) {
  return error ? <p className="mt-1.5 text-xs text-red-600">{error}</p> : null;
}

// Название юридического документа (отдельных страниц пока нет)
function DocumentName({ children }: { children: ReactNode }) {
  return <span className="text-[#2f3530] underline decoration-[#c9cdc2] underline-offset-2">{children}</span>;
}

function PhoneField({ label, value, onChange, error }: { label: string; value: string; onChange: (value: string) => void; error?: string }) {
  return (
    <div className="space-y-2">
      <label htmlFor="phone" className={LABEL_CLASS}>{label}</label>
      <div className="relative">
        <span className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 select-none text-[15px] text-[#6c736c]">+90</span>
        <input
          id="phone"
          type="tel"
          inputMode="tel"
          autoComplete="tel-national"
          placeholder="5XX XXX XX XX"
          aria-invalid={Boolean(error)}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          className={`${CONTROL_CLASS} pl-13`}
        />
      </div>
      <FieldErrorText error={error} />
    </div>
  );
}

interface CheckboxProps {
  id: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  error?: string;
  children: ReactNode;
}

function Checkbox({ id, checked, onChange, error, children }: CheckboxProps) {
  return (
    <div>
      <label htmlFor={id} className="flex cursor-pointer items-start gap-3 text-[13px] leading-relaxed text-[#5b625b]">
        <input id={id} type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} aria-invalid={Boolean(error)} className="peer sr-only" />
        <span
          aria-hidden
          className={`mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-md border transition peer-focus-visible:ring-4 peer-focus-visible:ring-[#737f69]/15 ${
            checked ? 'border-[#737f69] bg-[#737f69] text-white' : error ? 'border-red-300 bg-white' : 'border-[#cfd3c8] bg-[#fdfdf9]'
          }`}
        >
          {checked && <Check className="h-3.5 w-3.5" strokeWidth={3} />}
        </span>
        <span>{children}</span>
      </label>
      {error && <p className="mt-1 pl-8 text-xs text-red-600">{error}</p>}
    </div>
  );
}
