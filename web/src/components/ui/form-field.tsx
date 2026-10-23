'use client';

import { createContext, useContext, useId, type ReactNode } from 'react';
import { cn } from '@/lib/cn';
import { ErrorIcon } from './icons';

// FormField (design/specs/components/form-field.md): a persistent label, helper, inline error and
// the programmatic links between them. Controls read their wiring from `useField()`, so any
// control composes inside a field without prop plumbing.

export interface FieldWiring {
  readonly id: string;
  /** Ids of the helper/reason and the inline error, in reading order. */
  readonly describedBy: string | undefined;
  readonly invalid: boolean;
  readonly disabled: boolean;
  readonly required: boolean;
}

const FieldContext = createContext<FieldWiring | null>(null);

/** The surrounding field's wiring, or null outside a field (the control then needs its own name). */
export function useField(): FieldWiring | null {
  return useContext(FieldContext);
}

export interface FormFieldProps {
  readonly label: ReactNode;
  readonly helper?: ReactNode;
  /** A client or unambiguous server message for this field (verbatim). Sets `aria-invalid`. */
  readonly error?: ReactNode;
  /** Marks the minority: most Lotero forms are short and mostly required. */
  readonly optional?: boolean;
  readonly required?: boolean;
  /** The whole field is unavailable: the label dims and the reason replaces the helper. */
  readonly disabledReason?: ReactNode;
  /** The screen tracks dirty fields: **Alterado** beside the label. */
  readonly changed?: boolean;
  readonly id?: string;
  readonly className?: string;
  readonly children: ReactNode;
}

export function FormField({
  label,
  helper,
  error,
  optional = false,
  required = false,
  disabledReason,
  changed = false,
  id,
  className,
  children,
}: FormFieldProps) {
  const generated = useId();
  const controlId = id ?? `${generated}-control`;
  const helperId = `${generated}-helper`;
  const errorId = `${generated}-error`;
  const disabled = disabledReason !== undefined && disabledReason !== null;
  const description = disabled ? disabledReason : helper;
  const invalid = error !== undefined && error !== null && error !== false;
  const describedBy =
    [description ? helperId : null, invalid ? errorId : null].filter(Boolean).join(' ') ||
    undefined;

  return (
    <FieldContext.Provider value={{ id: controlId, describedBy, invalid, disabled, required }}>
      <div className={cn('flex min-w-0 flex-col gap-stack-xs', className)}>
        <div className="flex flex-wrap items-baseline gap-x-inline-sm">
          <label
            htmlFor={controlId}
            className={cn('type-label', disabled ? 'text-text-disabled' : 'text-text-primary')}
          >
            {label}
            {optional ? <span className="type-helper text-text-muted"> (opcional)</span> : null}
          </label>
          {changed ? <span className="type-helper text-text-secondary">Alterado</span> : null}
        </div>
        {children}
        {description ? (
          <p id={helperId} className="type-helper text-text-muted">
            {description}
          </p>
        ) : null}
        {invalid ? (
          <p
            id={errorId}
            className="flex items-start gap-inline-xs type-helper text-feedback-danger-fg"
          >
            <LineIcon>
              <ErrorIcon />
            </LineIcon>
            <span>{error}</span>
          </p>
        ) : null}
      </div>
    </FieldContext.Provider>
  );
}

/**
 * A group of related choices with a legend instead of a label (payment method, status filter). The
 * group's helper and error are its description; its message, not color, carries the meaning.
 */
export function Fieldset({
  legend,
  helper,
  error,
  optional = false,
  className,
  children,
}: {
  readonly legend: ReactNode;
  readonly helper?: ReactNode;
  readonly error?: ReactNode;
  readonly optional?: boolean;
  readonly className?: string;
  readonly children: ReactNode;
}) {
  const generated = useId();
  const helperId = `${generated}-helper`;
  const errorId = `${generated}-error`;
  const invalid = error !== undefined && error !== null && error !== false;
  const describedBy =
    [helper ? helperId : null, invalid ? errorId : null].filter(Boolean).join(' ') || undefined;
  return (
    <fieldset
      aria-describedby={describedBy}
      aria-invalid={invalid || undefined}
      className={cn('flex min-w-0 flex-col gap-stack-sm', className)}
    >
      <legend className="type-label text-text-primary">
        {legend}
        {optional ? <span className="type-helper text-text-muted"> (opcional)</span> : null}
      </legend>
      {helper ? (
        <p id={helperId} className="type-helper text-text-muted">
          {helper}
        </p>
      ) : null}
      <div
        className={cn(
          'flex flex-col gap-size-touch-target-gap',
          invalid && 'rounded-control outline outline-1 outline-border-control-invalid',
        )}
      >
        {children}
      </div>
      {invalid ? (
        <p
          id={errorId}
          className="flex items-start gap-inline-xs type-helper text-feedback-danger-fg"
        >
          <LineIcon>
            <ErrorIcon />
          </LineIcon>
          <span>{error}</span>
        </p>
      ) : null}
    </fieldset>
  );
}

/**
 * A value that is real and final for this caller (locked period, cancelled row, generated
 * movement): selectable text on the muted surface, no control chrome, with the reason. It never
 * looks like a disabled input.
 */
export function ReadOnlyField({
  label,
  value,
  reason,
  align = 'start',
  className,
}: {
  readonly label: ReactNode;
  readonly value: ReactNode;
  readonly reason?: ReactNode;
  readonly align?: 'start' | 'end';
  readonly className?: string;
}) {
  const id = useId();
  return (
    <div
      role="group"
      aria-labelledby={id}
      className={cn('flex min-w-0 flex-col gap-stack-xs', className)}
    >
      <span id={id} className="type-label text-text-primary">
        {label}
      </span>
      <span
        className={cn(
          'min-h-size-control-height rounded-control bg-surface-muted px-inset-control-x py-2 type-control text-text-primary select-text',
          align === 'end' && 'text-end',
        )}
      >
        {value}
      </span>
      {reason ? <span className="type-helper text-text-muted">{reason}</span> : null}
    </div>
  );
}

/** Centers an icon on the first line of the text beside it, whatever the text size. */
export function LineIcon({ children }: { readonly children: ReactNode }) {
  return <span className="flex h-lh shrink-0 items-center">{children}</span>;
}

/** Fields in columns at least `spacing.form.column-min` wide; they stack when two do not fit. */
export function FormGrid({
  className,
  children,
}: {
  readonly className?: string;
  readonly children: ReactNode;
}) {
  return (
    <div
      className={cn(
        'grid gap-x-form-column-gap gap-y-form-row-gap',
        'grid-cols-[repeat(auto-fit,minmax(min(100%,var(--spacing-form-column-min)),1fr))]',
        className,
      )}
    >
      {children}
    </div>
  );
}
