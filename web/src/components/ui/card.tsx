import { useId, type ReactNode } from 'react';
import { cn } from '@/lib/cn';

// Card (design/specs/components/card.md): a bounded panel for one coherent unit of work, used
// sparingly — ruled sections are preferred, and there is no tinted highlight card. A card's actions
// are explicit buttons or links; the card itself is never one big click target.

export type CardVariant = 'section' | 'next-action' | 'checklist' | 'sheet';

export function Card({
  variant = 'section',
  eyebrow,
  title,
  titleLevel = 2,
  actions,
  accent,
  region = variant === 'next-action',
  className,
  children,
}: {
  readonly variant?: CardVariant;
  readonly eyebrow?: ReactNode;
  readonly title?: ReactNode;
  readonly titleLevel?: 2 | 3;
  readonly actions?: ReactNode;
  /** A status card's rule: a feedback border on the start edge. */
  readonly accent?: 'danger' | 'warning' | 'info' | 'success';
  /** A landmark-worthy unit (the Cockpit's next action): a region named by its heading. */
  readonly region?: boolean;
  readonly className?: string;
  readonly children?: ReactNode;
}) {
  const headingId = useId();
  const Heading = titleLevel === 2 ? 'h2' : 'h3';
  return (
    <section
      role={region ? 'region' : undefined}
      aria-labelledby={title && region ? headingId : undefined}
      className={cn(
        'flex min-w-0 flex-col gap-stack-md rounded-panel border border-border-default bg-surface-default p-inset-panel',
        variant === 'next-action' &&
          'border-t-(length:--spacing-size-marker-width) border-t-action-primary-bg',
        variant === 'sheet' && 'mx-auto w-full max-w-dialog-max p-inset-dialog',
        accent === 'danger' &&
          'border-s-(length:--spacing-size-marker-width) border-s-feedback-danger-border',
        accent === 'warning' &&
          'border-s-(length:--spacing-size-marker-width) border-s-feedback-warning-border',
        accent === 'info' &&
          'border-s-(length:--spacing-size-marker-width) border-s-feedback-info-border',
        accent === 'success' &&
          'border-s-(length:--spacing-size-marker-width) border-s-feedback-success-border',
        className,
      )}
    >
      {eyebrow || title ? (
        <header className="flex flex-col gap-stack-xs">
          {eyebrow ? <span className="type-overline text-text-muted">{eyebrow}</span> : null}
          {title ? (
            <Heading
              id={headingId}
              className={cn(
                variant === 'next-action' ? 'type-next-action' : 'type-section-title',
                'text-text-primary',
              )}
            >
              {title}
            </Heading>
          ) : null}
        </header>
      ) : null}
      {children}
      {actions ? <div className="flex flex-wrap items-center gap-inline-md">{actions}</div> : null}
    </section>
  );
}

/** One checklist line: status text and a link to resolve it (owner setup checklist). */
export function ChecklistItem({
  label,
  status,
  action,
}: {
  readonly label: ReactNode;
  readonly status: ReactNode;
  readonly action?: ReactNode;
}) {
  return (
    <li className="flex flex-wrap items-center justify-between gap-x-inline-md gap-y-stack-xs border-b border-border-subtle py-stack-sm last:border-b-0">
      <span className="flex min-w-0 flex-col">
        <span className="type-body text-text-primary">{label}</span>
        <span className="type-helper text-text-muted">{status}</span>
      </span>
      {action}
    </li>
  );
}
