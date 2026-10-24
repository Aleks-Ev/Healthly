import type { ReactNode } from 'react';
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

const ROLE_TONES: Record<Role, Tone> = { Patient: 'green', Doctor: 'blue', Admin: 'purple' };

export function RoleBadge({ role }: { role: Role }) {
  return <Badge tone={ROLE_TONES[role]}>{role}</Badge>;
}

const STATUS_TONES: Record<DocumentStatus, Tone> = { Pending: 'neutral', Processing: 'amber', Completed: 'green', Error: 'red' };

export function StatusBadge({ status }: { status: DocumentStatus }) {
  return <Badge tone={STATUS_TONES[status]}>{status}</Badge>;
}

export function AbnormalBadge({ isAbnormal }: { isAbnormal: boolean }) {
  return isAbnormal ? <Badge tone="red">Out of range</Badge> : <Badge tone="green">Normal</Badge>;
}
