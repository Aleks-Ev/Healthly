import type { ReactNode } from 'react';
import { AlertCircle, CheckCircle2 } from 'lucide-react';

interface AlertProps {
  variant?: 'error' | 'success';
  className?: string;
  children: ReactNode;
}

export default function Alert({ variant = 'error', className = '', children }: AlertProps) {
  const isError = variant === 'error';
  const Icon = isError ? AlertCircle : CheckCircle2;

  return (
    <div
      role={isError ? 'alert' : 'status'}
      className={`flex items-start gap-2 rounded-2xl px-4 py-3 text-sm ${isError ? 'bg-red-50 text-red-700' : 'bg-[#dce8d8] text-[#3a5235]'} ${className}`}
    >
      <Icon className="w-4 h-4 mt-0.5 shrink-0" />
      <div>{children}</div>
    </div>
  );
}
