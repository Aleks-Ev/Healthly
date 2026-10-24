'use client';

// Форма регистрации пациента (макет в стиле Function Health).
// Клиентская валидация показывает ошибки сразу; ошибки сервера (ValidationProblem) выводятся под теми же полями.

import { useState, type ChangeEvent, type FormEvent, type ReactNode } from 'react';
import { Check, ChevronDown } from 'lucide-react';
import { SelectField, TextField, fieldError } from '@/components/ui/Field';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import { ApiError, getErrorMessage } from '@/lib/api';
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

const CITY_OPTIONS = [
  { value: '', label: 'Select a city' },
  ...TESTING_CITIES.map((city) => ({ value: city, label: city })),
];

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

function validate(form: FormState): Errors {
  const errors: Errors = {};

  if (!form.email.trim()) {
    errors.email = 'Email is required';
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) {
    errors.email = 'Enter a valid email address';
  }

  if (form.password.length < 8) {
    errors.password = 'Password must be at least 8 characters';
  }

  if (!form.firstName.trim()) {
    errors.firstName = 'First name is required';
  }
  if (!form.lastName.trim()) {
    errors.lastName = 'Last name is required';
  }

  if (!form.phone) {
    errors.phone = 'Phone number is required';
  } else if (form.phone.replace(/\D/g, '').length !== 10) {
    errors.phone = 'Enter 10 digits after +90';
  }

  const isoDate = toIsoDate(form.dateOfBirth);
  if (!form.dateOfBirth) {
    errors.dateOfBirth = 'Date of birth is required';
  } else if (!isoDate) {
    errors.dateOfBirth = 'Use the format MM/DD/YYYY';
  } else {
    const age = ageOn(isoDate, new Date());
    if (age < 18) {
      errors.dateOfBirth = 'You must be at least 18 years old';
    } else if (age > 120) {
      errors.dateOfBirth = 'Enter a valid date of birth';
    }
  }

  if (!form.gender) {
    errors.gender = 'Select your gender';
  }
  if (!form.city) {
    errors.city = 'Select where you will be testing';
  }
  if (!form.acceptTerms) {
    errors.acceptTerms = 'You must accept to continue';
  }
  if (!form.acceptMedicalAuthorization) {
    errors.acceptMedicalAuthorization = 'You must accept to continue';
  }

  return errors;
}

