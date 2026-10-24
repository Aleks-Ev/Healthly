import Link from 'next/link';
import { HeartPulse } from 'lucide-react';

export default function Logo({ tone = 'dark' }: { tone?: 'dark' | 'light' }) {
  return (
    <Link href="/" className="flex items-center gap-2 group shrink-0">
      <HeartPulse className={`w-6 h-6 transition-transform group-hover:scale-110 ${tone === 'light' ? 'text-emerald-400' : 'text-[#72826c]'}`} />
      <span className={`text-lg font-semibold tracking-tight ${tone === 'light' ? 'text-white' : 'text-[#242b26]'}`}>Healthly</span>
    </Link>
  );
}
