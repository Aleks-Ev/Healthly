// Первый экран: заголовок, кнопки и арочная иллюстрация

import Link from 'next/link';
import { ArrowUpRight, ChevronDown, ShieldCheck, Sparkles } from 'lucide-react';
import type { SectionProps } from './theme';

export default function HeroSection({ theme }: SectionProps) {
  return (
    <section className="pt-12 pb-24 px-6 sm:px-12 max-w-7xl mx-auto">
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 lg:gap-8 items-center">

        {/* Left Hero Content */}
        <div className="lg:col-span-6 space-y-8">
          <div className="inline-flex items-center gap-2 text-xs font-semibold uppercase tracking-widest"
               style={{ color: theme.textSecondary }}>
            <span className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: theme.accentGreen }} />
            A HEALTHIER VERSION OF YOU.
          </div>

          <h1 className="text-5xl sm:text-6xl md:text-7xl font-serif-aesthetic leading-[1.08] tracking-tight"
              style={{ color: theme.textPrimary }}>
            Live Healthily <br />
            Ever After
          </h1>

          <p className="text-base sm:text-lg max-w-md font-sans-aesthetic font-light leading-relaxed"
             style={{ color: theme.textSecondary }}>
            Your health is a story worth understanding. Bring your data together, discover what it means, and take your next step with confidence.
          </p>

          {/* CTA Buttons */}
          <div className="flex flex-wrap items-center gap-4 pt-2">
            <Link
              href="/register"
              className="px-6 py-3.5 rounded-full text-sm font-medium flex items-center gap-2 text-white shadow-md transition-transform hover:scale-[1.02] active:scale-95"
              style={{ backgroundColor: theme.accentGreen }}
            >
              <span>Start Your Journey</span>
              <ArrowUpRight className="w-4 h-4" />
            </Link>

            <Link
              href="/dashboard"
              className="px-6 py-3.5 rounded-full text-sm font-medium flex items-center gap-2 shadow-sm transition-transform hover:scale-[1.02] active:scale-95"
              style={{ backgroundColor: theme.bgCard, color: theme.textPrimary }}
            >
              <span>Start Testing</span>
              <ArrowUpRight className="w-4 h-4" />
            </Link>
          </div>

          {/* Sub-tagline */}
          <div className="flex items-center gap-2 text-xs pt-4" style={{ color: theme.textSecondary }}>
            <ShieldCheck className="w-4 h-4" style={{ color: theme.accentGreen }} />
            <span>Your health is personal. We keep it that way.</span>
          </div>

          <div className="pt-12 text-[10px] font-semibold tracking-widest uppercase"
               style={{ color: theme.textSecondary }}>
            PERSONAL HEALTH, BEAUTIFULLY CONNECTED
          </div>
        </div>

        {/* Right Hero Image Container (Aesthetic Arched Graphic Card) */}
        <div className="lg:col-span-6 relative">
          <div className="relative w-full aspect-[4/5] sm:aspect-[1/1] max-w-md mx-auto lg:max-w-none rounded-[100px] sm:rounded-[140px] overflow-hidden shadow-2xl flex items-center justify-center bg-gradient-to-br from-[#dce8d8] via-[#e5ede2] to-[#cddbc8]">

            {/* Artistic SVG Graphic mimicking the Ginkgo / Botanical Leaf artwork from screenshot */}
            <svg className="w-3/4 h-3/4 text-[#72826c]/60 animate-pulse" viewBox="0 0 200 200" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M100 180 C 100 120, 140 80, 160 40 C 120 40, 80 60, 100 180 Z" fill="currentColor" opacity="0.3" />
              <path d="M100 180 C 100 110, 60 70, 40 30 C 80 30, 120 50, 100 180 Z" fill="currentColor" opacity="0.4" />
              <circle cx="100" cy="90" r="40" stroke="currentColor" strokeWidth="1" strokeDasharray="4 4" fill="none" />
              <circle cx="140" cy="50" r="6" fill="currentColor" opacity="0.5" />
              <circle cx="60" cy="70" r="4" fill="currentColor" opacity="0.3" />
            </svg>

            {/* Floating AI Glassmorphism Badge */}
            <div className="absolute bottom-8 left-6 right-6 sm:left-12 sm:right-12 p-5 rounded-3xl bg-white/70 backdrop-blur-md border border-white/60 shadow-xl text-center">
              <div className="inline-flex items-center gap-1.5 text-[10px] font-bold tracking-widest uppercase mb-1"
                   style={{ color: theme.textSecondary }}>
                <Sparkles className="w-3 h-3 text-emerald-700" />
                POWERED BY AI. CENTRED ON YOU.
              </div>
              <div className="text-lg sm:text-xl font-serif-aesthetic" style={{ color: theme.textPrimary }}>
                A little clarity. A better you.
              </div>
            </div>

            {/* Bottom Right Scroll Indicator */}
            <a href="#philosophy" className="absolute bottom-4 right-8 flex items-center gap-1 text-[11px] hover:underline" style={{ color: theme.textSecondary }}>
              <span>Explore a clearer kind of care</span>
              <ChevronDown className="w-3 h-3" />
            </a>
          </div>
        </div>

      </div>
    </section>
  );
}
