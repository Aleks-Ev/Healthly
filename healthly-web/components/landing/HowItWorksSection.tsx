// Секция «How it works»: три шага

import { FileText, Leaf, Sparkles, type LucideIcon } from 'lucide-react';
import type { SectionProps } from './theme';

const STEPS: { number: string; icon: LucideIcon; title: string; text: string }[] = [
  {
    number: '01',
    icon: FileText,
    title: 'Begin with what you know.',
    text: 'Create your private health space. Upload existing results, or start fresh with a test.',
  },
  {
    number: '02',
    icon: Sparkles,
    title: 'Find meaning in the details.',
    text: 'Explore clear explanations of your biomarkers, with AI that makes the complex feel simple.',
  },
  {
    number: '03',
    icon: Leaf,
    title: 'Grow your understanding.',
    text: 'Track changes over time and bring better questions to conversations with your clinician.',
  },
];

export default function HowItWorksSection({ theme }: SectionProps) {
  return (
    <section id="how-it-works" className="py-24 px-6 sm:px-12 max-w-7xl mx-auto space-y-16">

      <div className="flex flex-col md:flex-row md:items-end justify-between gap-6">
        <div className="space-y-3">
          <div className="text-xs font-semibold tracking-widest uppercase" style={{ color: theme.textSecondary }}>
            HOW IT WORKS
          </div>
          <h2 className="text-4xl sm:text-5xl font-serif-aesthetic" style={{ color: theme.textPrimary }}>
            A small beginning. A clearer <br />
            tomorrow.
          </h2>
        </div>
        <div className="italic font-serif-aesthetic text-base" style={{ color: theme.textSecondary }}>
          At your pace. Always.
        </div>
      </div>

      {/* 3 Step Process Grid */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-8 pt-8">
        {STEPS.map(({ number, icon: Icon, title, text }) => (
          <div key={number} className="space-y-6 relative">
            <div className="flex items-center justify-between pb-4 border-b" style={{ borderColor: theme.borderLight }}>
              <span className="font-serif-aesthetic text-2xl text-slate-400">{number}</span>
              <Icon className="w-5 h-5 text-slate-400" />
            </div>
            <h3 className="text-xl font-serif-aesthetic" style={{ color: theme.textPrimary }}>
              {title}
            </h3>
            <p className="text-xs sm:text-sm leading-relaxed" style={{ color: theme.textSecondary }}>
              {text}
            </p>
          </div>
        ))}
      </div>

    </section>
  );
}
