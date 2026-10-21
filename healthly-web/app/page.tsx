'use client';

import React, { useState, useEffect } from 'react';
import { 
  Plus, 
  ArrowUpRight, 
  Sparkles, 
  ShieldCheck, 
  Leaf, 
  FileText, 
  TrendingUp, 
  FlaskConical, 
  Palette, 
  RotateCcw,
  Sliders,
  CheckCircle2,
  Lock,
  ChevronDown,
  X
} from 'lucide-react';

// Default Color Palette matching the exact aesthetic from the screenshots
const DEFAULT_THEME = {
  bgPrimary: '#f6f7f2',      // Soft warm sage cream background
  bgCard: '#ffffff',         // Clean white for cards
  bgSecondary: '#ebefe6',    // Slightly deeper sage tint
  bgDark: '#080c0a',         // Sophisticated dark section
  textPrimary: '#242b26',    // Dark forest text
  textSecondary: '#6b756e',  // Muted sage text
  textLight: '#ffffff',      // Pure white text
  textMutedDark: '#8e9890',  // Muted text for dark backgrounds
  accentGreen: '#72826c',    // Primary muted olive green
  accentGreenHover: '#5e6d59',// Darker olive green hover
  accentMint: '#dce8d8',     // Light mint badge
  accentMintText: '#3a5235', // Dark green text on mint badge
  borderLight: '#e1e7dc',    // Delicate border color
  borderDark: '#232c25',     // Dark section subtle borders
};

