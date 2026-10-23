import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';
import { InfoIcon } from './icons';
import { LineIcon } from './form-field';

// DetailList (design/specs/components/detail-list.md): label/value pairs for facts that are read,
// not edited — the read-only counterpart of a form. Values are selectable; an absent value is the
// em dash, never R$ 0,00 or an empty cell.

export interface DetailRow {
  readonly label: ReactNode;
  /** `null` renders **—** (formatting §8); pass the catalog's fallback text where one exists. */
  readonly value: ReactNode | null;
  readonly action?: ReactNode;
  /** A restriction row (**Período bloqueado até 31/08/2026**): muted, with an info icon. */
  readonly restriction?: boolean;
}

export function DetailList({
  rows,
  groups,
  className,
}: {
  readonly rows?: readonly DetailRow[];
  /** Groups separated by a rule. */
  readonly groups?: readonly (readonly DetailRow[])[];
  readonly className?: string;
}) {
  const sets = groups ?? (rows ? [rows] : []);
  return (
    <div className={cn('flex min-w-0 flex-col', className)}>
      {sets.map((set, index) => (
        <dl
          key={index}
          className={cn(
            'grid grid-cols-1 gap-y-stack-sm medium:grid-cols-[minmax(10em,max-content)_1fr] medium:gap-x-inline-lg',
            index > 0 && 'mt-stack-md border-t border-border-subtle pt-stack-md',
          )}
        >
          {set.map((row, rowIndex) => (
            <div key={rowIndex} className="contents">
              <dt className="type-label text-text-secondary">{row.label}</dt>
              <dd
                className={cn(
                  'flex min-w-0 flex-wrap items-start gap-inline-md type-body select-text [overflow-wrap:anywhere]',
                  row.restriction ? 'text-text-muted' : 'text-text-primary',
                  'max-medium:mb-stack-xs',
                )}
              >
                {row.restriction ? (
                  <LineIcon>
                    <InfoIcon />
                  </LineIcon>
                ) : null}
                <span className="min-w-0">{row.value ?? '—'}</span>
                {row.action ?? null}
              </dd>
            </div>
          ))}
        </dl>
      ))}
    </div>
  );
}
