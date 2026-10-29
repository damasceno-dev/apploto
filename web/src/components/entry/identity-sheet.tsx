import type { ReactNode } from 'react';
import { Card } from '@/components/ui/card';
import { ThemeControl } from '@/theme/theme-control';
export function IdentitySheet({
  title,
  children,
}: {
  readonly title: string;
  readonly children: ReactNode;
}) {
  return (
    <>
      <header className="flex flex-wrap items-center justify-between gap-stack-md bg-nav-bg p-4 text-nav-text-strong">
        <span className="type-section-title">Lotero</span>
        <ThemeControl iconsOnly />
      </header>
      <main className="mx-auto max-w-dialog-max px-page-inline-compact py-page-block-start">
        <Card variant="sheet" eyebrow="Lotero">
          <h1 className="type-page-title">{title}</h1>
          {children}
        </Card>
      </main>
    </>
  );
}
