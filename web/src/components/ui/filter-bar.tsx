'use client';

import { useState, type FormEvent, type ReactNode } from 'react';
import { cn } from '@/lib/cn';
import { Button } from './button';
import { Dialog, useCompactOutsideDialog } from './dialog';

// FilterBar (design/specs/components/filter-bar.md): the controls that set a list's server query
// plus a plain-language summary of the scope shown. Changes are staged and applied together (one
// request, one result announcement). It uses only the endpoint's real query fields — the screen
// passes them in — and fields a role cannot use are absent, not disabled.

export function FilterBar({
  label,
  scopeSummary,
  fields,
  onApply,
  onClear,
  dirty = false,
  applying = false,
  applyLabel = 'Filtrar',
  activeCount,
  activeCountLabel,
  resultsAnnouncement,
  className,
}: {
  /** The form region's name (**Filtros de transações**). */
  readonly label: string;
  /** The applied scope, in words or chips (**24/09/2026 · Terminal 2 · padrão — todos os tipos**). */
  readonly scopeSummary: ReactNode;
  readonly fields: ReactNode;
  readonly onApply: () => void;
  /** Shown only when some value differs from the default. */
  readonly onClear?: () => void;
  /** Staged changes not yet applied: the apply action becomes primary. */
  readonly dirty?: boolean;
  readonly applying?: boolean;
  readonly applyLabel?: string;
  /** Compact: how many filters are active (**Filtros ({n})**). */
  readonly activeCount: number;
  /** The `{n}` of **Filtros ({n})**, formatted by the screen (`@loto/core`). */
  readonly activeCountLabel?: string;
  /** e.g. **37 transações**, announced politely when results arrive; focus stays put. */
  readonly resultsAnnouncement?: string;
  readonly className?: string;
}) {
  // Inside a dialog the fields stay inline: one modal layer only.
  const compact = useCompactOutsideDialog();
  const [sheet, setSheet] = useState(false);
  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onApply();
    setSheet(false);
  };
  const actions = (
    <div className="flex flex-wrap items-center gap-inline-md">
      <Button type="submit" variant={dirty ? 'primary' : 'secondary'} loading={applying}>
        {applyLabel}
      </Button>
      {onClear ? (
        <Button variant="text" onClick={onClear}>
          Limpar filtros
        </Button>
      ) : null}
    </div>
  );
  const summary = (
    <p className="type-helper text-text-secondary" data-testid="filter-scope">
      {scopeSummary}
    </p>
  );
  const live = (
    <span role="status" aria-live="polite" className="sr-only">
      {resultsAnnouncement ?? ''}
    </span>
  );

  if (compact) {
    return (
      <div className={cn('flex flex-col gap-stack-sm', className)}>
        <div className="flex flex-wrap items-center justify-between gap-inline-md">
          {summary}
          <Button variant="secondary" aria-haspopup="dialog" onClick={() => setSheet(true)}>
            {`Filtros (${activeCountLabel ?? activeCount})`}
          </Button>
        </div>
        {live}
        <Dialog open={sheet} onDismiss={() => setSheet(false)} title={label} variant="sheet">
          <form aria-label={label} onSubmit={submit} className="flex flex-col gap-stack-md">
            {fields}
            {actions}
          </form>
        </Dialog>
      </div>
    );
  }

  return (
    <div
      className={cn(
        'flex flex-col gap-stack-sm rounded-panel border border-border-default bg-surface-default p-inset-panel',
        className,
      )}
    >
      <form aria-label={label} onSubmit={submit} className="flex flex-col gap-stack-md">
        {fields}
        {actions}
      </form>
      {summary}
      {live}
    </div>
  );
}
