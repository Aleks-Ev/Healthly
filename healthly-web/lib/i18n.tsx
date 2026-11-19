'use client';

// Язык интерфейса (EN / TR) без отдельной библиотеки.
//
// Почему не next-intl: почти все страницы — клиентские компоненты, а для next-intl пришлось бы перенести
// все маршруты в app/[locale]/ (и переписать ссылки, RequireAuth, редиректы). Здесь достаточно словаря:
//   locales/en.ts — эталон (тип Dictionary), locales/tr.ts — перевод с теми же ключами (проверяет TypeScript).
//
// Использование в компоненте:  const { t, locale, setLocale } = useI18n();  →  t.hero.primaryCta
// Выбор хранится в localStorage ('healthly.locale'); сервер и первый рендер всегда на английском
// (getServerSnapshot), после гидрации React сам перерисует страницу на выбранном языке — без ошибок гидрации.
// Сообщения API (ProblemDetails) пока приходят на английском — это отдельный этап (Accept-Language на бэкенде).

import { createContext, useContext, useEffect, useMemo, useSyncExternalStore, type ReactNode } from 'react';
import en, { type Dictionary } from '@/locales/en';
import tr from '@/locales/tr';

export const LOCALES = ['en', 'tr'] as const;
export type Locale = (typeof LOCALES)[number];

const DICTIONARIES: Record<Locale, Dictionary> = { en, tr };
const STORAGE_KEY = 'healthly.locale';
const DEFAULT_LOCALE: Locale = 'en';

// --- Внешнее хранилище (localStorage + подписчики), читается через useSyncExternalStore ---

const listeners = new Set<() => void>();

function isLocale(value: unknown): value is Locale {
  return typeof value === 'string' && (LOCALES as readonly string[]).includes(value);
}

function readLocale(): Locale {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    return isLocale(stored) ? stored : DEFAULT_LOCALE;
  } catch {
    // Приватный режим / заблокированное хранилище — просто английский
    return DEFAULT_LOCALE;
  }
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  // Смена языка в другой вкладке
  const onStorage = (event: StorageEvent) => {
    if (event.key === STORAGE_KEY) {
      listener();
    }
  };
  window.addEventListener('storage', onStorage);
  return () => {
    listeners.delete(listener);
    window.removeEventListener('storage', onStorage);
  };
}

function writeLocale(locale: Locale) {
  try {
    window.localStorage.setItem(STORAGE_KEY, locale);
  } catch {
    // Не сохранилось — язык всё равно переключится до перезагрузки страницы
  }
  current = locale;
  listeners.forEach((listener) => listener());
}

// Последний выбранный язык в памяти — на случай, если localStorage недоступен
let current: Locale | null = null;
const getSnapshot = () => current ?? readLocale();
const getServerSnapshot = () => DEFAULT_LOCALE;

// --- Контекст ---

interface I18nValue {
  locale: Locale;
  t: Dictionary;
  setLocale: (locale: Locale) => void;
  // Локаль для Intl (даты, числа)
  intlLocale: string;
}

const I18nContext = createContext<I18nValue | null>(null);

export function I18nProvider({ children }: { children: ReactNode }) {
  const locale = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);

  // <html lang> — для экранных дикторов и правильных переносов (в турецком своя i/İ)
  useEffect(() => {
    document.documentElement.lang = locale;
  }, [locale]);

  const value = useMemo<I18nValue>(
    () => ({ locale, t: DICTIONARIES[locale], setLocale: writeLocale, intlLocale: locale === 'tr' ? 'tr-TR' : 'en-US' }),
    [locale],
  );

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nValue {
  const value = useContext(I18nContext);
  if (!value) {
    throw new Error('useI18n must be used inside <I18nProvider>');
  }
  return value;
}
