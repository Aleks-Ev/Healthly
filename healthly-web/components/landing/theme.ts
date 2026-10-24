// Палитра лендинга. Её можно менять «вживую» через ThemeCustomizer, поэтому секции получают theme пропсом.

// Default Color Palette matching the exact aesthetic from the screenshots
export const DEFAULT_THEME = {
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

export type LandingTheme = typeof DEFAULT_THEME;
export type ThemeColor = keyof LandingTheme;

// Пропсы любой секции лендинга
export interface SectionProps {
  theme: LandingTheme;
}
