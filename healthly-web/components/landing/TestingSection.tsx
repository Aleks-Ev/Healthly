// Тёмная секция «Thoughtful testing»: категории анализов

import { ArrowUpRight, FlaskConical } from 'lucide-react';
import type { SectionProps } from './theme';

const TEST_CATEGORIES = [
  { name: 'Everyday essentials', description: 'Vitamins & key nutrients' },
  { name: 'Your inner balance', description: 'Metabolic & hormonal health' },
  { name: 'The bigger picture', description: 'Heart health & wellbeing' },
];

export default function TestingSection({ theme }: SectionProps) {
  return (
    <section id="testing" className="py-24 px-6 sm:px-12 transition-colors" style={{ backgroundColor: theme.bgDark }}>
      <div className="max-w-7xl mx-auto grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">

        {/* Left Arched Image Container */}
        <div className="lg:col-span-5 relative">
          <div className="relative w-full aspect-[4/5] rounded-t-[140px] rounded-b-[40px] overflow-hidden bg-gradient-to-t from-stone-900 to-stone-800 flex items-center justify-center">

            {/* Bottle / Botanical Artwork Mock */}
            <div className="flex flex-col items-center gap-4 opacity-80">
              <div className="w-16 h-36 bg-gradient-to-b from-emerald-100/40 to-emerald-300/20 rounded-full border border-white/20 backdrop-blur-md" />
              <FlaskConical className="w-8 h-8 text-emerald-200" />
            </div>

            {/* Floating Badge */}
            <div className="absolute bottom-6 left-6 right-6 p-4 rounded-2xl bg-white/10 backdrop-blur-md border border-white/10 text-white text-xs">
              <div className="font-medium">A deeper look at your everyday health.</div>
              <div className="text-[10px] text-stone-400 mt-0.5">Thoughtful testing. Meaningful understanding.</div>
            </div>
          </div>
        </div>

        {/* Right Content */}
        <div className="lg:col-span-7 space-y-8 text-white">
          <div className="text-xs font-semibold tracking-widest uppercase text-stone-400">
            KNOWLEDGE STARTS WITHIN
          </div>

          <h2 className="text-4xl sm:text-5xl md:text-6xl font-serif-aesthetic leading-tight">
            A little insight. <br />
            A world of possibility.
          </h2>

          <p className="text-sm sm:text-base text-stone-300 font-light max-w-xl leading-relaxed">
            Go beyond how you feel. Explore key biomarkers that offer a deeper view of your health, with results that become part of your personal story.
          </p>

          {/* Test Category Rows */}
          <div className="space-y-4 pt-4 border-t border-stone-800">
            {TEST_CATEGORIES.map(({ name, description }) => (
              <div key={name} className="flex justify-between items-center py-3 border-b border-stone-800 text-sm">
                <span className="font-medium text-stone-200">{name}</span>
                <span className="text-xs text-stone-400">{description}</span>
              </div>
            ))}
          </div>

          <div className="pt-4 flex flex-col sm:flex-row items-start sm:items-center gap-4">
            <button className="px-6 py-3.5 rounded-full text-sm font-medium flex items-center gap-2 text-white transition-transform hover:scale-105"
                    style={{ backgroundColor: theme.accentGreen }}>
              <span>Start Testing</span>
              <ArrowUpRight className="w-4 h-4" />
            </button>
            <span className="text-xs text-stone-400">Already have results? Bring them with you. Your journey starts there, too.</span>
          </div>

        </div>

      </div>
    </section>
  );
}
