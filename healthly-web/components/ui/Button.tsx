import type { ButtonHTMLAttributes } from 'react';
import { Loader2 } from 'lucide-react';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost';

const VARIANTS: Record<Variant, string> = {
  primary: 'bg-[#72826c] text-white hover:bg-[#5e6d59]',
  secondary: 'bg-white text-[#242b26] border border-[#e1e7dc] hover:bg-[#ebefe6]',
  danger: 'bg-red-600 text-white hover:bg-red-700',
  ghost: 'text-[#6b756e] hover:bg-[#ebefe6] hover:text-[#242b26]',
};

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: 'sm' | 'md';
  isLoading?: boolean;
}

export default function Button({
  variant = 'primary',
  size = 'md',
  isLoading = false,
  disabled,
  className = '',
  children,
  type = 'button',
  ...props
}: ButtonProps) {
  const sizeClass = size === 'sm' ? 'px-3 py-1.5 text-xs' : 'px-4 py-2.5 text-sm';

  return (
    <button
      type={type}
      disabled={disabled || isLoading}
      className={`inline-flex items-center justify-center gap-2 rounded-full font-medium transition active:scale-95 disabled:opacity-50 disabled:pointer-events-none ${sizeClass} ${VARIANTS[variant]} ${className}`}
      {...props}
    >
      {isLoading && <Loader2 className="w-4 h-4 animate-spin" />}
      {children}
    </button>
  );
}
