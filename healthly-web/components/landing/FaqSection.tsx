'use client';

// Секция #faq: вопросы-ответы на нативных <details> (работают без JS, доступны с клавиатуры).
// У вопроса id `faq-<id>`: ссылка /#faq-privacy или выбор в поиске раскрывает нужный ответ.

import { useEffect } from 'react';
import { Plus, Search } from 'lucide-react';
import { useI18n } from '@/lib/i18n';
import { revealTarget } from './SearchDialog';
import type { SectionProps } from './theme';

export default function FaqSection({ theme, onSearch }: SectionProps & { onSearch?: () => void }) {
  const { t } = useI18n();
  const f = t.faq;

  // Открыли страницу по ссылке на конкретный вопрос — раскрыть его (меняем DOM, не состояние React)
  useEffect(() => {
    const openFromHash = () => {
      const id = decodeURIComponent(window.location.hash.slice(1));
      if (id.startsWith('faq-')) {
        revealTarget(id);
      }
    };
    openFromHash();
    window.addEventListener('hashchange', openFromHash);
    return () => window.removeEventListener('hashchange', openFromHash);
  }, []);

  return (
    <section id="faq" className="mx-auto max-w-7xl px-4 py-16 sm:px-8 sm:py-24 lg:px-12">
      <div className="grid grid-cols-1 gap-10 lg:grid-cols-12 lg:gap-16">
        <div className="space-y-4 lg:col-span-5">
          <div className="text-[11px] font-semibold uppercase tracking-[0.18em]" style={{ color: theme.textSecondary }}>{f.eyebrow}</div>
          <h2 className="font-serif-aesthetic text-[40px] leading-[1.05] sm:text-6xl" style={{ color: theme.textPrimary }}>{f.title}</h2>
          <p className="max-w-sm text-sm leading-relaxed" style={{ color: theme.textSecondary }}>{f.text}</p>
          {onSearch && (
            <button
              type="button"
              onClick={onSearch}
              className="mt-2 inline-flex items-center gap-2 rounded-full bg-white px-4 py-2.5 text-sm shadow-[0_1px_2px_rgba(42,48,43,0.06),0_6px_16px_-8px_rgba(42,48,43,0.15)] transition hover:bg-[#fbfbf8]"
              style={{ color: theme.textPrimary }}
            >
              <Search className="h-4 w-4" style={{ color: theme.textSecondary }} />
              {t.search.title}
            </button>
          )}
        </div>

        <div className="divide-y border-y lg:col-span-7" style={{ borderColor: theme.borderLight }}>
          {f.items.map((item) => (
            <details key={item.id} id={`faq-${item.id}`} className="group" style={{ borderColor: theme.borderLight }}>
              <summary className="flex cursor-pointer list-none items-center justify-between gap-6 py-5 text-left outline-none focus-visible:ring-2 focus-visible:ring-[#737f69]/30 [&::-webkit-details-marker]:hidden">
                <span className="font-serif-aesthetic text-[22px] leading-snug sm:text-2xl" style={{ color: theme.textPrimary }}>{item.q}</span>
                <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-white transition-transform duration-300 group-open:rotate-45">
                  <Plus className="h-4 w-4" style={{ color: theme.textSecondary }} />
                </span>
              </summary>
              <p className="max-w-2xl pb-6 pr-12 text-sm leading-relaxed" style={{ color: theme.textSecondary }}>{item.a}</p>
            </details>
          ))}
        </div>
      </div>
    </section>
  );
}
