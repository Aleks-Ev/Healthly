import type { ReactNode } from 'react';
import Logo from './Logo';

interface AuthLayoutProps {
  title: string;
  subtitle: string;
  wide?: boolean; // Широкая карточка для длинных форм (регистрация)
  children: ReactNode;
}

// Каркас страниц входа и регистрации: логотип + карточка по центру на кремовом фоне
export default function AuthLayout({ title, subtitle, wide = false, children }: AuthLayoutProps) {
  return (
    <div className="min-h-screen bg-[#f7f3ec] flex flex-col items-center justify-center px-4 py-12">
      <div className="mb-8">
        <Logo />
      </div>
      <div className={`w-full ${wide ? 'max-w-xl' : 'max-w-md'} rounded-3xl bg-[#fffdf9] border border-[#ebe4d6] shadow-xl shadow-[#8a7a5c]/5 p-6 sm:p-10`}>
        <h1 className="font-serif text-3xl tracking-tight text-[#242b26]">{title}</h1>
        <p className="mt-2 mb-8 text-sm text-[#6b756e]">{subtitle}</p>
        {children}
      </div>
    </div>
  );
}
