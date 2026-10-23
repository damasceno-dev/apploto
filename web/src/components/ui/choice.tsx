'use client';

import {
  createContext,
  useContext,
  useEffect,
  useId,
  useRef,
  type ComponentPropsWithoutRef,
  type ReactNode,
} from 'react';
import { cn } from '@/lib/cn';
import { Fieldset } from './form-field';
import { CheckIcon, DashIcon } from './icons';

// Checkbox, Radio and Switch (design/specs/components/choice-controls.md). Native inputs; the whole
// label row is the hit area (at least the touch minimum). On/selected uses `sem.selection.indicator`
// — it keeps 3:1 on selected and hovered rows where the primary fill would not.

function ChoiceRow({
  control,
  label,
  description,
  disabled,
  className,
}: {
  readonly control: ReactNode;
  readonly label: ReactNode;
  readonly description?: ReactNode;
  readonly disabled?: boolean;
  readonly className?: string;
}) {
  return (
    <label
      className={cn(
        'group/choice flex min-h-size-touch-target-min cursor-pointer items-start gap-inline-md py-[calc((var(--spacing-size-touch-target-min)-1lh)/2)]',
        disabled && 'cursor-not-allowed',
        className,
      )}
    >
      <span className="flex h-lh shrink-0 items-center">{control}</span>
      <span className="flex min-w-0 flex-col">
        <span className={cn('type-control', disabled ? 'text-text-disabled' : 'text-text-primary')}>
          {label}
        </span>
        {description ? <span className="type-helper text-text-muted">{description}</span> : null}
      </span>
    </label>
  );
}

const box = cn(
  'peer relative size-[max(1rem,0.75lh)] shrink-0 cursor-pointer appearance-none border border-border-control bg-surface-default',
  'hover:border-border-control-hover motion-control transition-colors',
  'checked:border-selection-indicator checked:bg-selection-indicator',
  'aria-invalid:border-border-control-invalid',
  'disabled:cursor-not-allowed disabled:border-action-disabled-border disabled:bg-action-disabled-bg',
);

export type CheckboxProps = Omit<ComponentPropsWithoutRef<'input'>, 'type'> & {
  readonly label: ReactNode;
  readonly description?: ReactNode;
  /** Group selection ("all remaining installments"): a bar mark instead of the check. */
  readonly indeterminate?: boolean;
};

export function Checkbox({
  label,
  description,
  indeterminate = false,
  className,
  ...props
}: CheckboxProps) {
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (input.current) input.current.indeterminate = indeterminate;
  }, [indeterminate]);
  return (
    <ChoiceRow
      label={label}
      description={description}
      disabled={props.disabled}
      className={className}
      control={
        <span className="relative flex">
          <input
            ref={input}
            type="checkbox"
            {...props}
            aria-checked={indeterminate ? 'mixed' : undefined}
            className={cn(
              box,
              'rounded-xs',
              indeterminate && 'border-selection-indicator bg-selection-indicator',
            )}
          />
          <span className="pointer-events-none absolute inset-0 hidden items-center justify-center text-selection-indicator-fg peer-checked:flex">
            {indeterminate ? null : <CheckIcon />}
          </span>
          {indeterminate ? (
            <span className="pointer-events-none absolute inset-0 flex items-center justify-center text-selection-indicator-fg">
              <DashIcon />
            </span>
          ) : null}
        </span>
      }
    />
  );
}

const RadioContext = createContext<{
  readonly name: string;
  readonly value: string | null;
  readonly onValueChange: (value: string) => void;
  readonly invalid: boolean;
} | null>(null);

/**
 * A single choice with a legend. `value` starts `null` where the catalog wants a deliberate answer
 * (`HandsOutCountedStock`, the payment method): nothing is preselected and an unanswered group sends
 * nothing. Arrow keys move within the group (native radios).
 */
export function RadioGroup({
  legend,
  helper,
  error,
  value,
  onValueChange,
  name,
  children,
  className,
}: {
  readonly legend: ReactNode;
  readonly helper?: ReactNode;
  readonly error?: ReactNode;
  readonly value: string | null;
  readonly onValueChange: (value: string) => void;
  readonly name?: string;
  readonly children: ReactNode;
  readonly className?: string;
}) {
  const generated = useId();
  return (
    <RadioContext.Provider
      value={{ name: name ?? generated, value, onValueChange, invalid: Boolean(error) }}
    >
      <Fieldset legend={legend} helper={helper} error={error} className={className}>
        {children}
      </Fieldset>
    </RadioContext.Provider>
  );
}

export function Radio({
  value,
  label,
  description,
  disabled,
}: {
  readonly value: string;
  readonly label: ReactNode;
  readonly description?: ReactNode;
  readonly disabled?: boolean;
}) {
  const group = useContext(RadioContext);
  if (group === null) throw new Error('Radio must be inside RadioGroup.');
  return (
    <ChoiceRow
      label={label}
      description={description}
      disabled={disabled}
      control={
        <span className="relative flex">
          <input
            type="radio"
            name={group.name}
            value={value}
            checked={group.value === value}
            disabled={disabled}
            data-invalid={group.invalid || undefined}
            onChange={() => group.onValueChange(value)}
            className={cn(
              box,
              'rounded-full checked:border-2 checked:bg-surface-default',
              'data-invalid:border-border-control-invalid',
            )}
          />
          <span className="pointer-events-none absolute inset-[30%] hidden rounded-full bg-selection-indicator peer-checked:block" />
        </span>
      }
    />
  );
}

/**
 * An immediately applied, reversible device preference (never a financial or permission change).
 * The state is also written (**Ativado** / **Desativado**), not carried by color alone.
 */
export function Switch({
  checked,
  onCheckedChange,
  label,
  description,
  disabled = false,
  onLabel = 'Ativado',
  offLabel = 'Desativado',
}: {
  readonly checked: boolean;
  readonly onCheckedChange: (checked: boolean) => void;
  readonly label: ReactNode;
  readonly description?: ReactNode;
  readonly disabled?: boolean;
  readonly onLabel?: string;
  readonly offLabel?: string;
}) {
  const labelId = useId();
  const descriptionId = useId();
  return (
    <div className="flex min-h-size-touch-target-min items-start justify-between gap-inline-md py-1">
      <span className="flex min-w-0 flex-col">
        <span id={labelId} className="type-control text-text-primary">
          {label}
        </span>
        {description ? (
          <span id={descriptionId} className="type-helper text-text-muted">
            {description}
          </span>
        ) : null}
      </span>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        aria-labelledby={labelId}
        aria-describedby={description ? descriptionId : undefined}
        disabled={disabled}
        onClick={() => onCheckedChange(!checked)}
        className="flex min-h-size-touch-target-min shrink-0 cursor-pointer items-center gap-inline-sm disabled:cursor-not-allowed"
      >
        <span className="type-helper text-text-secondary" aria-hidden="true">
          {checked ? onLabel : offLabel}
        </span>
        <span
          aria-hidden="true"
          className={cn(
            'relative inline-flex h-6 w-10 items-center rounded-full motion-control transition-colors',
            checked ? 'bg-selection-indicator' : 'bg-border-control',
            disabled && 'bg-action-disabled-border',
          )}
        >
          <span
            className={cn(
              'absolute size-5 rounded-full motion-control transition-[inset-inline-start]',
              checked
                ? 'start-[calc(100%-1.375rem)] bg-selection-indicator-fg'
                : 'start-[calc(var(--spacing-1)/2)] bg-surface-default',
            )}
          />
        </span>
      </button>
    </div>
  );
}
