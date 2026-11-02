import type { ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';
import type { DocumentStatus, Role } from '@/types/api';

type Tone = 'neutral' | 'green' | 'blue' | 'amber' | 'red' | 'purple';

const TONES: Record<Tone, string> = {
  neutral: 'bg-[#ebefe6] text-[#6b756e]',
  green: 'bg-[#dce8d8] text-[#3a5235]',
  blue: 'bg-sky-100 text-sky-800',
  amber: 'bg-amber-100 text-amber-800',
  red: 'bg-red-100 text-red-700',
  purple: 'bg-violet-100 text-violet-800',
};

export default function Badge({ tone = 'neutral', children }: { tone?: Tone; children: ReactNode }) {
  return <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${TONES[tone]}`}>{children}</span>;
}

const ROLE_TONES: Record<Role, Tone> = { Patient: 'green', Doctor: 'blue', Admin: 'purple', LabStaff: 'amber' };
const ROLE_LABELS: Record<Role, string> = { Patient: 'Patient', Doctor: 'Doctor', Admin: 'Admin', LabStaff: 'Lab staff' };

export function RoleBadge({ role }: { role: Role }) {
  return <Badge tone={ROLE_TONES[role]}>{ROLE_LABELS[role]}</Badge>;
}

const STATUS_TONES: Record<DocumentStatus, Tone> = { Pending: 'neutral', Processing: 'amber', Completed: 'green', Error: 'red' };

export function StatusBadge({ status }: { status: DocumentStatus }) {
  return <Badge tone={STATUS_TONES[status]}>{status}</Badge>;
}

// Критическое значение (за критическим порогом справочника норм) — заливка ярко-красным, заметнее обычного отклонения
export function CriticalBadge() {
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-red-600 px-2.5 py-0.5 text-xs font-semibold text-white">
      <AlertTriangle className="w-3 h-3" aria-hidden="true" />
      Critical
    </span>
  );
}

export function AbnormalBadge({ isAbnormal, isCritical = false }: { isAbnormal: boolean; isCritical?: boolean }) {
  if (isCritical) {
    return <CriticalBadge />;
  }
  return isAbnormal ? <Badge tone="red">Out of range</Badge> : <Badge tone="green">Normal</Badge>;
}
