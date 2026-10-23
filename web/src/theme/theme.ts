// Theme preference (Web M3 Phase 2): a device preference — Claro, Escuro or Sistema (default). It
// lives in a non-secret cookie so the server render can put it on `<html>` before first paint;
// Sistema is resolved by CSS (`prefers-color-scheme`), so no script decides the first paint. It is
// never part of the session: Sair, a new login or a branch change leave it alone. Isomorphic.

export const themePreferences = ['light', 'dark', 'system'] as const;
export type ThemePreference = (typeof themePreferences)[number];

/** Host-only and `Secure` like every app cookie; not `HttpOnly`: the page itself changes it. */
export const themeCookieName = '__Host-loto-theme';
const themeCookieMaxAgeSeconds = 400 * 24 * 60 * 60;

export const defaultThemePreference: ThemePreference = 'system';

export function parseThemePreference(value: unknown): ThemePreference {
  return themePreferences.includes(value as ThemePreference)
    ? (value as ThemePreference)
    : defaultThemePreference;
}

/** The `document.cookie` assignment that persists `preference` on this device. */
export function themeCookie(preference: ThemePreference): string {
  return `${themeCookieName}=${preference}; Path=/; Max-Age=${themeCookieMaxAgeSeconds}; SameSite=Lax; Secure`;
}

/** The attribute `<html>` carries; CSS selects the theme from it (see `token-adapter.ts`). */
export const themeAttribute = 'data-theme-preference';

/** pt-BR names of the choices (shell blueprint: Claro / Escuro / Sistema). */
export const themeLabels: Readonly<Record<ThemePreference, string>> = Object.freeze({
  light: 'Claro',
  dark: 'Escuro',
  system: 'Sistema',
});
