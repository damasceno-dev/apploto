'use client';

import { useEffect, useRef, useState } from 'react';
import { cn } from '@/lib/cn';
import { Button } from './button';
import { useFormat } from './format-port';
import { ChevronLeftIcon, ChevronRightIcon } from './icons';

// Pagination (design/specs/components/pagination.md): driven only by the endpoint's envelope;
// `HasNext`/`HasPrevious` decide the buttons and the range text says exactly which slice of the
// whole is shown. Counts are formatted by the format port (`@loto/core`).

export interface PageEnvelope {
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
  readonly hasNext: boolean;
  readonly hasPrevious: boolean;
}

/** Page numbers with gaps for long sets: first, the neighbours of the current page, last. */
function pageWindow(page: number, total: number): (number | 'gap')[] {
  const pages = new Set(
    [1, total, page - 1, page, page + 1].filter((value) => value >= 1 && value <= total),
  );
  const sorted = [...pages].sort((a, b) => a - b);
  const items: (number | 'gap')[] = [];
  sorted.forEach((value, index) => {
    const previous = sorted[index - 1];
    if (previous !== undefined && value - previous > 1) items.push('gap');
    items.push(value);
  });
  return items;
}

export function Pagination({
  envelope,
  onPageChange,
  listName,
  updating = false,
  onNavigated,
  className,
}: {
  readonly envelope: PageEnvelope;
  readonly onPageChange: (page: number) => void;
  /** Names the landmark: **Paginação de {lista}**. */
  readonly listName: string;
  /** Loading the next page: buttons wait; the range text stays with **Atualizando…**. */
  readonly updating?: boolean;
  /** After a change settles: move focus to the table heading (not the top of the page). */
  readonly onNavigated?: () => void;
  readonly className?: string;
}) {
  const format = useFormat();
  const { page, pageSize, totalCount, totalPages, hasNext, hasPrevious } = envelope;
  const first = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const last = Math.min(page * pageSize, totalCount);
  const range = `${format.count(first)}–${format.count(last)} de ${format.count(totalCount)}`;
  const [announced, setAnnounced] = useState('');
  const requested = useRef(false);
  useEffect(() => {
    if (!requested.current || updating) return;
    requested.current = false;
    setAnnounced(range);
    onNavigated?.();
  }, [range, updating, onNavigated]);
  const go = (target: number) => {
    requested.current = true;
    onPageChange(target);
  };

  if (totalPages <= 1) return null;
  return (
    <nav
      aria-label={`Paginação de ${listName}`}
      className={cn('flex flex-wrap items-center justify-between gap-inline-md', className)}
    >
      <p className="type-helper text-text-secondary tabular-nums">
        {range}
        {updating ? ' · Atualizando…' : null}
      </p>
      <span role="status" aria-live="polite" className="sr-only">
        {announced}
      </span>
      <div className="flex flex-wrap items-center gap-inline-sm">
        <Button
          variant="secondary"
          size="small"
          icon={<ChevronLeftIcon />}
          aria-label="Página anterior"
          disabled={!hasPrevious || updating}
          onClick={() => go(page - 1)}
        >
          Anterior
        </Button>
        <span className="type-helper text-text-secondary tabular-nums max-medium:inline wide:hidden">
          {`Página ${format.count(page)} de ${format.count(totalPages)}`}
        </span>
        <ol className="hidden items-center gap-inline-xs wide:flex">
          {pageWindow(page, totalPages).map((item, index) =>
            item === 'gap' ? (
              <li key={`gap-${index}`} aria-hidden="true" className="px-1 text-text-muted">
                …
              </li>
            ) : (
              <li key={item}>
                <Button
                  variant={item === page ? 'primary' : 'text'}
                  size="small"
                  aria-current={item === page ? 'page' : undefined}
                  aria-label={`Página ${format.count(item)}`}
                  disabled={updating}
                  onClick={() => item !== page && go(item)}
                >
                  {format.count(item)}
                </Button>
              </li>
            ),
          )}
        </ol>
        <Button
          variant="secondary"
          size="small"
          aria-label="Próxima página"
          disabled={!hasNext || updating}
          onClick={() => go(page + 1)}
        >
          <span className="inline-flex items-center gap-inline-xs whitespace-nowrap">
            Próxima
            <ChevronRightIcon />
          </span>
        </Button>
      </div>
    </nav>
  );
}
