// Плавающая кнопка «Theme & Colors» и панель для подбора цветов лендинга в реальном времени

import { Palette, RotateCcw, Sliders, X } from 'lucide-react';
import type { LandingTheme, ThemeColor } from './theme';

// Группы цветов в панели: заголовок группы и поля (ключ палитры + подпись)
const COLOR_GROUPS: { title: string; className: string; colors: { key: ThemeColor; label: string }[] }[] = [
  {
    title: 'Backgrounds',
    className: 'mt-2',
    colors: [
      { key: 'bgPrimary', label: 'Page Background' },
      { key: 'bgCard', label: 'Card Background' },
      { key: 'bgSecondary', label: 'Secondary Tint' },
      { key: 'bgDark', label: 'Dark Section BG' },
    ],
  },
  {
    title: 'Accents & Buttons',
    className: 'mt-4',
    colors: [
      { key: 'accentGreen', label: 'Primary Accent Green' },
      { key: 'accentMint', label: 'Soft Mint Badge' },
    ],
  },
  {
    title: 'Typography & Borders',
    className: 'mt-4',
    colors: [
      { key: 'textPrimary', label: 'Primary Text' },
      { key: 'textSecondary', label: 'Muted Text' },
      { key: 'borderLight', label: 'Border Color' },
    ],
  },
];

interface ThemeCustomizerProps {
  theme: LandingTheme;
  isOpen: boolean;
  onToggle: () => void;
  onClose: () => void;
  onColorChange: (key: ThemeColor, value: string) => void;
  onReset: () => void;
}

export default function ThemeCustomizer({ theme, isOpen, onToggle, onClose, onColorChange, onReset }: ThemeCustomizerProps) {
  return (
    <div className="fixed bottom-6 right-6 z-50">
      <button
        onClick={onToggle}
        className="flex items-center gap-2 px-4 py-3 rounded-full shadow-2xl text-white font-medium text-sm transition-transform hover:scale-105 active:scale-95"
        style={{ backgroundColor: theme.accentGreen }}
      >
        <Palette className="w-4 h-4" />
        <span>Theme & Colors</span>
      </button>

      {isOpen && (
        <div className="absolute bottom-16 right-0 w-80 sm:w-96 p-6 bg-white/95 backdrop-blur-md rounded-3xl shadow-2xl border border-slate-200 text-slate-800 text-sm z-50 max-h-[80vh] overflow-y-auto">
          <div className="flex items-center justify-between pb-4 border-b border-slate-100">
            <div className="flex items-center gap-2 font-semibold text-slate-900">
              <Sliders className="w-4 h-4 text-emerald-600" />
              <span>Live Palette Customizer</span>
            </div>
            <div className="flex items-center gap-1">
              <button
                onClick={onReset}
                title="Reset to defaults"
                className="p-1.5 hover:bg-slate-100 rounded-full text-slate-500 transition-colors"
              >
                <RotateCcw className="w-3.5 h-3.5" />
              </button>
              <button
                onClick={onClose}
                className="p-1.5 hover:bg-slate-100 rounded-full text-slate-500 transition-colors"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          </div>

          <p className="text-xs text-slate-500 my-3">
            Adjust any color variable in real-time to match your vision perfectly.
          </p>

          <div className="space-y-3">
            {COLOR_GROUPS.map((group) => [
              <div key={group.title} className={`text-xs font-semibold uppercase tracking-wider text-slate-400 ${group.className}`}>
                {group.title}
              </div>,
              ...group.colors.map(({ key, label }) => (
                <div key={key} className="flex justify-between items-center">
                  <label className="text-xs font-medium text-slate-700">{label}</label>
                  <input
                    type="color"
                    value={theme[key]}
                    onChange={(e) => onColorChange(key, e.target.value)}
                    className="w-7 h-7 rounded-lg border cursor-pointer"
                  />
                </div>
              )),
            ])}
          </div>
        </div>
      )}
    </div>
  );
}
