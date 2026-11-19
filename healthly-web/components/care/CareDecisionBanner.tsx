'use client';

// Плашка в кабинете пациента: врач или лаборатория приняли запрос и ждут, поделится ли пациент данными.
// Показывается, только если такие решения есть; ведёт на /care, где кнопки «Share my records» / «Not now».

import Link from 'next/link';
import { ArrowRight, ShieldCheck } from 'lucide-react';
import { careApi } from '@/lib/api';
import { useI18n } from '@/lib/i18n';
import { useApiData } from '@/lib/useApiData';

export default function CareDecisionBanner() {
  const { t } = useI18n();
  const { data: connections } = useApiData(careApi.connections);
  const waiting = (connections ?? []).filter((item) => item.status === 'Accepted');

  if (waiting.length === 0) {
    return null;
  }

  return (
    <Link
      href="/care"
      className="mb-6 flex items-center gap-3 rounded-3xl border border-[#eadfc8] bg-[#fbf6ec] px-5 py-4 transition hover:bg-[#f8f0e2]"
    >
      <ShieldCheck className="h-5 w-5 shrink-0 text-[#7a5a22]" />
      <span className="min-w-0 flex-1">
        <span className="block text-[11px] font-semibold uppercase tracking-[0.14em] text-[#7a5a22]">{t.care.actionNeeded}</span>
        <span className="block text-sm text-[#2a302b]">{waiting.map((item) => t.care.acceptedTitle(item.provider.name)).join(' · ')}</span>
      </span>
      <ArrowRight className="h-4 w-4 shrink-0 text-[#7a5a22]" />
    </Link>
  );
}
