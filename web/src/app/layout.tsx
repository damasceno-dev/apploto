import type { Metadata, Viewport } from 'next';
import type { ReactNode } from 'react';
import { cookies } from 'next/headers';
import { AppProviders } from '@/client/app-providers';
import { serverEnv } from '@/config/server-env';
import { serverSnapshot } from '@/server/gate/guards';
import { parseThemePreference, themeCookieName } from '@/theme/theme';
import { ThemeProvider } from '@/theme/theme-provider';
import './globals.css';

export const metadata: Metadata = {
  title: 'Lotero',
  description: 'Aplicativo Lotero',
};

// Both themes ship; native form controls and scrollbars follow the active one.
export const viewport: Viewport = { colorScheme: 'light dark' };

export default async function RootLayout({ children }: { children: ReactNode }) {
  // The session view from this request's cookies (no network, no token): seeds the client.
  const session = await serverSnapshot();
  // The device's theme choice is on `<html>` in the first byte of HTML: no wrong-theme flash.
  const theme = parseThemePreference((await cookies()).get(themeCookieName)?.value);
  return (
    <html lang="pt-BR" data-theme-preference={theme}>
      <body>
        <ThemeProvider initial={theme}>
          <AppProviders session={session} fixtures={serverEnv.e2eFixtures}>
            {children}
          </AppProviders>
        </ThemeProvider>
      </body>
    </html>
  );
}
