// Палитра лендинга (сверена с макетами Figma). Её можно менять «вживую» через ThemeCustomizer,
// поэтому секции получают theme пропсом.

export const DEFAULT_THEME = {
  bgPrimary: '#f7f7f2',      // тёплый кремовый фон страницы
  bgCard: '#ffffff',         // белые карточки и кнопки «второго плана»
  bgSecondary: '#eef0e9',    // приглушённый шалфей (секция философии)
  bgMint: '#e4ece5',         // светло-мятный фон секции с карточками и финального блока
  bgPanel: '#f1f4ef',        // карточка-виджет на мятном фоне
  bgDark: '#465a4a',         // глубокий зелёный: главная кнопка на телефоне, акцентный столбец
  textPrimary: '#2a302b',    // почти чёрный зелёный
  textSecondary: '#6c736c',  // приглушённый текст
  textMuted: '#9aa19a',      // подписи осей и мелкие метки
  accentGreen: '#737f69',    // оливковая кнопка
  accentGreenHover: '#5f6b57',
  accentSoft: '#b9c4b5',     // столбцы и полосы второго плана
  accentMint: '#dce7d8',     // фон бейджей «Within range»
  accentMintText: '#3d5340', // текст на мятном бейдже
  borderLight: '#e3e6dd',    // тонкие границы
};

export type LandingTheme = typeof DEFAULT_THEME;
export type ThemeColor = keyof LandingTheme;

// Пропсы любой секции лендинга
export interface SectionProps {
  theme: LandingTheme;
}
