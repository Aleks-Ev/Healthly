'use client';

// Колокольчик в шапке кабинета: число непрочитанных (проверяется раз в минуту и при возврате на вкладку),
// по клику — последние уведомления. Клик по уведомлению отмечает его прочитанным и открывает ссылку (обычно /care или /lab).
// Заголовок показывается на языке интерфейса (по типу уведомления), текст — из API (английский, для мобильного приложения тоже).

import { useCallback, useEffect, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Bell, CheckCheck } from 'lucide-react';
import { getErrorMessage, notificationsApi } from '@/lib/api';
import { useI18n } from '@/lib/i18n';
import { useApiData } from '@/lib/useApiData';
import type { AppNotification } from '@/types/api';

const POLL_INTERVAL_MS = 60_000;

export default function NotificationBell() {
  const { t, intlLocale } = useI18n();
  const n = t.notifications;
  const router = useRouter();
  const { data: unread, reload: reloadCount } = useApiData(notificationsApi.unreadCount);
  const [isOpen, setIsOpen] = useState(false);
  const [items, setItems] = useState<AppNotification[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const panelRef = useRef<HTMLDivElement>(null);

  // Опрос счётчика: таймер и возврат на вкладку (setState — в колбэках, не в теле эффекта)
  useEffect(() => {
    const timer = window.setInterval(reloadCount, POLL_INTERVAL_MS);
    const onFocus = () => reloadCount();
    window.addEventListener('focus', onFocus);
    return () => {
      window.clearInterval(timer);
      window.removeEventListener('focus', onFocus);
    };
  }, [reloadCount]);

  // Закрыть по клику мимо панели и по Escape
  useEffect(() => {
    if (!isOpen) {
      return;
    }
    const onPointer = (event: PointerEvent) => {
      if (panelRef.current && !panelRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsOpen(false);
      }
    };
    document.addEventListener('pointerdown', onPointer);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('pointerdown', onPointer);
      document.removeEventListener('keydown', onKey);
    };
  }, [isOpen]);

  const loadItems = useCallback(async () => {
    setError(null);
    try {
      setItems(await notificationsApi.list());
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }, []);

  const toggle = () => {
    const next = !isOpen;
    setIsOpen(next);
    if (next) {
      void loadItems();
      reloadCount();
    }
  };

  const open = async (item: AppNotification) => {
    setIsOpen(false);
    if (!item.readAt) {
      try {
        await notificationsApi.markRead(item.id);
      } catch {
        // Не отметилось — не страшно, откроем ссылку всё равно
      }
      reloadCount();
    }
    if (item.link) {
      router.push(item.link);
    }
  };

  const markAll = async () => {
    try {
      await notificationsApi.markAllRead();
      reloadCount();
      await loadItems();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  };

  const count = unread?.count ?? 0;
  const timeFormat = (iso: string) =>
    new Date(iso).toLocaleString(intlLocale, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });

  return (
    <div className="relative" ref={panelRef}>
      <button
        type="button"
        onClick={toggle}
        aria-label={count > 0 ? `${n.open} (${count})` : n.open}
        aria-expanded={isOpen}
        className="relative rounded-full p-2 text-[#6b756e] transition hover:bg-[#ebefe6] hover:text-[#242b26]"
      >
        <Bell className="h-5 w-5" />
        {count > 0 && (
          <span className="absolute right-0.5 top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-[#a4302a] px-1 text-[10px] font-semibold leading-none text-white">
            {count > 9 ? '9+' : count}
          </span>
        )}
      </button>

      {/* На телефоне панель — во всю ширину экрана под шапкой: колокольчик не у края, и она вылезла бы влево */}
      {isOpen && (
        <div className="fixed inset-x-4 top-16 z-50 overflow-hidden sm:absolute sm:inset-x-auto sm:right-0 sm:top-full sm:mt-2 sm:w-[22rem] rounded-3xl border border-[#e3e6dd] bg-white shadow-[0_24px_48px_-24px_rgba(42,48,43,0.35)]">
          <div className="flex items-center justify-between gap-3 border-b border-[#eef0e9] px-4 py-3">
            <span className="text-sm font-semibold text-[#2a302b]">{n.title}</span>
            {count > 0 && (
              <button type="button" onClick={markAll} className="inline-flex items-center gap-1 text-xs font-medium text-[#3d5340] hover:underline">
                <CheckCheck className="h-3.5 w-3.5" />
                {n.markAllRead}
              </button>
            )}
          </div>
          <ul className="max-h-[60vh] overflow-y-auto">
            {error && <li className="px-4 py-4 text-sm text-red-600">{error}</li>}
            {!items && !error && <li className="px-4 py-6 text-center text-sm text-[#8e9890]">…</li>}
            {items && items.length === 0 && <li className="px-4 py-8 text-center text-sm text-[#8e9890]">{n.empty}</li>}
            {items?.map((item) => (
              <li key={item.id}>
                <button
                  type="button"
                  onClick={() => open(item)}
                  className={`flex w-full items-start gap-3 px-4 py-3 text-left transition hover:bg-[#f6f7f2] ${item.readAt ? '' : 'bg-[#f3f6ef]'}`}
                >
                  <span className={`mt-1.5 h-2 w-2 shrink-0 rounded-full ${item.readAt ? 'bg-transparent' : 'bg-[#737f69]'}`} aria-hidden />
                  <span className="min-w-0">
                    <span className="block text-sm font-medium text-[#2a302b]">{n.types[item.type]?.(item.actorName) ?? item.title}</span>
                    <span className="mt-0.5 block text-xs leading-relaxed text-[#6b756e]">{item.body}</span>
                    <span className="mt-1 block text-[11px] text-[#9aa19a]">{timeFormat(item.createdAt)}</span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
