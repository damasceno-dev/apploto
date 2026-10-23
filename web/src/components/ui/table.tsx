'use client';

import {
  useEffect,
  useId,
  useRef,
  useState,
  type ComponentPropsWithoutRef,
  type MouseEvent,
  type ReactNode,
} from 'react';
import Link from 'next/link';
import { cn } from '@/lib/cn';

// Table / List (design/specs/components/table.md). A real `<table>` with caption and scoped headers
// inside a labelled, keyboard-scrollable region; columns are never dropped to fit. A row may be the
// pointer target for its record, but it always contains an explicit link with an accessible name
// (keyboard users tab to it; the row is not a tab stop). Nested controls never trigger the row, and
// a drag that selects text never navigates. Opening grants no permission.

/** Elements whose own activation must never also open the row. */
const interactive =
  'a, button, input, select, textarea, label, summary, [role="button"], [role="checkbox"], [role="switch"], [contenteditable]';

export function TableRegion({
  caption,
  scrollLabel,
  headingRef,
  updating = false,
  updatingLabel = 'Atualizando…',
  toolbar,
  footer,
  className,
  children,
}: {
  /** What, which scope, which period. Also the table's caption. */
  readonly caption: ReactNode;
  /** The scroll region's name (**Tabela de transações, role horizontalmente**). */
  readonly scrollLabel: string;
  /** Focused after a page change or when the focused row is removed. */
  readonly headingRef?: React.Ref<HTMLHeadingElement>;
  /** A refresh is in flight: previous rows stay, dimmed, with a polite **Atualizando…**. */
  readonly updating?: boolean;
  readonly updatingLabel?: string;
  readonly toolbar?: ReactNode;
  readonly footer?: ReactNode;
  readonly className?: string;
  readonly children: ReactNode;
}) {
  const scroller = useRef<HTMLDivElement>(null);
  const [edges, setEdges] = useState({ start: false, end: false });
  useEffect(() => {
    const element = scroller.current;
    if (element === null) return;
    const measure = () => {
      const max = element.scrollWidth - element.clientWidth;
      const at = Math.abs(element.scrollLeft);
      setEdges({ start: at > 1, end: max - at > 1 });
    };
    measure();
    element.addEventListener('scroll', measure, { passive: true });
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(measure);
    observer?.observe(element);
    return () => {
      element.removeEventListener('scroll', measure);
      observer?.disconnect();
    };
  }, []);
  return (
    <section className={cn('flex min-w-0 flex-col gap-stack-sm', className)}>
      <h2
        ref={headingRef}
        tabIndex={-1}
        className="type-section-title text-text-primary outline-none focus-visible:outline-2"
      >
        {caption}
      </h2>
      {toolbar}
      <span role="status" aria-live="polite" className="sr-only">
        {updating ? updatingLabel : ''}
      </span>
      {updating ? (
        <p className="type-helper text-text-muted" aria-hidden="true">
          {updatingLabel}
        </p>
      ) : null}
      <div className="relative min-w-0">
        <div
          ref={scroller}
          role="region"
          aria-label={scrollLabel}
          // Focusable so keyboard users can scroll it (only when it overflows is it a useful stop).
          tabIndex={0}
          className={cn(
            'min-w-0 overflow-x-auto rounded-panel border border-border-default bg-surface-default',
            updating && 'opacity-60',
          )}
        >
          {children}
        </div>
        {/* Visible affordance that more columns sit beyond an edge. */}
        <span
          aria-hidden="true"
          className={cn(
            'pointer-events-none absolute inset-y-px start-px w-3 rounded-s-panel shadow-raised transition-opacity',
            edges.start ? 'opacity-100' : 'opacity-0',
          )}
        />
        <span
          aria-hidden="true"
          className={cn(
            'pointer-events-none absolute inset-y-px end-px w-3 rounded-e-panel shadow-raised transition-opacity',
            edges.end ? 'opacity-100' : 'opacity-0',
          )}
        />
      </div>
      {footer}
    </section>
  );
}

export function Table({
  caption,
  className,
  children,
  ...props
}: ComponentPropsWithoutRef<'table'> & { readonly caption: string }) {
  return (
    <table
      {...props}
      className={cn(
        'w-full border-separate border-spacing-0 type-table text-text-primary',
        className,
      )}
    >
      <caption className="sr-only">{caption}</caption>
      {children}
    </table>
  );
}

export function TableHead({ children }: { readonly children: ReactNode }) {
  return <thead className="sticky top-0 layer-raised">{children}</thead>;
}

export function TableBody({ children }: { readonly children: ReactNode }) {
  return <tbody>{children}</tbody>;
}

const cellPadding =
  'px-inset-table-cell-x py-inset-table-cell-y first:ps-inset-table-edge-x last:pe-inset-table-edge-x';

export function Th({
  align = 'start',
  sticky,
  className,
  children,
  ...props
}: Omit<ComponentPropsWithoutRef<'th'>, 'align'> & {
  readonly align?: 'start' | 'end';
  readonly sticky?: 'start' | 'end';
}) {
  return (
    <th
      scope="col"
      {...props}
      className={cn(
        cellPadding,
        'border-b border-border-subtle bg-surface-subtle type-table-header whitespace-nowrap text-text-muted',
        align === 'end' ? 'text-end' : 'text-start',
        sticky === 'start' && 'sticky start-0',
        sticky === 'end' && 'sticky end-0',
        className,
      )}
    >
      {children}
    </th>
  );
}

