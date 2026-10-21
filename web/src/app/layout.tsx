import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import { serverEnv } from '@/config/server-env';
import './globals.css';

export const metadata: Metadata = {
  title: 'Lotero',
  description: 'Aplicativo Lotero',
};

export default function RootLayout({ children }: { children: ReactNode }) {
  // Validate configuration on the server without exposing it or making a request.
  void serverEnv;
  return (
    <html lang="pt-BR">
      <body>{children}</body>
    </html>
  );
}
