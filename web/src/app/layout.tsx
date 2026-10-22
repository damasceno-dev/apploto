import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import { AppProviders } from '@/client/app-providers';
import { serverEnv } from '@/config/server-env';
import { serverSnapshot } from '@/server/gate/guards';
import './globals.css';

export const metadata: Metadata = {
  title: 'Lotero',
  description: 'Aplicativo Lotero',
};

export default async function RootLayout({ children }: { children: ReactNode }) {
  // The session view from this request's cookies (no network, no token): seeds the client.
  const session = await serverSnapshot();
  return (
    <html lang="pt-BR">
      <body>
        <AppProviders session={session} fixtures={serverEnv.e2eFixtures}>
          {children}
        </AppProviders>
      </body>
    </html>
  );
}
