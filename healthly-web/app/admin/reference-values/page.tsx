'use client';

// Справочник норм для врачей и админа — только чтение (GET /api/reference-values).
// Показывает, по каким границам сайт решает «норма / вне нормы / критично», для кого строка (пол, возраст),
// источник и статус проверки. Пока строка не проверена медсоветником (ReviewedBy пуст) — она «Draft».
// Править нормы — в HealthlyApi/Data/reference-values.csv (см. Data/REFERENCE_VALUES.md), не здесь.

import { useMemo, useState } from 'react';
import { AlertTriangle, BadgeCheck, Search } from 'lucide-react';
import AppShell from '@/components/layout/AppShell';
import Card, { EmptyState, LoadingState } from '@/components/ui/Card';
import Alert from '@/components/ui/Alert';
import { CONTROL_CLASS } from '@/components/ui/Field';
import { RangeScale } from '@/components/charts/primitives';
import { referenceValuesApi } from '@/lib/api';
import { useApiData } from '@/lib/useApiData';
import { useI18n } from '@/lib/i18n';
import type { ReferenceValue } from '@/types/api';

export default function ReferenceValuesPage() {
  return (
    <AppShell roles={['Doctor', 'Admin']}>
      <ReferenceValuesBrowser />
    </AppShell>
  );
}

type StatusFilter = 'all' | 'draft' | 'reviewed';

