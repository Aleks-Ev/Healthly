'use client';

// Лендинг. Каждая секция — отдельный компонент в components/landing; здесь только палитра,
// панель подбора цветов и порядок секций.

import { useState, useEffect } from 'react';
import Navbar from '@/components/landing/Navbar';
import ThemeCustomizer from '@/components/landing/ThemeCustomizer';
import HeroSection from '@/components/landing/HeroSection';
import PhilosophySection from '@/components/landing/PhilosophySection';
import FeaturesSection from '@/components/landing/FeaturesSection';
import HowItWorksSection from '@/components/landing/HowItWorksSection';
import TestingSection from '@/components/landing/TestingSection';
import ClosingSection from '@/components/landing/ClosingSection';
import { DEFAULT_THEME, type ThemeColor } from '@/components/landing/theme';

export default function HealthilyLanding() {
  // Color Customizer State
  const [theme, setTheme] = useState(DEFAULT_THEME);
  const [isThemePanelOpen, setIsThemePanelOpen] = useState(false);

  // Apply CSS Variables dynamically
  useEffect(() => {
    const root = document.documentElement;
    root.style.setProperty('--bg-primary', theme.bgPrimary);
    root.style.setProperty('--bg-card', theme.bgCard);
    root.style.setProperty('--bg-secondary', theme.bgSecondary);
    root.style.setProperty('--bg-dark', theme.bgDark);
    root.style.setProperty('--text-primary', theme.textPrimary);
    root.style.setProperty('--text-secondary', theme.textSecondary);
    root.style.setProperty('--accent-green', theme.accentGreen);
    root.style.setProperty('--accent-green-hover', theme.accentGreenHover);
    root.style.setProperty('--border-light', theme.borderLight);
  }, [theme]);

  const handleColorChange = (key: ThemeColor, value: string) => {
    setTheme((prev) => ({ ...prev, [key]: value }));
  };

  return (
    <div
      className="min-h-screen font-sans transition-colors duration-300 selection:bg-[#72826c]/20"
      style={{ backgroundColor: theme.bgPrimary, color: theme.textPrimary }}
    >
      {/* Google Serif Font Import */}
      <style jsx global>{`
        @import url('https://fonts.googleapis.com/css2?family=Newsreader:ital,opsz,wght@0,6..72,300..500;1,6..72,300..500&family=Plus+Jakarta+Sans:wght@300;400;500;600&display=swap');

        .font-serif-aesthetic {
          font-family: 'Newsreader', serif;
          font-weight: 300;
          letter-spacing: -0.02em;
        }

        .font-sans-aesthetic {
          font-family: 'Plus Jakarta Sans', sans-serif;
        }

        .smooth-scroll {
          scroll-behavior: smooth;
        }
      `}</style>

      {/* Floating Theme Customizer Button & Drawer */}
      <ThemeCustomizer
        theme={theme}
        isOpen={isThemePanelOpen}
        onToggle={() => setIsThemePanelOpen(!isThemePanelOpen)}
        onClose={() => setIsThemePanelOpen(false)}
        onColorChange={handleColorChange}
        onReset={() => setTheme(DEFAULT_THEME)}
      />

      {/* Floating Glassmorphism Navbar */}
      <Navbar />

      <HeroSection theme={theme} />
      <PhilosophySection theme={theme} />
      <FeaturesSection theme={theme} />
      <HowItWorksSection theme={theme} />
      <TestingSection theme={theme} />
      <ClosingSection theme={theme} />
    </div>
  );
}
