'use client';

// Поиск по лендингу (иконка лупы в шапке, а также клавиши «/» и Ctrl/⌘+K).
// Ищет по разделам, названиям анализов (#analyses) и вопросам FAQ (#faq) — на текущем языке,
// а названия анализов ещё и по-английски (в бланках часто латиница: «Cholesterol», «ALT»).
// Выбор результата прокручивает к нужному блоку; вопрос FAQ при этом раскрывается.
// Компонент монтируется только при открытии, поэтому строка поиска каждый раз начинается пустой.

import { useMemo, useState, type KeyboardEvent } from 'react';
import { CornerDownLeft, FlaskConical, HelpCircle, LayoutGrid, Search, X, type LucideIcon } from 'lucide-react';
import { useI18n } from '@/lib/i18n';
import en from '@/locales/en';

type Group = 'sections' | 'tests' | 'faq';

interface SearchEntry {
  key: string;
  group: Group;
  label: string;
  hint?: string;
  // id элемента на странице, к которому прокручиваем
  target: string;
  keywords: string;
}

const GROUP_ICONS: Record<Group, LucideIcon> = { sections: LayoutGrid, tests: FlaskConical, faq: HelpCircle };

// Поиск без учёта регистра и диакритики: «Şeker» найдётся по «seker», «İ» → «i»
function normalize(value: string, locale: string): string {
  return value
    .toLocaleLowerCase(locale)
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/ı/g, 'i');
}

// Прокрутка к блоку; у <details> (вопрос FAQ) — сначала раскрыть
export function revealTarget(target: string) {
  const element = document.getElementById(target);
  if (!element) {
    return;
  }
  if (element instanceof HTMLDetailsElement) {
    element.open = true;
  }
  element.scrollIntoView({ behavior: 'smooth', block: 'start' });
  history.replaceState(null, '', `#${target}`);
}

