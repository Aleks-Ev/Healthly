import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes } from 'react';

const CONTROL_CLASS =
  'w-full rounded-xl border border-[#e1e7dc] bg-white px-3.5 py-2.5 text-sm text-[#242b26] placeholder:text-[#a3aba5] outline-none transition focus:border-[#72826c] focus:ring-2 focus:ring-[#72826c]/20 aria-[invalid=true]:border-red-400';

interface WrapperProps {
  label: string;
  id: string;
  error?: string;
  hint?: string;
  children: ReactNode;
}

function FieldWrapper({ label, id, error, hint, children }: WrapperProps) {
  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-xs font-medium text-[#6b756e]">{label}</label>
      {children}
      {error ? <p className="text-xs text-red-600">{error}</p> : hint && <p className="text-xs text-[#8e9890]">{hint}</p>}
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
