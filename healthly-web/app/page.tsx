'use client';

// Лендинг. Каждая секция — отдельный компонент в components/landing; здесь только палитра,
// панель подбора цветов, поиск по странице и порядок секций (как в макете Figma «Healthily home»).

import { useEffect, useState } from 'react';
import Navbar from '@/components/landing/Navbar';
import SearchDialog from '@/components/landing/SearchDialog';
import ThemeCustomizer from '@/components/landing/ThemeCustomizer';
import HeroSection from '@/components/landing/HeroSection';
import FeaturesSection from '@/components/landing/FeaturesSection';
import InsightsSection from '@/components/landing/InsightsSection';
import HowItWorksSection from '@/components/landing/HowItWorksSection';
import TestingSection from '@/components/landing/TestingSection';
import AnalysesSection from '@/components/landing/AnalysesSection';
import PhilosophySection from '@/components/landing/PhilosophySection';
import FaqSection from '@/components/landing/FaqSection';
import ClosingSection from '@/components/landing/ClosingSection';
import { DEFAULT_THEME, type ThemeColor } from '@/components/landing/theme';

export default function HealthlyLanding() {
  const [theme, setTheme] = useState(DEFAULT_THEME);
  const [isThemePanelOpen, setIsThemePanelOpen] = useState(false);
  const [isSearchOpen, setIsSearchOpen] = useState(false);

  // Поиск: «/» (если пользователь не печатает в поле) и Ctrl/⌘+K
  useEffect(() => {
    const handleKey = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      const isTyping = target?.closest('input, textarea, select, [contenteditable="true"]');
      if ((event.key.toLowerCase() === 'k' && (event.metaKey || event.ctrlKey)) || (event.key === '/' && !isTyping)) {
        event.preventDefault();
        setIsSearchOpen(true);
      }
    };
    window.addEventListener('keydown', handleKey);
    return () => window.removeEventListener('keydown', handleKey);
  }, []);

  const handleColorChange = (key: ThemeColor, value: string) => {
    setTheme((prev) => ({ ...prev, [key]: value }));
  };

  const openSearch = () => setIsSearchOpen(true);

  return (
    <div
      className="min-h-screen overflow-x-clip font-sans-aesthetic transition-colors duration-300 selection:bg-[#737f69]/20"
      style={{ backgroundColor: theme.bgPrimary, color: theme.textPrimary }}
    >
      <ThemeCustomizer
        theme={theme}
        isOpen={isThemePanelOpen}
        onToggle={() => setIsThemePanelOpen(!isThemePanelOpen)}
        onClose={() => setIsThemePanelOpen(false)}
        onColorChange={handleColorChange}
        onReset={() => setTheme(DEFAULT_THEME)}
      />

      <Navbar onOpenSearch={openSearch} />

      <main>
        <HeroSection theme={theme} />
        <FeaturesSection theme={theme} />
        <InsightsSection theme={theme} />
        <HowItWorksSection theme={theme} />
        <TestingSection theme={theme} />
        <AnalysesSection theme={theme} />
        <PhilosophySection theme={theme} />
        <FaqSection theme={theme} onSearch={openSearch} />
      </main>
      <ClosingSection theme={theme} />

      {isSearchOpen && <SearchDialog onClose={() => setIsSearchOpen(false)} />}
    </div>
  );
}
