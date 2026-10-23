import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';
import { Button } from './button';
import { Skeleton } from './skeleton';

// SummaryStrip (design/specs/components/summary-strip.md): labelled figures of one scope. It shows
// what the endpoint returned (or the explanatory totals the catalog allows) — it never computes
// money. A figure that could not load reads **—** with its own retry, never R$ 0,00; figures never
// animate.

export type SummaryFigure =
  | { readonly kind: 'value'; readonly value: ReactNode }
  | { readonly kind: 'loading' }
  | { readonly kind: 'unavailable'; readonly retry?: () => void };

export interface SummaryItem {
  readonly label: ReactNode;
  readonly figure: SummaryFigure;
  /** Scope or provenance (**ainda não enviada**, **Subtotal desta página**). */
  readonly caption?: ReactNode;
}

export function SummaryStrip({
  items,
  variant = 'totals',
  label,
  className,
}: {
  readonly items: readonly SummaryItem[];
  /** `explanatory`: figures derived for explanation only, visually secondary to the official one. */
  readonly variant?: 'totals' | 'explanatory' | 'signed';
  /** The strip's name when it stands alone (**Prévia da diferença · ainda não enviada**). */
  readonly label?: string;
  readonly className?: string;
}) {
  const loading = items.some((item) => item.figure.kind === 'loading');
  return (
    <dl
      aria-label={label}
      aria-busy={loading || undefined}
      className={cn(
        'grid min-w-0 grid-cols-1 overflow-hidden rounded-panel border border-border-default bg-surface-default',
        'medium:grid-cols-2 wide:grid-flow-col wide:auto-cols-fr wide:grid-cols-none',
        className,
      )}
    >
      {items.map((item, index) => (
        <div
          key={index}
          className={cn(
            'flex min-w-0 flex-col gap-stack-xs p-inset-panel',
            // Rules: horizontal when stacked, vertical between columns.
            'border-border-subtle not-first:border-t medium:[&:nth-child(2)]:border-t-0 medium:odd:not-first:border-t medium:even:border-s wide:border-t-0! wide:not-first:border-s',
          )}
        >
          <dt className="type-table-header text-text-muted">{item.label}</dt>
          <dd
            className={cn(
              'type-summary-value text-text-primary [overflow-wrap:anywhere]',
              variant === 'explanatory' && 'text-text-secondary',
            )}
          >
            {item.figure.kind === 'value' ? (
              item.figure.value
            ) : item.figure.kind === 'loading' ? (
              <Skeleton className="w-[6ch]" />
            ) : (
              <span className="flex flex-wrap items-baseline gap-inline-md">
                <span>—</span>
                {item.figure.retry ? (
                  <Button variant="text" size="small" onClick={item.figure.retry}>
                    Tentar novamente
                  </Button>
                ) : null}
              </span>
            )}
          </dd>
          {item.figure.kind === 'unavailable' ? (
            <dd className="type-helper text-feedback-danger-fg">Não foi possível carregar</dd>
          ) : item.caption ? (
            <dd className="type-helper text-text-muted">{item.caption}</dd>
          ) : null}
        </div>
      ))}
    </dl>
  );
}