export default function SearchDialog({ onClose }: { onClose: () => void }) {
  const { t, intlLocale } = useI18n();
  const [query, setQuery] = useState('');
  const [activeIndex, setActiveIndex] = useState(0);

  const entries = useMemo<SearchEntry[]>(() => {
    const sections: SearchEntry[] = [
      { key: 's-how', group: 'sections', label: t.nav.howItWorks, target: 'how-it-works', keywords: t.howItWorks.title },
      { key: 's-insights', group: 'sections', label: t.insights.eyebrow, target: 'insights', keywords: t.insights.title },
      { key: 's-tests', group: 'sections', label: t.nav.labTests, target: 'analyses', keywords: t.analyses.title },
      { key: 's-testing', group: 'sections', label: t.nav.testing, target: 'testing', keywords: t.testing.titleLine2 },
      { key: 's-about', group: 'sections', label: t.nav.about, target: 'philosophy', keywords: t.philosophy.titleLine1 },
      { key: 's-faq', group: 'sections', label: t.nav.faq, target: 'faq', keywords: t.faq.title },
    ];

    const tests: SearchEntry[] = t.analyses.groups.flatMap((group, groupIndex) =>
      group.tests.map((test, testIndex) => ({
        key: `t-${group.id}-${testIndex}`,
        group: 'tests' as const,
        label: test,
        hint: group.name,
        target: `analyses-${group.id}`,
        keywords: `${group.name} ${en.analyses.groups[groupIndex]?.tests[testIndex] ?? ''} ${en.analyses.groups[groupIndex]?.name ?? ''}`,
      })),
    );

    const faq: SearchEntry[] = t.faq.items.map((item) => ({
      key: `f-${item.id}`,
      group: 'faq',
      label: item.q,
      target: `faq-${item.id}`,
      keywords: item.a,
    }));

    return [...sections, ...tests, ...faq];
  }, [t]);

  const results = useMemo(() => {
    const words = normalize(query.trim(), intlLocale).split(/\s+/).filter(Boolean);
    if (words.length === 0) {
      // Пустой запрос — подсказка: разделы и вопросы
      return entries.filter((entry) => entry.group !== 'tests');
    }
    return entries.filter((entry) => {
      const haystack = normalize(`${entry.label} ${entry.hint ?? ''} ${entry.keywords}`, intlLocale);
      return words.every((word) => haystack.includes(word));
    });
  }, [entries, query, intlLocale]);

  const groupTitles: Record<Group, string> = { sections: t.search.groupSections, tests: t.search.groupTests, faq: t.search.groupFaq };
  const safeIndex = Math.min(activeIndex, Math.max(results.length - 1, 0));

  const select = (entry: SearchEntry) => {
    onClose();
    // Даём диалогу закрыться (вернуть прокрутку странице), затем едем к блоку
    requestAnimationFrame(() => revealTarget(entry.target));
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      setActiveIndex((safeIndex + 1) % Math.max(results.length, 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActiveIndex((safeIndex - 1 + results.length) % Math.max(results.length, 1));
    } else if (event.key === 'Enter' && results[safeIndex]) {
      event.preventDefault();
      select(results[safeIndex]);
    }
  };

  return (
    <div className="fixed inset-0 z-[60] flex items-start justify-center bg-[#2a302b]/25 px-4 pt-[12vh] backdrop-blur-sm" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-label={t.search.title}
        onKeyDown={handleKeyDown}
        onClick={(event) => event.stopPropagation()}
        className="w-full max-w-xl overflow-hidden rounded-3xl border border-[#e3e6dd] bg-[#fbfbf8] shadow-[0_24px_60px_-20px_rgba(42,48,43,0.35)] font-sans-aesthetic"
      >
        <div className="flex items-center gap-3 border-b border-[#e3e6dd] px-5 py-4">
          <Search className="h-5 w-5 shrink-0 text-[#9aa19a]" aria-hidden />
          <input
            autoFocus
            type="search"
            value={query}
            onChange={(event) => {
              setQuery(event.target.value);
              setActiveIndex(0);
            }}
            placeholder={t.search.placeholder}
            aria-label={t.search.title}
            aria-controls="landing-search-results"
            aria-activedescendant={results[safeIndex] ? `search-${results[safeIndex].key}` : undefined}
            className="min-w-0 flex-1 bg-transparent text-base text-[#2a302b] placeholder:text-[#9aa19a] outline-none [&::-webkit-search-cancel-button]:hidden"
          />
          <button type="button" onClick={onClose} aria-label={t.common.close} className="rounded-full p-1.5 text-[#6c736c] transition hover:bg-[#eef0e9]">
            <X className="h-4 w-4" />
          </button>
        </div>

        <ul id="landing-search-results" role="listbox" className="max-h-[50vh] overflow-y-auto p-2">
          {results.length === 0 && <li className="px-4 py-8 text-center text-sm text-[#6c736c]">{t.search.empty}</li>}
          {results.map((entry, index) => {
            const Icon = GROUP_ICONS[entry.group];
            const isFirstOfGroup = index === 0 || results[index - 1].group !== entry.group;
            const isActive = index === safeIndex;
            return (
              <li key={entry.key} role="presentation">
                {isFirstOfGroup && (
                  <div className="px-3 pb-1 pt-3 text-[10px] font-semibold uppercase tracking-[0.14em] text-[#9aa19a]">{groupTitles[entry.group]}</div>
                )}
                <div
                  id={`search-${entry.key}`}
                  role="option"
                  aria-selected={isActive}
                  onMouseEnter={() => setActiveIndex(index)}
                  onClick={() => select(entry)}
                  className={`flex cursor-pointer items-center gap-3 rounded-2xl px-3 py-2.5 text-sm transition ${isActive ? 'bg-[#e4ece5] text-[#2a302b]' : 'text-[#465049]'}`}
                >
                  <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-white text-[#737f69] shadow-[0_1px_2px_rgba(42,48,43,0.06)]">
                    <Icon className="h-4 w-4" aria-hidden />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate">{entry.label}</span>
                    {entry.hint && <span className="block truncate text-xs text-[#9aa19a]">{entry.hint}</span>}
                  </span>
                  {isActive && <CornerDownLeft className="h-3.5 w-3.5 shrink-0 text-[#9aa19a]" aria-hidden />}
                </div>
              </li>
            );
          })}
        </ul>

        <div className="border-t border-[#e3e6dd] px-5 py-2.5 text-[11px] text-[#9aa19a]">{t.search.hint}</div>
      </div>
    </div>
  );
}
