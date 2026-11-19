'use client';

// Общие мелочи экранов «команды здоровья»: статус связи, дата на языке интерфейса, аватар-инициалы, поле для сообщения.

import type { TextareaHTMLAttributes } from 'react';
import { CONTROL_CLASS, LABEL_CLASS } from '@/components/ui/Field';
import { useI18n } from '@/lib/i18n';
import type { CareConnectionStatus } from '@/types/api';

const STATUS_STYLES: Record<CareConnectionStatus, string> = {
  Requested: 'bg-[#eef0e9] text-[#5b625b]',
  Accepted: 'bg-[#f6ecd8] text-[#7a5a22]',
  Active: 'bg-[#dce8d8] text-[#3a5235]',
  Declined: 'bg-[#f6e6da] text-[#8a4a23]',
  Cancelled: 'bg-[#eef0e9] text-[#8e9890]',
  Ended: 'bg-[#eef0e9] text-[#8e9890]',
};

// perspective — чьими глазами: у пациента Accepted = «нужно ваше решение», у врача = «ждём пациента»
export function CareStatusBadge({ status, perspective = 'patient' }: { status: CareConnectionStatus; perspective?: 'patient' | 'provider' }) {
  const { t } = useI18n();
  const labels = perspective === 'patient' ? t.care.statuses : t.provider.statuses;
  return (
    <span className={`inline-flex shrink-0 items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_STYLES[status]}`}>
      {labels[status] ?? status}
    </span>
  );
}

// "Nov 20, 2026" / "20 Kas 2026"
export function useDateFormat() {
  const { intlLocale } = useI18n();
  return (iso: string | null) =>
    iso ? new Date(iso).toLocaleDateString(intlLocale, { year: 'numeric', month: 'short', day: 'numeric' }) : '—';
}

export function Initials({ name, tone = 'sage' }: { name: string; tone?: 'sage' | 'sand' }) {
  // «Dr.» в начале не считаем частью имени
  const letters = name
    .replace(/^dr\.?\s+/i, '')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toLocaleUpperCase())
    .join('');
  return (
    <span
      aria-hidden
      className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-sm font-semibold ${
        tone === 'sage' ? 'bg-[#dfe8db] text-[#3d5340]' : 'bg-[#f1e8d8] text-[#6b5326]'
      }`}
    >
      {letters || '·'}
    </span>
  );
}

interface TextAreaFieldProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label: string;
  id: string;
  hint?: string;
}

export function TextAreaField({ label, id, hint, ...props }: TextAreaFieldProps) {
  return (
    <div className="space-y-2">
      <label htmlFor={id} className={LABEL_CLASS}>{label}</label>
      <textarea id={id} rows={3} className={`${CONTROL_CLASS} resize-none`} {...props} />
      {hint && <p className="text-xs text-[#6c736c]">{hint}</p>}
    </div>
  );
}