export default function HealthilyLanding() {
  // Color Customizer State
  const [theme, setTheme] = useState(DEFAULT_THEME);
  const [isThemePanelOpen, setIsThemePanelOpen] = useState(false);
  const [activeTab, setActiveTab] = useState<'main' | 'users'>('main');

  // API State (keeping connection to .NET backend)
  interface User {
    id: number;
    email: string;
    fullName: string;
    role: string;
    height?: number; // Добавили рост
    weight?: number; // Добавили вес
    gender?: string; // Добавили пол
    createdAt: string;
  }
  const [users, setUsers] = useState<User[]>([]);
  const [loadingUsers, setLoadingUsers] = useState(false);
  const [usersError, setUsersError] = useState<string | null>(null);

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

  // Fetch Users from .NET API
  const fetchUsers = () => {
    setLoadingUsers(true);
    setUsersError(null);
    fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5097/api'}/users`)
      .then((res) => {
        if (!res.ok) throw new Error('Не удалось получить данные от .NET API');
        return res.json();
      })
      .then((data) => {
        setUsers(data);
        setLoadingUsers(false);
      })
      .catch((err) => {
        setUsersError(err.message);
        setLoadingUsers(false);
      });
  };

  useEffect(() => {
  const fetchUsers = async () => {
    setLoadingUsers(true);
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/users`);
      if (!res.ok) throw new Error('Не удалось получить данные');
      const data = await res.json();
      setUsers(data);
    } catch (err: unknown) {
      if (err instanceof Error) {
      setUsersError(err.message);
      } else {
        setUsersError('Произошла неизвестная ошибка');
      }
    } finally {
      setLoadingUsers(false);
    }
  };

  fetchUsers();
}, []);

  const handleColorChange = (key: keyof typeof DEFAULT_THEME, value: string) => {
    setTheme((prev) => ({ ...prev, [key]: value }));
  };

  const resetTheme = () => {
    setTheme(DEFAULT_THEME);
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
      <div className="fixed bottom-6 right-6 z-50">
        <button
          onClick={() => setIsThemePanelOpen(!isThemePanelOpen)}
          className="flex items-center gap-2 px-4 py-3 rounded-full shadow-2xl text-white font-medium text-sm transition-transform hover:scale-105 active:scale-95"
          style={{ backgroundColor: theme.accentGreen }}
        >
          <Palette className="w-4 h-4" />
          <span>Theme & Colors</span>
        </button>

        {isThemePanelOpen && (
          <div className="absolute bottom-16 right-0 w-80 sm:w-96 p-6 bg-white/95 backdrop-blur-md rounded-3xl shadow-2xl border border-slate-200 text-slate-800 text-sm z-50 max-h-[80vh] overflow-y-auto">
            <div className="flex items-center justify-between pb-4 border-b border-slate-100">
              <div className="flex items-center gap-2 font-semibold text-slate-900">
                <Sliders className="w-4 h-4 text-emerald-600" />
                <span>Live Palette Customizer</span>
              </div>
              <div className="flex items-center gap-1">
                <button 
                  onClick={resetTheme}
                  title="Reset to defaults"
                  className="p-1.5 hover:bg-slate-100 rounded-full text-slate-500 transition-colors"
                >
                  <RotateCcw className="w-3.5 h-3.5" />
                </button>
                <button 
                  onClick={() => setIsThemePanelOpen(false)}
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
              <div className="text-xs font-semibold uppercase tracking-wider text-slate-400 mt-2">Backgrounds</div>
              
              <div className="flex justify-between items-center">
                <label className="text-xs font-medium text-slate-700">Page Background</label>
                <input 
                  type="color" 
                  value={theme.bgPrimary} 
                  onChange={(e) => handleColorChange('bgPrimary', e.target.value)} 
                  className="w-7 h-7 rounded-lg border cursor-pointer" 
                />
              </div>

              <div className="flex justify-between items-center">
                <label className="text-xs font-medium text-slate-700">Card Background</label>
                <input 
                  type="color" 
                  value={theme.bgCard} 
                  onChange={(e) => handleColorChange('bgCard', e.target.value)} 
                  className="w-7 h-7 rounded-lg border cursor-pointer" 
                />
              </div>

              <div className="flex justify-between items-center">
                <label className="text-xs font-medium text-slate-700">Secondary Tint</label>
                <input 
                  type="color" 
                  value={theme.bgSecondary} 
                  onChange={(e) => handleColorChange('bgSecondary', e.target.value)} 
                  className="w-7 h-7 rounded-lg border cursor-pointer" 
                />
              </div>

              <div className="flex justify-between items-center">
                <label className="text-xs font-medium text-slate-700">Dark Section BG</label>
                <input 
                  type="color" 
                  value={theme.bgDark} 
                  onChange={(e) => handleColorChange('bgDark', e.target.value)} 
                  className="w-7 h-7 rounded-lg border cursor-pointer" 
                />
              </div>

              <div className="text-xs font-semibold uppercase tracking-wider text-slate-400 mt-4">Accents & Buttons</div>

              <div className="flex justify-between items-center">
                <label className="text-xs font-medium text-slate-700">Primary Accent Green</label>
                <input 
                  type="color" 
                  value={theme.accentGreen} 
                  onChange={(e) => handleColorChange('accentGreen', e.target.value)} 
                  className="w-7 h-7 rounded-lg border cursor-pointer" 
                />
              </div>

              <div className="flex justify-between items-center">
                <label className="text-xs font-medium text-slate-700">Soft Mint Badge</label>
                <input 
                  type="color" 
                  value={theme.accentMint} 
                  onChange={(e) => handleColorChange('accentMint', e.target.value)} 
                  className="w-7 h-7 rounded-lg border cursor-pointer" 
                />
              </div>

              <div className="text-xs font-semibold uppercase tracking-wider text-slate-400 mt-4">Typography & Borders</div>

              <div className="flex justify-between items-center">
                <label className="text-xs font-medium text-slate-700">Primary Text</label>
                <input 
                  type="color" 
                  value={theme.textPrimary} 
                  onChange={(e) => handleColorChange('textPrimary', e.target.value)} 
                  className="w-7 h-7 rounded-lg border cursor-pointer" 
                />
              </div>

              <div className="flex justify-between items-center">
                <label className="text-xs font-medium text-slate-700">Muted Text</label>
                <input 
                  type="color" 
                  value={theme.textSecondary} 
                  onChange={(e) => handleColorChange('textSecondary', e.target.value)} 
                  className="w-7 h-7 rounded-lg border cursor-pointer" 
                />
              </div>

              <div className="flex justify-between items-center">
                <label className="text-xs font-medium text-slate-700">Border Color</label>
                <input 
                  type="color" 
                  value={theme.borderLight} 
                  onChange={(e) => handleColorChange('borderLight', e.target.value)} 
                  className="w-7 h-7 rounded-lg border cursor-pointer" 
                />
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Navigation Header */}
      <header className="sticky top-0 z-40 backdrop-blur-md transition-colors border-b border-transparent"
              style={{ backgroundColor: `${theme.bgPrimary}cc` }}>
        <div className="max-w-7xl mx-auto px-6 sm:px-12 h-20 flex items-center justify-between">
          {/* Logo */}
          <a href="#" className="flex items-center gap-2.5 group">
            <div className="w-8 h-8 rounded-full flex items-center justify-center transition-transform group-hover:scale-105"
                 style={{ backgroundColor: theme.accentGreen, color: theme.textLight }}>
              <Plus className="w-5 h-5 stroke-[2.5]" />
            </div>
            <span className="text-xl font-semibold tracking-tight font-sans-aesthetic" style={{ color: theme.textPrimary }}>
              Healthily
            </span>
          </a>

          {/* Navigation Links */}
          <nav className="hidden md:flex items-center gap-8 text-sm font-medium">
            <a href="#how-it-works" className="transition-colors hover:opacity-80" style={{ color: theme.textSecondary }}>
              How It Works
            </a>
            <a href="#testing" className="transition-colors hover:opacity-80" style={{ color: theme.textSecondary }}>
              Testing
            </a>
            <a href="#philosophy" className="transition-colors hover:opacity-80" style={{ color: theme.textSecondary }}>
              About
            </a>
            <button 
              onClick={() => setActiveTab(activeTab === 'main' ? 'users' : 'main')}
              className="text-xs px-3 py-1.5 rounded-full border border-slate-300 font-mono transition-colors"
              style={{ color: theme.textSecondary }}
            >
              {activeTab === 'main' ? 'DB Status: Connected' : 'View Homepage'}
            </button>
          </nav>

          {/* Action Button */}
          <button 
            className="px-5 py-2.5 rounded-full text-xs sm:text-sm font-medium shadow-sm transition-all hover:opacity-90 active:scale-95"
            style={{ backgroundColor: theme.bgCard, color: theme.textPrimary }}
          >
            Sign In
          </button>
        </div>
      </header>

      {/* Database Quick Test Drawer View (Optional View) */}
      {activeTab === 'users' && (
        <div className="max-w-4xl mx-auto my-8 p-6 bg-white rounded-3xl border border-slate-200 shadow-lg">
          <div className="flex justify-between items-center mb-6">
            <h3 className="text-lg font-bold">PostgreSQL Users via .NET 10 API</h3>
            <button onClick={fetchUsers} className="text-xs px-3 py-1 bg-slate-100 rounded-md">Refresh</button>
          </div>
          {loadingUsers && <p className="text-slate-500">Loading DB records...</p>}
          {usersError && <p className="text-red-500">Error: {usersError}</p>}
          {!loadingUsers && users.length === 0 && <p className="text-slate-500">No users found. Insert data via pgAdmin!</p>}
          <div className="space-y-3">
            {users.map((u) => (
              <div key={u.id} className="p-3 bg-slate-50 rounded-xl flex justify-between items-center">
                <div>
                  <div className="font-semibold text-sm">{u.fullName} ({u.email})</div>
                  <div className="text-xs text-slate-500">Role: {u.role} | Height: {u.height || '—'} cm | Weight: {u.weight || '—'} kg</div>
                </div>
                <span className="text-xs px-2 py-1 bg-emerald-100 text-emerald-800 rounded font-medium">{u.gender || 'Patient'}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* HERO SECTION */}
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
              <button 
                className="px-6 py-3.5 rounded-full text-sm font-medium flex items-center gap-2 text-white shadow-md transition-transform hover:scale-[1.02] active:scale-95"
                style={{ backgroundColor: theme.accentGreen }}
              >
                <span>Start Your Journey</span>
                <ArrowUpRight className="w-4 h-4" />
              </button>

              <button 
                className="px-6 py-3.5 rounded-full text-sm font-medium flex items-center gap-2 transition-transform hover:scale-[1.02] active:scale-95 shadow-sm"
                style={{ backgroundColor: theme.bgCard, color: theme.textPrimary }}
              >
                <span>Start Testing</span>
                <ArrowUpRight className="w-4 h-4" />
              </button>
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


      {/* THE HEALTHILY PHILOSOPHY SECTION */}
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
            
            {/* Feature 1 */}
            <div className="space-y-3">
              <div className="flex items-center gap-2 font-semibold text-sm" style={{ color: theme.textPrimary }}>
                <Sparkles className="w-4 h-4" style={{ color: theme.accentGreen }} />
                <span>Intelligence, with intention.</span>
              </div>
              <p className="text-xs sm:text-sm leading-relaxed" style={{ color: theme.textSecondary }}>
                AI to help you understand. Not to replace your clinician.
              </p>
            </div>

            {/* Feature 2 */}
            <div className="space-y-3">
              <div className="flex items-center gap-2 font-semibold text-sm" style={{ color: theme.textPrimary }}>
                <ShieldCheck className="w-4 h-4" style={{ color: theme.accentGreen }} />
                <span>Private, by nature.</span>
              </div>
              <p className="text-xs sm:text-sm leading-relaxed" style={{ color: theme.textSecondary }}>
                Your data belongs to you. You decide what to share.
              </p>
            </div>

            {/* Feature 3 */}
            <div className="space-y-3">
              <div className="flex items-center gap-2 font-semibold text-sm" style={{ color: theme.textPrimary }}>
                <Leaf className="w-4 h-4" style={{ color: theme.accentGreen }} />
                <span>Made for your real life.</span>
              </div>
              <p className="text-xs sm:text-sm leading-relaxed" style={{ color: theme.textSecondary }}>
                A calmer way to stay curious, informed and connected.
              </p>
            </div>

          </div>

        </div>
      </section>


      {/* FEATURE CARDS SECTION ("A Clearer Picture of You") */}
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
          
          {/* Card 1: Your Health. One Place */}
          <div className="p-8 rounded-[36px] flex flex-col justify-between space-y-8 transition-transform hover:-translate-y-1 shadow-sm border"
               style={{ backgroundColor: theme.bgSecondary, borderColor: theme.borderLight }}>
            
            {/* Mock UI Preview */}
            <div className="p-5 rounded-2xl bg-white/80 backdrop-blur shadow-sm space-y-3">
              <div className="flex justify-between items-center text-xs font-medium text-slate-400">
                <span>Your health library</span>
                <FileText className="w-3.5 h-3.5" />
              </div>
              
              <div className="p-3 bg-slate-50 rounded-xl flex items-center justify-between text-xs">
                <div className="flex items-center gap-2.5">
                  <FileText className="w-4 h-4 text-emerald-600" />
                  <div>
                    <div className="font-medium text-slate-800">Blood test results</div>
                    <div className="text-[10px] text-slate-400">12 Oct · 8 biomarkers</div>
                  </div>
                </div>
                <CheckCircle2 className="w-4 h-4 text-emerald-600" />
              </div>

              <div className="p-3 bg-slate-50 rounded-xl flex items-center justify-between text-xs">
                <div className="flex items-center gap-2.5">
                  <FileText className="w-4 h-4 text-emerald-600" />
                  <div>
                    <div className="font-medium text-slate-800">Wellbeing check-in</div>
                    <div className="text-[10px] text-slate-400">Today · Personal notes</div>
                  </div>
                </div>
                <CheckCircle2 className="w-4 h-4 text-emerald-600" />
              </div>
            </div>

            {/* Card Info */}
            <div className="space-y-4">
              <h3 className="text-2xl font-serif-aesthetic" style={{ color: theme.textPrimary }}>
                Your Health. One Place.
              </h3>
              <p className="text-xs sm:text-sm leading-relaxed" style={{ color: theme.textSecondary }}>
                A personal home for your results, records and reflections. Less searching. More seeing the whole picture.
              </p>
              <a href="#" className="inline-flex items-center gap-1.5 text-xs font-semibold pt-2 hover:underline" style={{ color: theme.textPrimary }}>
                <span>Bring it all together</span>
                <ArrowUpRight className="w-3.5 h-3.5" />
              </a>
            </div>

          </div>

          {/* Card 2: Understand Your Results */}
          <div className="p-8 rounded-[36px] flex flex-col justify-between space-y-8 transition-transform hover:-translate-y-1 shadow-sm border"
               style={{ backgroundColor: theme.bgSecondary, borderColor: theme.borderLight }}>
            
            {/* Mock UI Preview */}
            <div className="p-5 rounded-2xl bg-white/80 backdrop-blur shadow-sm space-y-4">
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
            </div>

            {/* Card Info */}
            <div className="space-y-4">
              <h3 className="text-2xl font-serif-aesthetic" style={{ color: theme.textPrimary }}>
                Understand Your Results.
              </h3>
              <p className="text-xs sm:text-sm leading-relaxed" style={{ color: theme.textSecondary }}>
                AI-powered explanations turn complex numbers into simple language, so you can ask better questions.
              </p>
              <a href="#" className="inline-flex items-center gap-1.5 text-xs font-semibold pt-2 hover:underline" style={{ color: theme.textPrimary }}>
                <span>Make sense of your data</span>
                <ArrowUpRight className="w-3.5 h-3.5" />
              </a>
            </div>

          </div>

          {/* Card 3: Track Your Wellbeing */}
          <div className="p-8 rounded-[36px] flex flex-col justify-between space-y-8 transition-transform hover:-translate-y-1 shadow-sm border"
               style={{ backgroundColor: theme.bgSecondary, borderColor: theme.borderLight }}>
            
            {/* Mock UI Preview */}
            <div className="p-5 rounded-2xl bg-white/80 backdrop-blur shadow-sm space-y-4">
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
                <span>JUN</span>
                <span>JUL</span>
                <span>AUG</span>
                <span>SEP</span>
                <span>OCT</span>
              </div>
            </div>

            {/* Card Info */}
            <div className="space-y-4">
              <h3 className="text-2xl font-serif-aesthetic" style={{ color: theme.textPrimary }}>
                Track Your Wellbeing.
              </h3>
              <p className="text-xs sm:text-sm leading-relaxed" style={{ color: theme.textSecondary }}>
                Notice patterns, follow changes and connect how you feel with what your health data is telling you.
              </p>
              <a href="#" className="inline-flex items-center gap-1.5 text-xs font-semibold pt-2 hover:underline" style={{ color: theme.textPrimary }}>
                <span>See your story unfold</span>
                <ArrowUpRight className="w-3.5 h-3.5" />
              </a>
            </div>

          </div>

        </div>

        <div className="text-right text-[10px]" style={{ color: theme.textSecondary }}>
          Illustrative data, for a little perspective.
        </div>
      </section>


      {/* HOW IT WORKS SECTION */}
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
          
          {/* Step 01 */}
          <div className="space-y-6 relative">
            <div className="flex items-center justify-between pb-4 border-b" style={{ borderColor: theme.borderLight }}>
              <span className="font-serif-aesthetic text-2xl text-slate-400">01</span>
              <FileText className="w-5 h-5 text-slate-400" />
            </div>
            <h3 className="text-xl font-serif-aesthetic" style={{ color: theme.textPrimary }}>
              Begin with what you know.
            </h3>
            <p className="text-xs sm:text-sm leading-relaxed" style={{ color: theme.textSecondary }}>
              Create your private health space. Upload existing results, or start fresh with a test.
            </p>
          </div>

          {/* Step 02 */}
          <div className="space-y-6 relative">
            <div className="flex items-center justify-between pb-4 border-b" style={{ borderColor: theme.borderLight }}>
              <span className="font-serif-aesthetic text-2xl text-slate-400">02</span>
              <Sparkles className="w-5 h-5 text-slate-400" />
            </div>
            <h3 className="text-xl font-serif-aesthetic" style={{ color: theme.textPrimary }}>
              Find meaning in the details.
            </h3>
            <p className="text-xs sm:text-sm leading-relaxed" style={{ color: theme.textSecondary }}>
              Explore clear explanations of your biomarkers, with AI that makes the complex feel simple.
            </p>
          </div>

          {/* Step 03 */}
          <div className="space-y-6 relative">
            <div className="flex items-center justify-between pb-4 border-b" style={{ borderColor: theme.borderLight }}>
              <span className="font-serif-aesthetic text-2xl text-slate-400">03</span>
              <Leaf className="w-5 h-5 text-slate-400" />
            </div>
            <h3 className="text-xl font-serif-aesthetic" style={{ color: theme.textPrimary }}>
              Grow your understanding.
            </h3>
            <p className="text-xs sm:text-sm leading-relaxed" style={{ color: theme.textSecondary }}>
              Track changes over time and bring better questions to conversations with your clinician.
            </p>
          </div>

        </div>

      </section>


      {/* DARK SECTION: THOUGHTFUL TESTING */}
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
              <div className="flex justify-between items-center py-3 border-b border-stone-800 text-sm">
                <span className="font-medium text-stone-200">Everyday essentials</span>
                <span className="text-xs text-stone-400">Vitamins & key nutrients</span>
              </div>
              <div className="flex justify-between items-center py-3 border-b border-stone-800 text-sm">
                <span className="font-medium text-stone-200">Your inner balance</span>
                <span className="text-xs text-stone-400">Metabolic & hormonal health</span>
              </div>
              <div className="flex justify-between items-center py-3 border-b border-stone-800 text-sm">
                <span className="font-medium text-stone-200">The bigger picture</span>
                <span className="text-xs text-stone-400">Heart health & wellbeing</span>
              </div>
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


      {/* FINAL CTA & FOOTER SECTION */}
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
              <a href="#how-it-works" className="hover:underline">How It Works</a>
              <a href="#testing" className="hover:underline">Testing</a>
              <a href="#philosophy" className="hover:underline">About</a>
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

    </div>
  );
}