export function Td({
  align = 'start',
  sticky,
  className,
  children,
  ...props
}: Omit<ComponentPropsWithoutRef<'td'>, 'align'> & {
  readonly align?: 'start' | 'end';
  readonly sticky?: 'start' | 'end';
}) {
  return (
    <td
      {...props}
      className={cn(
        cellPadding,
        'border-b border-border-subtle align-top group-last/row:border-b-0',
        // Numbers never break; text cells wrap at enlarged text.
        align === 'end' ? 'text-end whitespace-nowrap tabular-nums' : 'text-start',
        sticky && 'bg-inherit',
        sticky === 'start' && 'sticky start-0',
        sticky === 'end' && 'sticky end-0',
        className,
      )}
    >
      {children}
    </td>
  );
}

export interface RowProps extends ComponentPropsWithoutRef<'tr'> {
  /** The record this row opens: the row is a pointer target and must contain a `RowLink`. */
  readonly href?: string;
  readonly selected?: boolean;
  /** Muted presentation (Cancelled rows keep their legible badge). */
  readonly muted?: boolean;
}

/**
 * A body row. With `href`, a click on the row (outside nested controls, without a text selection)
 * activates its explicit `RowLink` — the same native link, so modifier keys open a new tab.
 */
export function Tr({
  href,
  selected = false,
  muted = false,
  className,
  onClick,
  children,
  ...props
}: RowProps) {
  const open = (event: MouseEvent<HTMLTableRowElement>) => {
    onClick?.(event);
    if (href === undefined || event.defaultPrevented) return;
    const target = event.target as Element;
    if (target.closest(interactive)) return;
    if ((window.getSelection()?.toString() ?? '').length > 0) return;
    const link = event.currentTarget.querySelector<HTMLAnchorElement>('a[data-row-link]');
    link?.dispatchEvent(
      new window.MouseEvent('click', {
        bubbles: true,
        cancelable: true,
        ctrlKey: event.ctrlKey,
        metaKey: event.metaKey,
        shiftKey: event.shiftKey,
        altKey: event.altKey,
        button: event.button,
      }),
    );
  };
  return (
    <tr
      {...props}
      onClick={open}
      aria-selected={selected || undefined}
      data-selected={selected || undefined}
      className={cn(
        'group/row bg-surface-default motion-control transition-colors',
        href !== undefined && 'cursor-pointer hover:bg-surface-hover focus-within:bg-surface-hover',
        selected &&
          'bg-surface-selected hover:[background-image:linear-gradient(var(--loto-sem-surface-selected),var(--loto-sem-surface-selected))] hover:bg-surface-hover focus-within:[background-image:linear-gradient(var(--loto-sem-surface-selected),var(--loto-sem-surface-selected))]',
        muted && 'text-text-muted',
        className,
      )}
    >
      {children}
    </tr>
  );
}

/**
 * The row's explicit link (**Abrir transação**), named with the row's identity (date, value,
 * account) so repeated labels stay distinguishable.
 */
export function RowLink({
  href,
  label,
  accessibleName,
}: {
  readonly href: string;
  readonly label: ReactNode;
  readonly accessibleName: string;
}) {
  return (
    <Link
      href={href as never}
      data-row-link=""
      aria-label={accessibleName}
      className="type-table text-action-ghost-fg underline-offset-2 hover:text-action-ghost-fg-hover hover:underline"
    >
      {label}
    </Link>
  );
}

/** A table body placeholder for loading / empty / error, spanning every column. */
export function TableMessageRow({
  columns,
  children,
}: {
  readonly columns: number;
  readonly children: ReactNode;
}) {
  return (
    <tr>
      <td colSpan={columns} className="px-inset-table-edge-x">
        {children}
      </td>
    </tr>
  );
}

/**
 * The compact arrangement for rows read alone (queues, admin lists): each item's link first, then
 * its secondary line, then a trailing action that never also opens the item.
 */
export function GroupedList({
  label,
  children,
}: {
  readonly label: string;
  readonly children: ReactNode;
}) {
  return (
    <ul
      aria-label={label}
      className="flex flex-col overflow-hidden rounded-panel border border-border-default bg-surface-default"
    >
      {children}
    </ul>
  );
}

export function GroupedListItem({
  primary,
  secondary,
  trailing,
}: {
  /** Usually the item's `RowLink`-style link. */
  readonly primary: ReactNode;
  readonly secondary?: ReactNode;
  readonly trailing?: ReactNode;
}) {
  const id = useId();
  return (
    <li
      aria-labelledby={id}
      className="flex min-h-size-touch-target-min items-start justify-between gap-inline-md border-b border-border-subtle px-inset-panel py-inset-table-cell-y last:border-b-0 hover:bg-surface-hover focus-within:bg-surface-hover"
    >
      <span id={id} className="flex min-w-0 flex-col gap-stack-xs">
        <span className="type-body-strong">{primary}</span>
        {secondary ? <span className="type-helper text-text-secondary">{secondary}</span> : null}
      </span>
      {trailing}
    </li>
  );
}
