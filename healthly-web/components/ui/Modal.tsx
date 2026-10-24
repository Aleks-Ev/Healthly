'use client';

import { useEffect, type ReactNode } from 'react';
import { X } from 'lucide-react';

interface ModalProps {
  isOpen: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
  // Кнопки внизу окна (Cancel / Save)
  footer?: ReactNode;
}

export default function Modal({ isOpen, title, onClose, children, footer }: ModalProps) {
  // Закрытие по Escape
  useEffect(() => {
    if (!isOpen) {
      return;
    }
    const handleKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose();
      }
    };
    window.addEventListener('keydown', handleKey);
    return () => window.removeEventListener('keydown', handleKey);
  }, [isOpen, onClose]);

  if (!isOpen) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4 bg-black/40 backdrop-blur-sm" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="w-full max-w-lg max-h-[90vh] overflow-y-auto rounded-3xl bg-white shadow-2xl border border-[#e1e7dc]"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="flex items-center justify-between px-6 pt-5 pb-3">
          <h2 className="text-lg font-semibold text-[#242b26]">{title}</h2>
          <button onClick={onClose} aria-label="Close" className="p-1.5 rounded-full text-[#6b756e] hover:bg-[#ebefe6] transition">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="px-6 pb-4">{children}</div>
        {footer && <div className="flex justify-end gap-2 px-6 py-4 border-t border-[#e1e7dc]">{footer}</div>}
      </div>
    </div>
  );
}
