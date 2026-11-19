'use client';

// Переключатель языка EN / TR (сегментированная «таблетка»). Выбор сохраняется в localStorage (lib/i18n.tsx)

import { LOCALES, useI18n } from '@/lib/i18n';

export default function LanguageSwitcher({ className = '' }: { className?: string }) {
  const { locale, setLocale, t } = useI18n();

  return (
    // className — для внешней обёртки (например, «hidden sm:block»), чтобы не спорить с inline-flex самой кнопки
    <div className={className}>
    <div role="group" aria-label={t.common.language} className="inline-flex items-center rounded-full border border-[#e3e6dd] bg-white/70 p-0.5 text-[11px] font-semibold">
      {LOCALES.map((code) => {
        const isActive = code === locale;
        return (
          <button
            key={code}
            type="button"
            lang={code}
            aria-pressed={isActive}
            onClick={() => setLocale(code)}
            className={`rounded-full px-2.5 py-1 uppercase tracking-wide transition ${
              isActive ? 'bg-[#2a302b] text-white' : 'text-[#6c736c] hover:text-[#2a302b]'
            }`}
          >
            {code}
          </button>
        );
      })}
    </div>
    </div>
  );
}
