'use client';

import { useState, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes } from 'react';
import { Eye, EyeOff } from 'lucide-react';

// Поле в стиле макета Figma: высокое, кремовая заливка, тонкая граница, мягкий оливковый фокус
export const CONTROL_CLASS =
  'w-full rounded-xl border border-[#e3e6dc] bg-[#fdfdf9] px-4 py-3 text-[15px] text-[#2a302b] placeholder:text-[#a3aaa3] outline-none transition focus:border-[#8d9a84] focus:bg-white focus:ring-4 focus:ring-[#737f69]/10 aria-[invalid=true]:border-red-300 aria-[invalid=true]:bg-red-50/30';

export const LABEL_CLASS = 'block text-sm font-medium text-[#2f3530]';

interface WrapperProps {
  label: string;
  id: string;
  error?: string;
  hint?: string;
  children: ReactNode;
}

export function FieldWrapper({ label, id, error, hint, children }: WrapperProps) {
  return (
    <div className="space-y-2">
      <label htmlFor={id} className={LABEL_CLASS}>{label}</label>
      {children}
      {error ? <p className="text-xs text-red-600">{error}</p> : hint && <p className="text-xs text-[#6c736c]">{hint}</p>}
    </div>
  );
}

interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  id: string;
  error?: string;
  hint?: string;
}

export function TextField({ label, id, error, hint, ...props }: TextFieldProps) {
  return (
    <FieldWrapper label={label} id={id} error={error} hint={hint}>
      <input id={id} aria-invalid={Boolean(error)} className={CONTROL_CLASS} {...props} />
    </FieldWrapper>
  );
}

interface PasswordFieldProps extends Omit<TextFieldProps, 'type'> {
  showLabel?: string;
  hideLabel?: string;
}

// Пароль с кнопкой «показать» (глаз справа, как в макете)
export function PasswordField({ label, id, error, hint, showLabel = 'Show password', hideLabel = 'Hide password', ...props }: PasswordFieldProps) {
  const [isVisible, setIsVisible] = useState(false);
  const Icon = isVisible ? EyeOff : Eye;

  return (
    <FieldWrapper label={label} id={id} error={error} hint={hint}>
      <div className="relative">
        <input id={id} type={isVisible ? 'text' : 'password'} aria-invalid={Boolean(error)} className={`${CONTROL_CLASS} pr-12`} {...props} />
        <button
          type="button"
          onClick={() => setIsVisible((visible) => !visible)}
          aria-label={isVisible ? hideLabel : showLabel}
          aria-pressed={isVisible}
          className="absolute right-2 top-1/2 -translate-y-1/2 rounded-lg p-2 text-[#6c736c] transition hover:text-[#2a302b] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#737f69]/30"
        >
          <Icon className="h-[18px] w-[18px]" strokeWidth={1.75} />
        </button>
      </div>
    </FieldWrapper>
  );
}

interface SelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string;
  id: string;
  error?: string;
  options: { value: string; label: string }[];
}

export function SelectField({ label, id, error, options, ...props }: SelectFieldProps) {
  return (
    <FieldWrapper label={label} id={id} error={error}>
      <select id={id} aria-invalid={Boolean(error)} className={CONTROL_CLASS} {...props}>
        {options.map((option) => (
          <option key={option.value} value={option.value}>{option.label}</option>
        ))}
      </select>
    </FieldWrapper>
  );
}

// Ошибка валидации ASP.NET для поля (ключи приходят как "Email", "FullName" или "$.email")
export function fieldError(errors: Record<string, string[]>, field: string): string | undefined {
  const key = Object.keys(errors).find((name) => name.replace('$.', '').toLowerCase() === field.toLowerCase());
  return key ? errors[key][0] : undefined;
}
