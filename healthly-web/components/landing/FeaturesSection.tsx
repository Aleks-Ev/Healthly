// Секция «Your health, with the dots connected»: три карточки возможностей с макетами интерфейса

import type { ReactNode } from 'react';
import { ArrowUpRight, CheckCircle2, FileText, Sparkles, TrendingUp } from 'lucide-react';
import type { LandingTheme, SectionProps } from './theme';

export default function FeaturesSection({ theme }: SectionProps) {
  return (
    <section className="py-24 px-6 sm:px-12 max-w-7xl mx-auto space-y-16">

      {/* Section Header */}
      <div className="flex flex-col md:flex-row md:items-end justify-between gap-6">
        <div className="space-y-3 max-w-2xl">
          <div className="text-xs font-semibold tracking-widest uppercase" style={{ color: theme.textSecondary }}>
            LESS COMPLEXITY. MORE CLARITY.
          </div>
          <h2 className="text-4xl sm:text-5xl font-serif-aesthetic" style={{ color: theme.textPrimary }}>
            Your health, with the dots connected.
          </h2>
        </div>
        <p className="text-xs sm:text-sm max-w-xs" style={{ color: theme.textSecondary }}>
          From scattered information to a more meaningful picture of your wellbeing.
        </p>
      </div>

      {/* 3 Interactive Cards Grid */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
        <FeatureCard
          theme={theme}
          title="Your Health. One Place."
          text="A personal home for your results, records and reflections. Less searching. More seeing the whole picture."
          linkText="Bring it all together"
          previewClassName="space-y-3"
        >
          <div className="flex justify-between items-center text-xs font-medium text-slate-400">
            <span>Your health library</span>
            <FileText className="w-3.5 h-3.5" />
          </div>
          <LibraryItem title="Blood test results" subtitle="12 Oct · 8 biomarkers" />
          <LibraryItem title="Wellbeing check-in" subtitle="Today · Personal notes" />
        </FeatureCard>

        <FeatureCard
          theme={theme}
          title="Understand Your Results."
          text="AI-powered explanations turn complex numbers into simple language, so you can ask better questions."
          linkText="Make sense of your data"
        >
          <div className="flex justify-between items-center text-xs font-medium text-slate-400">
            <span>Vitamin D</span>
            <Sparkles className="w-3.5 h-3.5 text-emerald-600" />
          </div>

          <div className="flex items-baseline justify-between">
            <div className="text-2xl font-serif-aesthetic font-semibold text-slate-800">
              42 <span className="text-xs font-normal text-slate-400">ng/mL</span>
            </div>
            <span className="px-2.5 py-1 rounded-full text-[10px] font-medium bg-emerald-100 text-emerald-800">
              Within range
            </span>
          </div>

          {/* Range bar */}
          <div className="w-full h-2 bg-slate-100 rounded-full overflow-hidden">
            <div className="h-full bg-emerald-600 w-3/4 rounded-full" />
          </div>

          <div className="text-[10px] text-slate-500">
            Supports healthy bones, immune function and everyday vitality.
          </div>
        </FeatureCard>

        <FeatureCard
          theme={theme}
          title="Track Your Wellbeing."
          text="Notice patterns, follow changes and connect how you feel with what your health data is telling you."
          linkText="See your story unfold"
        >
          <div className="flex justify-between items-center text-xs font-medium text-slate-400">
            <span>Your wellbeing over time</span>
            <TrendingUp className="w-3.5 h-3.5" />
          </div>

          <div className="text-sm font-serif-aesthetic font-medium text-slate-700">
            Finding your balance
          </div>

          {/* Sparkline chart SVG */}
          <div className="h-12 w-full pt-2">
            <svg className="w-full h-full text-emerald-600" viewBox="0 0 100 30" fill="none">
              <path d="M0 20 Q 20 5, 40 18 T 80 10 T 100 5" stroke="currentColor" strokeWidth="2" fill="none" strokeLinecap="round" />
            </svg>
          </div>

          <div className="flex justify-between text-[9px] text-slate-400 font-mono">
            {['JUN', 'JUL', 'AUG', 'SEP', 'OCT'].map((month) => (
              <span key={month}>{month}</span>
            ))}
          </div>
        </FeatureCard>
      </div>

      <div className="text-right text-[10px]" style={{ color: theme.textSecondary }}>
        Illustrative data, for a little perspective.
      </div>
    </section>
  );
}

interface FeatureCardProps {
  theme: LandingTheme;
  title: string;
  text: string;
  linkText: string;
  // Отступы внутри макета интерфейса (у первой карточки элементы плотнее)
  previewClassName?: string;
  // Макет интерфейса в верхней части карточки
  children: ReactNode;
}

function FeatureCard({ theme, title, text, linkText, previewClassName = 'space-y-4', children }: FeatureCardProps) {
  return (
    <div className="p-8 rounded-[36px] flex flex-col justify-between space-y-8 transition-transform hover:-translate-y-1 shadow-sm border"
         style={{ backgroundColor: theme.bgSecondary, borderColor: theme.borderLight }}>

      {/* Mock UI Preview */}
      <div className={`p-5 rounded-2xl bg-white/80 backdrop-blur shadow-sm ${previewClassName}`}>
        {children}
      </div>

      {/* Card Info */}
      <div className="space-y-4">
        <h3 className="text-2xl font-serif-aesthetic" style={{ color: theme.textPrimary }}>
          {title}
        </h3>
        <p className="text-xs sm:text-sm leading-relaxed" style={{ color: theme.textSecondary }}>
          {text}
        </p>
        <a href="#" className="inline-flex items-center gap-1.5 text-xs font-semibold pt-2 hover:underline" style={{ color: theme.textPrimary }}>
          <span>{linkText}</span>
          <ArrowUpRight className="w-3.5 h-3.5" />
        </a>
      </div>

    </div>
  );
}

function LibraryItem({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <div className="p-3 bg-slate-50 rounded-xl flex items-center justify-between text-xs">
      <div className="flex items-center gap-2.5">
        <FileText className="w-4 h-4 text-emerald-600" />
        <div>
          <div className="font-medium text-slate-800">{title}</div>
          <div className="text-[10px] text-slate-400">{subtitle}</div>
        </div>
      </div>
      <CheckCircle2 className="w-4 h-4 text-emerald-600" />
    </div>
  );
}