function ReferenceValuesBrowser() {
  const { t, intlLocale } = useI18n();
  const r = t.referenceValues;
  const { data: rows, error } = useApiData(referenceValuesApi.list);
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState('');
  const [status, setStatus] = useState<StatusFilter>('all');

  const categories = useMemo(() => [...new Set((rows ?? []).map((row) => row.category))].sort(), [rows]);

  const filtered = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase(intlLocale);
    return (rows ?? []).filter((row) => {
      if (category && row.category !== category) {
        return false;
      }
      if (status === 'draft' && row.isReviewed) {
        return false;
      }
      if (status === 'reviewed' && !row.isReviewed) {
        return false;
      }
      if (!needle) {
        return true;
      }
      return [row.metricName, ...row.aliases].some((name) => name.toLocaleLowerCase(intlLocale).includes(needle));
    });
  }, [rows, query, category, status, intlLocale]);

  // Группы по категориям в исходном порядке (API уже сортирует)
  const groups = useMemo(() => {
    const map = new Map<string, ReferenceValue[]>();
    filtered.forEach((row) => map.set(row.category, [...(map.get(row.category) ?? []), row]));
    return [...map.entries()];
  }, [filtered]);

  const reviewedCount = rows?.filter((row) => row.isReviewed).length ?? 0;
  const total = rows?.length ?? 0;
  const isAllReviewed = total > 0 && reviewedCount === total;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold sm:text-3xl">{r.title}</h1>
        <p className="mt-1 max-w-3xl text-sm text-[#6b756e]">{r.subtitle}</p>
      </div>

      {rows && (
        <div
          className={`flex items-start gap-3 rounded-2xl border px-4 py-3.5 text-sm ${
            isAllReviewed ? 'border-[#d5e0d1] bg-[#eef3eb] text-[#3d5340]' : 'border-[#eadfc8] bg-[#f8f2e6] text-[#6b5326]'
          }`}
          role="status"
        >
          {isAllReviewed ? <BadgeCheck className="mt-0.5 h-4 w-4 shrink-0" /> : <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />}
          <div>
            <div className="font-semibold">{isAllReviewed ? r.reviewedTitle : r.draftTitle}</div>
            <div className="mt-0.5 opacity-90">{isAllReviewed ? r.reviewedText(total) : r.draftText(reviewedCount, total)}</div>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_auto_auto]">
        <label className="relative">
          <span className="sr-only">{r.searchPlaceholder}</span>
          <Search className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[#9aa19a]" />
          <input type="search" value={query} onChange={(event) => setQuery(event.target.value)} placeholder={r.searchPlaceholder} className={`${CONTROL_CLASS} py-2.5! pl-10`} />
        </label>
        <select value={category} onChange={(event) => setCategory(event.target.value)} aria-label={r.allCategories} className={`${CONTROL_CLASS} py-2.5! sm:w-56`}>
          <option value="">{r.allCategories}</option>
          {categories.map((name) => <option key={name} value={name}>{name}</option>)}
        </select>
        <select value={status} onChange={(event) => setStatus(event.target.value as StatusFilter)} aria-label={r.status} className={`${CONTROL_CLASS} py-2.5! sm:w-44`}>
          <option value="all">{r.allStatuses}</option>
          <option value="draft">{r.onlyDrafts}</option>
          <option value="reviewed">{r.onlyReviewed}</option>
        </select>
      </div>

      {error && <Alert>{error}</Alert>}
      {!rows && !error && <LoadingState />}
      {rows && groups.length === 0 && (
        <Card>
          <EmptyState>{r.empty}</EmptyState>
        </Card>
      )}

      {groups.map(([name, items]) => (
        <Card key={name} title={name} actions={<span className="text-xs text-[#8e9890]">{r.rows(items.length)}</span>}>
          {/* Таблица — от планшета; на телефоне те же данные карточками (без горизонтальной прокрутки) */}
          <div className="-mx-5 hidden overflow-x-auto sm:-mx-6 md:block">
            <table className="w-full min-w-[860px] text-sm">
              <thead>
                <tr className="border-b border-[#e1e7dc] text-left text-xs text-[#6b756e]">
                  <th className="px-5 py-2 font-medium sm:px-6">{r.metric}</th>
                  <th className="px-3 py-2 font-medium">{r.who}</th>
                  <th className="px-3 py-2 font-medium">{r.normal}</th>
                  <th className="px-3 py-2 font-medium">{r.critical}</th>
                  <th className="px-3 py-2 font-medium">{r.source}</th>
                  <th className="px-5 py-2 font-medium sm:px-6">{r.status}</th>
                </tr>
              </thead>
              <tbody>
                {items.map((row) => (
                  <tr key={row.id} className="border-b border-[#e1e7dc] align-top last:border-0">
                    <td className="px-5 py-3 sm:px-6">
                      <div className="font-medium">{row.metricName}</div>
                      {row.aliases.length > 0 && <div className="mt-0.5 max-w-[220px] text-xs text-[#8e9890]">{r.aliases}: {row.aliases.join(', ')}</div>}
                      {row.doctorSpecialty && <div className="mt-0.5 text-xs text-[#8e9890]">{r.specialty}: {row.doctorSpecialty}</div>}
                    </td>
                    <td className="whitespace-nowrap px-3 py-3 text-[#4a524c]">
                      <WhoCell row={row} />
                    </td>
                    <td className="px-3 py-3">
                      <NormalCell row={row} />
                    </td>
                    <td className="whitespace-nowrap px-3 py-3 tabular-nums text-[#4a524c]">
                      <CriticalCell row={row} />
                    </td>
                    <td className="max-w-[260px] px-3 py-3 text-xs leading-relaxed text-[#6b756e]">
                      <span className="line-clamp-3" title={row.source}>{row.source || r.none}</span>
                    </td>
                    <td className="px-5 py-3 sm:px-6">
                      <StatusCell row={row} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <ul className="divide-y divide-[#e1e7dc] md:hidden">
            {items.map((row) => (
              <li key={row.id} className="space-y-2.5 py-4 first:pt-0 last:pb-0">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="font-medium">{row.metricName}</div>
                    <div className="text-xs text-[#6b756e]"><WhoCell row={row} /></div>
                  </div>
                  <StatusCell row={row} />
                </div>
                <NormalCell row={row} />
                <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-[#6b756e]">
                  <span>{r.critical}: <CriticalCell row={row} /></span>
                  {row.doctorSpecialty && <span>{r.specialty}: {row.doctorSpecialty}</span>}
                </div>
                {row.aliases.length > 0 && <div className="text-xs text-[#8e9890]">{r.aliases}: {row.aliases.join(', ')}</div>}
                {row.source && <p className="text-xs leading-relaxed text-[#8e9890]">{r.source}: {row.source}</p>}
              </li>
            ))}
          </ul>
        </Card>
      ))}
    </div>
  );
}

function useNumber() {
  const { intlLocale } = useI18n();
  return (value: number) => value.toLocaleString(intlLocale, { maximumFractionDigits: 3 });
}

function WhoCell({ row }: { row: ReferenceValue }) {
  const { t } = useI18n();
  const r = t.referenceValues;
  const age =
    row.ageFrom !== null && row.ageTo !== null ? r.ageRange(row.ageFrom, row.ageTo)
      : row.ageFrom !== null ? r.ageFrom(row.ageFrom)
        : row.ageTo !== null ? r.ageTo(row.ageTo)
          : r.anyAge;
  return <>{r.sexes[row.sex] ?? row.sex} · {age}</>;
}

function NormalCell({ row }: { row: ReferenceValue }) {
  const format = useNumber();
  const { t } = useI18n();
  const range =
    row.minNormal !== null && row.maxNormal !== null ? `${format(row.minNormal)} – ${format(row.maxNormal)}`
      : row.maxNormal !== null ? `< ${format(row.maxNormal)}`
        : row.minNormal !== null ? `> ${format(row.minNormal)}`
          : t.referenceValues.none;

  return (
    <div className="min-w-[150px] max-w-[240px]">
      <div className="tabular-nums">
        <span className="font-medium text-[#2a302b]">{range}</span> <span className="text-xs text-[#8e9890]">{row.unit}</span>
      </div>
      {/* Наглядно: где норма относительно критических порогов */}
      <RangeScale
        min={row.minNormal}
        max={row.maxNormal}
        criticalLow={row.criticalLow}
        criticalHigh={row.criticalHigh}
        className="mt-2"
        label={`${row.metricName}: ${range} ${row.unit}`}
      />
    </div>
  );
}

function CriticalCell({ row }: { row: ReferenceValue }) {
  const format = useNumber();
  const { t } = useI18n();
  const parts = [row.criticalLow !== null ? `< ${format(row.criticalLow)}` : null, row.criticalHigh !== null ? `> ${format(row.criticalHigh)}` : null].filter(Boolean);
  return <>{parts.length > 0 ? `${parts.join(' · ')} ${row.unit}` : t.referenceValues.none}</>;
}

function StatusCell({ row }: { row: ReferenceValue }) {
  const { t, intlLocale } = useI18n();
  const r = t.referenceValues;
  if (!row.isReviewed) {
    return <span className="inline-flex shrink-0 items-center rounded-full bg-[#f3ead8] px-2.5 py-0.5 text-xs font-medium text-[#7a5a22]">{r.draft}</span>;
  }
  const date = row.reviewedAt
    ? new Date(`${row.reviewedAt}T00:00:00Z`).toLocaleDateString(intlLocale, { year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC' })
    : '';
  return (
    <div className="shrink-0 text-right md:text-left">
      <span className="inline-flex items-center gap-1 rounded-full bg-[#dce8d8] px-2.5 py-0.5 text-xs font-medium text-[#3a5235]">
        <BadgeCheck className="h-3 w-3" />
        {r.reviewed}
      </span>
      <div className="mt-1 text-[11px] text-[#8e9890]">{r.reviewedBy(row.reviewedBy ?? '', date)}</div>
    </div>
  );
}
