import type { ReactNode } from 'react';
import { Loader2 } from 'lucide-react';

interface CardProps {
  title?: string;
  // Кнопки справа от заголовка
  actions?: ReactNode;
  className?: string;
  children: ReactNode;
}

export default function Card({ title, actions, className = '', children }: CardProps) {
  return (
    <section className={`rounded-3xl bg-white border border-[#e1e7dc] p-5 sm:p-6 ${className}`}>
      {(title || actions) && (
        <div className="flex flex-wrap items-center justify-between gap-3 mb-4">
          {title && <h2 className="text-base font-semibold text-[#242b26]">{title}</h2>}
          {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
        </div>
      )}
      {children}
    </section>
  );
}

export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-12 text-sm text-[#6b756e]">
      <Loader2 className="w-4 h-4 animate-spin" />
      {label}
    </div>
  );
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <p className="py-8 text-center text-sm text-[#8e9890]">{children}</p>;
}
