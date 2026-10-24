// Финальный призыв «More understanding. More you.» и подвал сайта (дисклеймер, ссылки, копирайт)

import { ArrowUpRight, Plus, ShieldCheck } from 'lucide-react';
import type { SectionProps } from './theme';

const FOOTER_LINKS = [
  { href: '#how-it-works', label: 'How It Works' },
  { href: '#testing', label: 'Testing' },
  { href: '#philosophy', label: 'About' },
];

export default function ClosingSection({ theme }: SectionProps) {
  return (
    <section className="py-24 px-6 sm:px-12 max-w-7xl mx-auto space-y-20">

      {/* Banner Card */}
      <div className="p-12 sm:p-20 rounded-[48px] text-center space-y-8 relative overflow-hidden shadow-sm"
           style={{ backgroundColor: theme.bgSecondary }}>

        {/* Subtle background circles */}
        <div className="absolute top-0 right-0 w-96 h-96 rounded-full border border-emerald-900/5 -mr-20 -mt-20 pointer-events-none" />
        <div className="absolute bottom-0 left-0 w-80 h-80 rounded-full border border-emerald-900/5 -ml-20 -mb-20 pointer-events-none" />

        <div className="text-xs font-semibold tracking-widest uppercase" style={{ color: theme.textSecondary }}>
          YOUR NEXT CHAPTER STARTS HERE
        </div>

        <h2 className="text-4xl sm:text-6xl font-serif-aesthetic" style={{ color: theme.textPrimary }}>
          More understanding. More you.
        </h2>

        <p className="text-sm sm:text-base max-w-lg mx-auto font-light leading-relaxed" style={{ color: theme.textSecondary }}>
          A calmer, clearer relationship with your health is waiting. Take the first step, and let the bigger picture unfold.
        </p>

        <div className="pt-2">
          <button className="px-8 py-4 rounded-full text-sm font-medium inline-flex items-center gap-2 text-white shadow-md transition-transform hover:scale-105"
                  style={{ backgroundColor: theme.accentGreen }}>
            <span>Start Your Journey</span>
            <ArrowUpRight className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Footer */}
      <footer className="pt-8 border-t space-y-12" style={{ borderColor: theme.borderLight }}>

        <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-8">
          {/* Footer Logo */}
          <a href="#" className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-full flex items-center justify-center text-white" style={{ backgroundColor: theme.accentGreen }}>
              <Plus className="w-5 h-5 stroke-[2.5]" />
            </div>
            <span className="text-xl font-semibold tracking-tight font-sans-aesthetic" style={{ color: theme.textPrimary }}>
              Healthily
            </span>
          </a>

          {/* Footer Navigation */}
          <div className="flex flex-wrap items-center gap-8 text-xs font-medium" style={{ color: theme.textSecondary }}>
            {FOOTER_LINKS.map(({ href, label }) => (
              <a key={href} href={href} className="hover:underline">{label}</a>
            ))}
            <a href="#" className="flex items-center gap-1 hover:underline">
              <span>Contact us</span>
              <ArrowUpRight className="w-3 h-3" />
            </a>
          </div>
        </div>

        {/* Medical Disclaimer */}
        <p className="text-[11px] leading-relaxed max-w-3xl" style={{ color: theme.textSecondary }}>
          Healthily helps you organise and understand your health information. It does not provide a medical diagnosis or replace advice from a qualified healthcare professional. Always discuss health concerns and test results with your clinician.
        </p>

        {/* Copyright & Legal Links */}
        <div className="flex flex-col sm:flex-row justify-between items-center gap-4 text-[11px]" style={{ color: theme.textSecondary }}>
          <div>© 2026 Healthily</div>

          <div className="flex items-center gap-1.5">
            <ShieldCheck className="w-3.5 h-3.5 text-emerald-700" />
            <span>Your health is personal. We keep it that way.</span>
          </div>

          <div className="flex items-center gap-6">
            <a href="#" className="hover:underline">Privacy</a>
            <a href="#" className="hover:underline">Terms</a>
          </div>
        </div>

      </footer>

    </section>
  );
}
