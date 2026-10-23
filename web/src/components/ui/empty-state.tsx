import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';

// EmptyState (design/specs/components/empty-state.md): explains a successful, scoped response with
// nothing to show — and only that (never a failure, a loading count or a forbidden read). Four kinds,
// because each asks for a different next step. An all-clear stays neutral: "nothing pending" is not
// a financial result.

export type EmptyKind = 'no-records' | 'no-matches' | 'all-clear' | 'setup';

export function EmptyState({
  kind,
  statement,
  explanation,
  primaryAction,
  secondaryAction,
  className,
}: {
  readonly kind: EmptyKind;
  /** One sentence (**Nenhuma transação neste filtro.**). */
  readonly statement: ReactNode;
  /** The scope summary (no matches) or the catalog's explanation (setup). */
  readonly explanation?: ReactNode;
  readonly primaryAction?: ReactNode;
  readonly secondaryAction?: ReactNode;
  readonly className?: string;
}) {
  return (
    <div
      data-kind={kind}
      className={cn('flex flex-col items-start gap-stack-sm py-stack-lg', className)}
    >
      <p className="type-body-strong text-text-primary">{statement}</p>
      {explanation ? <div className="type-body text-text-secondary">{explanation}</div> : null}
      {primaryAction || secondaryAction ? (
        <div className="flex flex-wrap gap-inline-md pt-stack-xs">
          {primaryAction}
          {secondaryAction}
        </div>
      ) : null}
    </div>
  );
}