export default function RegisterForm({ onSubmit }: RegisterFormProps) {
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

    const validationErrors = validate(form);
    setErrors(validationErrors);
    if (Object.keys(validationErrors).length > 0) {
      setFormError('Please fix the highlighted fields.');
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

  return (
    <form onSubmit={handleSubmit} noValidate className="space-y-6">
      <TextField label="Email*" id="email" type="email" autoComplete="email" placeholder="you@example.com" value={form.email} onChange={text('email')} error={errorFor('email')} />

      <TextField label="Password*" id="password" type="password" autoComplete="new-password" hint="At least 8 characters" value={form.password} onChange={text('password')} error={errorFor('password')} />

      <fieldset className="space-y-2">
        <legend className="mb-2 text-sm font-medium text-[#242b26]">Legal name*</legend>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <TextField label="First name" id="firstName" autoComplete="given-name" value={form.firstName} onChange={text('firstName')} error={errorFor('firstName')} />
          <TextField label="Last name" id="lastName" autoComplete="family-name" value={form.lastName} onChange={text('lastName')} error={errorFor('lastName')} />
        </div>
      </fieldset>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <PhoneField value={form.phone} onChange={(value) => setField('phone', formatPhone(value))} error={errorFor('phone')} />
        <TextField
          label="Date of birth*"
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
        <legend className="mb-2 text-sm font-medium text-[#242b26]">Gender*</legend>
        <div role="radiogroup" className="grid grid-cols-3 gap-2">
          {GENDERS.map((gender) => (
            <label
              key={gender}
              className={`flex cursor-pointer items-center justify-center gap-2 rounded-xl border px-3 py-2.5 text-sm transition has-focus-visible:ring-2 has-focus-visible:ring-[#72826c]/30 ${
                form.gender === gender
                  ? 'border-[#72826c] bg-[#eef2ea] text-[#242b26] font-medium'
                  : `bg-white text-[#4a524c] hover:border-[#c9c1b0] ${errorFor('gender') ? 'border-red-400' : 'border-[#e6dfd1]'}`
              }`}
            >
              <input type="radio" name="gender" value={gender} checked={form.gender === gender} onChange={() => setField('gender', gender)} className="sr-only" />
              <span className={`flex h-4 w-4 items-center justify-center rounded-full border ${form.gender === gender ? 'border-[#72826c]' : 'border-[#c9c1b0]'}`}>
                {form.gender === gender && <span className="h-2 w-2 rounded-full bg-[#72826c]" />}
              </span>
              {gender}
            </label>
          ))}
        </div>
        <FieldErrorText error={errorFor('gender')} />
      </fieldset>

      <SelectField
        label="Where will you be testing?*"
        id="city"
        value={form.city}
        onChange={(event) => setField('city', event.target.value as TestingCity | '')}
        options={CITY_OPTIONS}
        error={errorFor('city')}
      />

      <div className="rounded-2xl border border-[#ebe4d6] bg-[#faf6ef]">
        <button
          type="button"
          onClick={() => setIsAccessCodeOpen((open) => !open)}
          aria-expanded={isAccessCodeOpen}
          aria-controls="access-code-panel"
          className="flex w-full items-center justify-between px-4 py-3 text-sm font-medium text-[#242b26]"
        >
          Have an access code?
          <ChevronDown className={`h-4 w-4 text-[#6b756e] transition-transform ${isAccessCodeOpen ? 'rotate-180' : ''}`} />
        </button>
        {isAccessCodeOpen && (
          <div id="access-code-panel" className="px-4 pb-4">
            <TextField label="Access code (optional)" id="accessCode" autoComplete="off" value={form.accessCode} onChange={text('accessCode')} error={errorFor('accessCode')} />
          </div>
        )}
      </div>

      <div className="space-y-4 border-t border-[#ebe4d6] pt-6">
        <Checkbox id="marketingOptIn" checked={form.marketingOptIn} onChange={(checked) => setField('marketingOptIn', checked)}>
          I would like to receive news, health tips and special offers from Healthly. You can unsubscribe at any time.
        </Checkbox>
        <Checkbox id="acceptTerms" checked={form.acceptTerms} onChange={(checked) => setField('acceptTerms', checked)} error={errorFor('acceptTerms')}>
          I agree to the <DocumentName>Privacy Policy</DocumentName>, <DocumentName>Terms of Service</DocumentName> and{' '}
          <DocumentName>Request for Lab Results</DocumentName>.*
        </Checkbox>
        <Checkbox
          id="acceptMedicalAuthorization"
          checked={form.acceptMedicalAuthorization}
          onChange={(checked) => setField('acceptMedicalAuthorization', checked)}
          error={errorFor('acceptMedicalAuthorization')}
        >
          I agree to the <DocumentName>Authorization for Use of Medical Information</DocumentName>.*
        </Checkbox>
      </div>

      {formError && <Alert>{formError}</Alert>}

      <Button type="submit" className="w-full py-3.5! text-base" isLoading={isSubmitting}>
        Get started
      </Button>
    </form>
  );
}

function FieldErrorText({ error }: { error?: string }) {
  return error ? <p className="mt-1.5 text-xs text-red-600">{error}</p> : null;
}

// Название юридического документа (отдельных страниц пока нет)
function DocumentName({ children }: { children: ReactNode }) {
  return <span className="font-medium text-[#242b26] underline decoration-[#c9c1b0] underline-offset-2">{children}</span>;
}

function PhoneField({ value, onChange, error }: { value: string; onChange: (value: string) => void; error?: string }) {
  return (
    <div className="space-y-1.5">
      <label htmlFor="phone" className="block text-xs font-medium text-[#6b756e]">Phone number*</label>
      <div
        className={`flex items-center rounded-xl border bg-white transition focus-within:border-[#72826c] focus-within:ring-2 focus-within:ring-[#72826c]/20 ${
          error ? 'border-red-400' : 'border-[#e1e7dc]'
        }`}
      >
        <span className="pl-3.5 pr-2 text-sm text-[#6b756e] select-none">+90</span>
        <input
          id="phone"
          type="tel"
          inputMode="tel"
          autoComplete="tel-national"
          placeholder="5XX XXX XX XX"
          aria-invalid={Boolean(error)}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          className="w-full rounded-r-xl bg-transparent py-2.5 pr-3.5 text-sm text-[#242b26] placeholder:text-[#a3aba5] outline-none"
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
      <label htmlFor={id} className="flex cursor-pointer items-start gap-3 text-sm leading-relaxed text-[#4a524c]">
        <input id={id} type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} aria-invalid={Boolean(error)} className="peer sr-only" />
        <span
          aria-hidden
          className={`mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-md border transition peer-focus-visible:ring-2 peer-focus-visible:ring-[#72826c]/30 ${
            checked ? 'border-[#72826c] bg-[#72826c] text-white' : error ? 'border-red-400 bg-white' : 'border-[#c9c1b0] bg-white'
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
