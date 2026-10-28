import { nativeTokens } from './token-adapter';

/** Claro / Escuro / Sistema — a device presentation preference, never account or session data. */
export type ThemePreference = 'system' | 'light' | 'dark';
export type ColorScheme = 'light' | 'dark';
export type SchemeReading = 'light' | 'dark' | 'unspecified' | null | undefined;

export const themePreferenceKey = 'loto.theme-preference.v1';
export const themePreferences: readonly ThemePreference[] = ['light', 'dark', 'system'];
export const themePreferenceLabels: Readonly<Record<ThemePreference, string>> = {
  light: 'Claro',
  dark: 'Escuro',
  system: 'Sistema',
};

export type ThemeColors = (typeof nativeTokens.themes)[ColorScheme];
export type SemanticColors = ThemeColors['sem'];
export type DomainColors = ThemeColors['dom'];
export type TypographyRole = keyof typeof nativeTokens.typography;
export type MotionName = keyof typeof nativeTokens.motion.transition;

/** A stored value that is not one of the three choices falls back to Sistema. */
export function parseThemePreference(raw: string | null | undefined): ThemePreference {
  return raw === 'light' || raw === 'dark' || raw === 'system' ? raw : 'system';
}

/** The explicit choice wins; Sistema follows the OS reading (light when the OS reports none). */
export function resolveScheme(preference: ThemePreference, system: SchemeReading): ColorScheme {
  if (preference !== 'system') return preference;
  return system === 'dark' ? 'dark' : 'light';
}

export function motionFor(name: MotionName, reducedMotion: boolean) {
  return (reducedMotion ? nativeTokens.motion.reduced : nativeTokens.motion.transition)[name];
}
