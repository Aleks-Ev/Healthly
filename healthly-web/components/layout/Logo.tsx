import Link from 'next/link';

// Логотип из макета Figma: оливковый «плюс» в светлом скруглённом квадрате + название
export default function Logo({ size = 'md' }: { size?: 'md' | 'lg' }) {
  const isLarge = size === 'lg';
  return (
    <Link href="/" className="group flex shrink-0 items-center gap-2.5" aria-label="Healthly — home">
      <span
        className={`flex items-center justify-center rounded-xl bg-[#e8ece3] transition-transform group-hover:scale-105 ${isLarge ? 'h-10 w-10' : 'h-8 w-8'}`}
        aria-hidden
      >
        <svg viewBox="0 0 24 24" className={isLarge ? 'h-6 w-6' : 'h-5 w-5'}>
          <path d="M9.5 3.5h5v6h6v5h-6v6h-5v-6h-6v-5h6z" fill="#6f7f66" stroke="#6f7f66" strokeWidth="1.5" strokeLinejoin="round" />
        </svg>
      </span>
      <span className={`font-semibold tracking-tight text-[#2a302b] ${isLarge ? 'text-2xl' : 'text-lg'}`}>Healthly</span>
    </Link>
  );
}
