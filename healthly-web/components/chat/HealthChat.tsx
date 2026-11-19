'use client';

// ИИ-чат «Healthly AI» (страница /chat).
// - история с сервера (GET /api/chat/messages), новое сообщение показывается сразу (оптимистично) — при ошибке
//   убирается, текст возвращается в поле, показывается причина (429 «слишком часто», 503 «ИИ недоступен»);
// - пока ждём ответ — пузырь «Healthly AI is thinking…»;
// - под каждым ответом ассистента — дисклеймер мелким шрифтом (на языке интерфейса);
// - Enter — отправить, Shift+Enter — новая строка; «Clear chat» — удалить историю (с подтверждением).
// Текст ответа выводится как простая разметка (абзацы, списки, **жирный**) — без HTML, поэтому безопасно.

import { Fragment, useEffect, useRef, useState, type FormEvent, type KeyboardEvent, type ReactNode } from 'react';
import { ArrowUp, Loader2, Siren, Sparkles, Trash2 } from 'lucide-react';
import Alert from '@/components/ui/Alert';
import ConfirmDialog from '@/components/ui/ConfirmDialog';
import { LoadingState } from '@/components/ui/Card';
import { chatApi, getErrorMessage } from '@/lib/api';
import { useI18n } from '@/lib/i18n';
import { useApiData } from '@/lib/useApiData';
import type { ChatMessage } from '@/types/api';

const MAX_LENGTH = 2000;

