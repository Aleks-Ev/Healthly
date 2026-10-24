// Секция «The Healthily Philosophy»: главный тезис и три принципа

import { Leaf, ShieldCheck, Sparkles, type LucideIcon } from 'lucide-react';
import type { SectionProps } from './theme';

const PILLARS: { icon: LucideIcon; title: string; text: string }[] = [
  { icon: Sparkles, title: 'Intelligence, with intention.', text: 'AI to help you understand. Not to replace your clinician.' },
  { icon: ShieldCheck, title: 'Private, by nature.', text: 'Your data belongs to you. You decide what to share.' },
  { icon: Leaf, title: 'Made for your real life.', text: 'A calmer way to stay curious, informed and connected.' },
];

export default function PhilosophySection({ theme }: SectionProps) {
  return (
    <section id="philosophy" className="py-24 px-6 sm:px-12 transition-colors"
             style={{ backgroundColor: theme.bgSecondary }}>
      <div className="max-w-7xl mx-auto space-y-16">

        <div className="grid grid-cols-1 md:grid-cols-12 gap-8 items-start">
          {/* Left Label */}
          <div className="md:col-span-3 space-y-4">
            <div className="text-xs font-semibold tracking-widest uppercase" style={{ color: theme.textSecondary }}>
              THE HEALTHILY PHILOSOPHY
            </div>
            <Leaf className="w-6 h-6" style={{ color: theme.accentGreen }} />
          </div>

          {/* Right Big Statement */}
          <div className="md:col-span-9 space-y-6">
            <h2 className="text-3xl sm:text-5xl md:text-6xl font-serif-aesthetic leading-tight" style={{ color: theme.textPrimary }}>
              Science gives us information. <br />
              Understanding makes it personal.
            </h2>
            <p className="text-base sm:text-lg max-w-2xl font-light leading-relaxed" style={{ color: theme.textSecondary }}>
              We believe feeling better informed is the beginning of living better. Healthily brings thoughtful technology and a more human perspective to your health — giving you space to understand, not more to worry about.
            </p>
          </div>
        </div>

        {/* 3 Pillar Features */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8 pt-8 border-t" style={{ borderColor: theme.borderLight }}>
          {PILLARS.map(({ icon: Icon, title, text }) => (
            <div key={title} className="space-y-3">
              <div className="flex items-center gap-2 font-semibold text-sm" style={{ color: theme.textPrimary }}>
                <Icon className="w-4 h-4" style={{ color: theme.accentGreen }} />
                <span>{title}</span>
              </div>
              <p className="text-xs sm:text-sm leading-relaxed" style={{ color: theme.textSecondary }}>
                {text}
              </p>
            </div>
          ))}
        </div>

      </div>
    </section>
  );
}
