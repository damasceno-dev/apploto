'use client';

import { useEffect, useRef, useState, type ReactNode } from 'react';
import { FeedbackBadge } from '@/components/ui/badge';
import { cn } from '@/lib/cn';

/** A gallery section for one component family or token category, with its contract file. */
export function Section({
  id,
  title,
  contract,
  children,
}: {
  readonly id: string;
  readonly title: string;
  readonly contract: string;
  readonly children: ReactNode;
}) {
  return (
    <section
      id={id}
      aria-labelledby={`${id}-title`}
      className="flex scroll-mt-6 flex-col gap-stack-lg border-t border-border-default pt-page-section-gap"
    >
      <header className="flex flex-col gap-stack-xs">
        <h2 id={`${id}-title`} className="type-section-title text-text-primary">
          {title}
        </h2>
        <p className="type-helper text-text-muted">
          Contrato: <code className="type-code">{contract}</code>
        </p>
      </header>
      {children}
    </section>
  );
}

/** One variant or state, with what it demonstrates (keyboard, touch, scenario notes). */
export function Example({
  title,
  note,
  fixture = false,
  className,
  children,
}: {
  readonly title: string;
  readonly note?: ReactNode;
  /** Shows the **Dados de exemplo** marker: the values are fixtures, not API data. */
  readonly fixture?: boolean;
  readonly className?: string;
  readonly children: ReactNode;
}) {
  return (
    <div className="flex min-w-0 flex-col gap-stack-sm">
      <div className="flex flex-wrap items-center gap-inline-sm">
        <h3 className="type-label text-text-primary">{title}</h3>
        {fixture ? <FeedbackBadge kind="neutral">Dados de exemplo</FeedbackBadge> : null}
      </div>
      {note ? <p className="type-helper text-text-muted">{note}</p> : null}
      <div className={cn('flex min-w-0 flex-col gap-stack-md', className)}>{children}</div>
    </div>
  );
}

/** The same content pinned to Claro and to Escuro, side by side (stacked on narrow screens). */
export function ThemePair({
  children,
}: {
  readonly children: (theme: 'light' | 'dark') => ReactNode;
}) {
  return (
    <div className="grid grid-cols-1 gap-stack-md wide:grid-cols-2">
      {(['light', 'dark'] as const).map((theme) => (
        <div
          key={theme}
          data-theme-scope={theme}
          className="flex min-w-0 flex-col gap-stack-sm rounded-panel border border-border-default bg-background p-inset-panel text-text-primary"
        >
          <span className="type-overline text-text-muted">
            {theme === 'light' ? 'Claro' : 'Escuro'}
          </span>
          {children(theme)}
        </div>
      ))}
    </div>
  );
}

/** The computed value of a CSS custom property where this element sits (its theme scope). */
export function ComputedValue({
  variable,
  className,
}: {
  readonly variable: string;
  readonly className?: string;
}) {
  const ref = useRef<HTMLElement>(null);
  const [value, setValue] = useState('');
  useEffect(() => {
    const read = () => {
      if (ref.current) setValue(getComputedStyle(ref.current).getPropertyValue(variable).trim());
    };
    read();
    const observer = new MutationObserver(read);
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['data-theme-preference'],
    });
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)');
    media.addEventListener('change', read);
    reduced.addEventListener('change', read);
    return () => {
      observer.disconnect();
      media.removeEventListener('change', read);
      reduced.removeEventListener('change', read);
    };
  }, [variable]);
  return (
    <code
      ref={ref}
      className={cn('type-code text-text-secondary', className)}
      data-variable={variable}
    >
      {value || '…'}
    </code>
  );
}
