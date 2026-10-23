'use client';

import Link from 'next/link';
import { useEffect, useRef, type ReactNode } from 'react';
import { cn } from '@/lib/cn';
import { ArrowLeftIcon, ChevronRightIcon } from './icons';

// PageHeader (design/specs/components/page-header.md): where the user came from, what this is, the
// context that scopes it and the screen's main actions. The origin comes from the validated
// navigation context — never a URL parameter claiming permission; without one, the catalog fallback
// or nothing. On route entry focus moves to the title, which is the page's only `<h1>`.

export interface Crumb {
  readonly label: string;
  /** A real address (branch routes include their `/f/{branchId}` namespace). */
  readonly href: string;
}

export function PageHeader({
  title,
  origin,
  context,
  status,
  actions,
  variant = 'record',
  focusOnMount = true,
  className,
}: {
  readonly title: ReactNode;
  /** The semantic origin chain, outermost first; the last item is where **Voltar para** goes. */
  readonly origin?: readonly Crumb[];
  /** **Terminal 2 · quinta-feira, 24 de setembro de 2026 · Rascunho · Meu turno**, pre-formatted. */
  readonly context?: ReactNode;
  readonly status?: ReactNode;
  readonly actions?: ReactNode;
  readonly variant?: 'record' | 'list' | 'auth';
  readonly focusOnMount?: boolean;
  readonly className?: string;
}) {
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    if (focusOnMount) heading.current?.focus();
  }, [focusOnMount]);
  const back = origin?.at(-1);
  return (
    <header
      className={cn(
        'flex flex-col gap-stack-sm border-b border-border-default pb-stack-md mb-page-section-gap',
        variant === 'auth' && 'border-b-0 mb-stack-md',
        className,
      )}
    >
      {origin && origin.length > 0 ? (
        <>
          <nav aria-label="Você está em" className="hidden medium:block">
            <ol className="flex flex-wrap items-center gap-inline-xs type-helper">
              {origin.map((crumb) => (
                <li key={crumb.href} className="flex items-center gap-inline-xs">
                  <Link
                    href={crumb.href as never}
                    className="text-text-link hover:text-text-link-hover hover:underline"
                  >
                    {crumb.label}
                  </Link>
                  <ChevronRightIcon className="text-text-muted" />
                </li>
              ))}
              <li aria-current="page" className="text-text-secondary">
                {title}
              </li>
            </ol>
          </nav>
          {back ? (
            <Link
              href={back.href as never}
              className="inline-flex min-h-size-touch-target-min items-center gap-inline-xs type-helper text-text-link hover:text-text-link-hover medium:hidden"
            >
              <ArrowLeftIcon />
              {`Voltar para ${back.label}`}
            </Link>
          ) : null}
        </>
      ) : null}
      <div className="flex flex-wrap items-start justify-between gap-x-inline-lg gap-y-stack-sm">
        <div className="flex min-w-0 flex-col gap-stack-xs">
          <div className="flex flex-wrap items-center gap-inline-md">
            <h1
              ref={heading}
              tabIndex={-1}
              className="type-page-title-compact text-text-primary outline-none medium:type-page-title [overflow-wrap:anywhere]"
            >
              {title}
            </h1>
            {status}
          </div>
          {context ? <p className="type-body text-text-secondary">{context}</p> : null}
        </div>
        {actions ? (
          <div className="flex flex-wrap items-center gap-inline-md">{actions}</div>
        ) : null}
      </div>
    </header>
  );
}