export default function HealthChat() {
  const { t, intlLocale } = useI18n();
  const c = t.chat;
  const { data: history, error: loadError, setData } = useApiData(chatApi.history);
  const [draft, setDraft] = useState('');
  const [pending, setPending] = useState<ChatMessage | null>(null); // отправленное, но ещё без ответа
  const [error, setError] = useState<string | null>(null);
  const [isMock, setIsMock] = useState(false);
  const [isClearing, setIsClearing] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);

  const messages = history?.messages ?? [];
  const isSending = pending !== null;

  // Прокрутка к последнему сообщению (меняем только DOM, не состояние)
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' });
  }, [messages.length, pending]);

  const send = async (text: string) => {
    const content = text.trim();
    if (!content || isSending || !history) {
      return;
    }
    setError(null);
    setDraft('');
    setPending({ id: -1, role: 'user', content, createdAt: new Date().toISOString() });
    try {
      const reply = await chatApi.send(content);
      setData({ ...history, messages: [...history.messages, reply.userMessage, reply.assistantMessage] });
      setIsMock(reply.isMock);
    } catch (err) {
      setError(getErrorMessage(err));
      setDraft(content); // ничего не сохранилось — вернуть текст, чтобы отправить ещё раз
    } finally {
      setPending(null);
      inputRef.current?.focus();
    }
  };

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    void send(draft);
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault();
      void send(draft);
    }
  };

  const timeFormat = (iso: string) => new Date(iso).toLocaleTimeString(intlLocale, { hour: '2-digit', minute: '2-digit' });

  return (
    <div className="flex h-[calc(100dvh-8.5rem)] min-h-[480px] flex-col overflow-hidden rounded-[28px] border border-[#e3e6dd] bg-white shadow-[0_1px_2px_rgba(42,48,43,0.04),0_24px_48px_-32px_rgba(42,48,43,0.25)] sm:h-[calc(100dvh-9.5rem)]">
      {/* Шапка чата */}
      <header className="flex items-center justify-between gap-3 border-b border-[#eef0e9] px-4 py-3 sm:px-6 sm:py-4">
        <div className="flex min-w-0 items-center gap-3">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[#e4ece5] text-[#3d5340]">
            <Sparkles className="h-5 w-5" strokeWidth={1.75} />
          </span>
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <h1 className="text-base font-semibold text-[#2a302b]">{c.title}</h1>
              {isMock && (
                <span title={c.demoModeHint} className="rounded-full bg-[#f3ead8] px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-[#7a5a22]">
                  {c.demoMode}
                </span>
              )}
            </div>
            <p className="truncate text-xs text-[#6b756e]">{c.subtitle}</p>
          </div>
        </div>
        {messages.length > 0 && (
          <button
            type="button"
            onClick={() => setIsClearing(true)}
            disabled={isSending}
            className="inline-flex shrink-0 items-center gap-1.5 rounded-full px-3 py-2 text-xs font-medium text-[#6b756e] transition hover:bg-[#f3f4ef] hover:text-[#2a302b] disabled:opacity-50"
          >
            <Trash2 className="h-3.5 w-3.5" />
            <span className="hidden sm:inline">{c.clear}</span>
          </button>
        )}
      </header>

      {/* Сообщения */}
      {/* relative + min-h-0: прокручивается только лента; иначе скрытые подписи (sr-only, absolute) растягивали страницу */}
      <div className="relative min-h-0 flex-1 overflow-y-auto bg-[#fbfbf8] px-3 py-5 sm:px-6" aria-live="polite">
        {loadError && <Alert>{loadError}</Alert>}
        {!history && !loadError && <LoadingState />}

        {history && messages.length === 0 && !pending && (
          <div className="mx-auto flex max-w-xl flex-col items-center py-6 text-center sm:py-10">
            <span className="flex h-14 w-14 items-center justify-center rounded-full bg-[#e4ece5] text-[#3d5340]">
              <Sparkles className="h-6 w-6" strokeWidth={1.5} />
            </span>
            <h2 className="mt-4 font-serif-aesthetic text-[30px] leading-tight text-[#2a302b]">{c.welcomeTitle}</h2>
            <p className="mt-2 text-sm leading-relaxed text-[#6b756e]">{c.welcomeText}</p>
            <div className="mt-6 grid w-full grid-cols-1 gap-2 sm:grid-cols-2">
              {c.suggestions.map((suggestion) => (
                <button
                  key={suggestion}
                  type="button"
                  onClick={() => void send(suggestion)}
                  className="rounded-2xl border border-[#e3e6dd] bg-white px-4 py-3 text-left text-sm text-[#2f3530] transition hover:border-[#cfd6c9] hover:bg-[#f6f8f3]"
                >
                  {suggestion}
                </button>
              ))}
            </div>
            <p className="mt-6 inline-flex items-start gap-1.5 text-xs text-[#8a4a23]">
              <Siren className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              {c.emergency}
            </p>
          </div>
        )}

        <ol className="mx-auto max-w-3xl space-y-5">
          {messages.map((message) => (
            <li key={message.id}>
              {message.role === 'user' ? (
                <UserBubble content={message.content} time={timeFormat(message.createdAt)} label={c.you} />
              ) : (
                <AssistantBubble label={c.assistant} time={timeFormat(message.createdAt)} disclaimer={c.disclaimer}>
                  <MessageText text={message.content} />
                </AssistantBubble>
              )}
            </li>
          ))}
          {pending && (
            <>
              <li><UserBubble content={pending.content} time={timeFormat(pending.createdAt)} label={c.you} /></li>
              <li>
                <AssistantBubble label={c.assistant}>
                  <span className="inline-flex items-center gap-2 text-sm text-[#6b756e]">
                    <TypingDots />
                    {c.thinking}
                  </span>
                </AssistantBubble>
              </li>
            </>
          )}
        </ol>
        <div ref={bottomRef} />
      </div>

      {/* Поле ввода */}
      <form onSubmit={handleSubmit} className="border-t border-[#eef0e9] bg-white px-3 pb-3 pt-3 sm:px-6 sm:pb-4">
        {error && <Alert className="mb-3">{error}</Alert>}
        <div className="flex items-end gap-2 rounded-[22px] border border-[#e3e6dc] bg-[#fdfdf9] p-1.5 pl-4 transition focus-within:border-[#8d9a84] focus-within:bg-white focus-within:ring-4 focus-within:ring-[#737f69]/10">
          <label htmlFor="chat-input" className="sr-only">{c.placeholder}</label>
          <textarea
            id="chat-input"
            ref={inputRef}
            rows={1}
            maxLength={MAX_LENGTH}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={c.placeholder}
            disabled={!history}
            className="max-h-40 min-h-[40px] flex-1 resize-none bg-transparent py-2.5 text-[15px] text-[#2a302b] placeholder:text-[#a3aaa3] outline-none [field-sizing:content]"
          />
          <button
            type="submit"
            disabled={!draft.trim() || isSending || !history}
            aria-label={c.send}
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[#737f69] text-white transition hover:bg-[#5f6b57] active:scale-95 disabled:pointer-events-none disabled:opacity-40"
          >
            {isSending ? <Loader2 className="h-4 w-4 animate-spin" /> : <ArrowUp className="h-4 w-4" />}
          </button>
        </div>
        <div className="mt-1.5 flex justify-between gap-3 px-2 text-[11px] text-[#9aa19a]">
          <span className="hidden sm:inline">{c.hint}</span>
          {draft.length > MAX_LENGTH - 200 && <span className="ml-auto">{draft.length}/{MAX_LENGTH} · {c.tooLong(MAX_LENGTH)}</span>}
        </div>
      </form>

      <ConfirmDialog
        isOpen={isClearing}
        title={c.clearTitle}
        message={c.clearText}
        confirmLabel={c.clear}
        onClose={() => setIsClearing(false)}
        onConfirm={async () => {
          await chatApi.clear();
          if (history) {
            setData({ ...history, messages: [] });
          }
          setIsMock(false);
        }}
      />
    </div>
  );
}

function UserBubble({ content, time, label }: { content: string; time: string; label: string }) {
  return (
    <div className="flex justify-end">
      <div className="max-w-[85%] sm:max-w-[75%]">
        <div className="whitespace-pre-wrap break-words rounded-[20px] rounded-br-md bg-[#465a4a] px-4 py-2.5 text-[15px] leading-relaxed text-white">
          {content}
        </div>
        <div className="mt-1 text-right text-[11px] text-[#9aa19a]">
          <span className="sr-only">{label}, </span>
          {time}
        </div>
      </div>
    </div>
  );
}

function AssistantBubble({ label, time, disclaimer, children }: { label: string; time?: string; disclaimer?: string; children: ReactNode }) {
  return (
    <div className="flex items-start gap-2.5">
      <span className="mt-1 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-[#e4ece5] text-[#3d5340]" aria-hidden>
        <Sparkles className="h-4 w-4" strokeWidth={1.75} />
      </span>
      <div className="min-w-0 max-w-[88%] sm:max-w-[80%]">
        <div className="rounded-[20px] rounded-tl-md border border-[#e8ebe3] bg-white px-4 py-3 text-[15px] leading-relaxed text-[#2f3530] shadow-[0_1px_2px_rgba(42,48,43,0.04)]">
          <span className="sr-only">{label}: </span>
          {children}
        </div>
        {(disclaimer || time) && (
          <p className="mt-1.5 px-1 text-[11px] leading-snug text-[#9aa19a]">
            {time && <span>{time}</span>}
            {time && disclaimer && <span aria-hidden> · </span>}
            {disclaimer}
          </p>
        )}
      </div>
    </div>
  );
}

function TypingDots() {
  return (
    <span className="inline-flex gap-1" aria-hidden>
      {[0, 150, 300].map((delay) => (
        <span key={delay} className="h-1.5 w-1.5 animate-bounce rounded-full bg-[#9fb09a]" style={{ animationDelay: `${delay}ms` }} />
      ))}
    </span>
  );
}

// Мини-разметка ответа: абзацы, списки «- » / «• » / «1. », заголовки «### », **жирный**. Без HTML — только React-узлы
function MessageText({ text }: { text: string }) {
  const blocks: ReactNode[] = [];
  // start — номер первого пункта: модель часто пишет «1.», «2.» между абзацами, нумерация не должна сбрасываться
  let list: { ordered: boolean; start: number; items: string[] } | null = null;

  const flushList = () => {
    if (list) {
      const items = list.items.map((item, index) => <li key={index}>{inline(item)}</li>);
      blocks.push(list.ordered
        ? <ol key={blocks.length} start={list.start} className="my-1.5 list-decimal space-y-1 pl-5">{items}</ol>
        : <ul key={blocks.length} className="my-1.5 list-disc space-y-1 pl-5 marker:text-[#9fb09a]">{items}</ul>);
      list = null;
    }
  };

  for (const rawLine of text.replace(/\r\n/g, '\n').split('\n')) {
    let line = rawLine.trim();
    // Таблицу модель всё же прислала (промпт просит без них): строка-разделитель пропускается, ячейки — через « — »
    if (/^\|.*\|$/.test(line)) {
      if (/^[|\s:-]+$/.test(line)) {
        continue;
      }
      line = line.slice(1, -1).split('|').map((cell) => cell.trim()).filter(Boolean).join(' — ');
    }
    // Горизонтальная черта «---» — просто разрыв между блоками
    if (/^([-*_])\1{2,}$/.test(line)) {
      flushList();
      continue;
    }
    const bullet = /^[-*•]\s+(.*)$/.exec(line);
    const numbered = /^(\d+)[.)]\s+(.*)$/.exec(line);
    if (bullet || numbered) {
      const ordered = Boolean(numbered);
      if (!list || list.ordered !== ordered) {
        flushList();
        list = { ordered, start: numbered ? Number(numbered[1]) : 1, items: [] };
      }
      list.items.push(bullet ? bullet[1] : numbered![2]);
      continue;
    }
    flushList();
    if (!line) {
      continue;
    }
    const heading = /^#{1,4}\s+(.*)$/.exec(line);
    blocks.push(heading
      ? <p key={blocks.length} className="mt-2 font-semibold first:mt-0">{inline(heading[1])}</p>
      : <p key={blocks.length} className="mt-2 first:mt-0">{inline(line)}</p>);
  }
  flushList();
  return <>{blocks}</>;
}

// **жирный** → <strong>, *курсив* → <em>; остальное — обычный текст. Markdown-экранирование (\<, \*) убираем
function inline(text: string): ReactNode {
  const parts = text.replace(/\\([\\`*_{}[\]()#+\-.!<>|])/g, '$1').split(/(\*\*[^*]+\*\*|\*[^*\s][^*]*\*)/g);
  return parts.map((part, index) => {
    if (part.startsWith('**') && part.endsWith('**') && part.length > 4) {
      return <strong key={index} className="font-semibold text-[#2a302b]">{part.slice(2, -2)}</strong>;
    }
    if (part.startsWith('*') && part.endsWith('*') && part.length > 2) {
      return <em key={index}>{part.slice(1, -1)}</em>;
    }
    return <Fragment key={index}>{part}</Fragment>;
  });
}
