'use client';

import {
  useEffect,
  useId,
  useLayoutEffect,
  useRef,
  type ClipboardEvent,
  type ComponentPropsWithoutRef,
  type ReactNode,
} from 'react';
import { cn } from '@/lib/cn';
import { useFormat } from './format-port';
import { controlChrome, useControlProps } from './input';

// MoneyInput (design/specs/components/money-input.md). The value is the exact typed state: blank,
// or the amount's integer cents as a **digit string** (`'1234'` is R$ 12,34; `'0'` a recorded
// zero). The component never does arithmetic on money and never parses or formats it itself: the
// display, the spoken value and paste parsing come from the format port (`@loto/core`). Turning the
// digits into a number for a payload is the screen's (core's integer-cents helpers).

export type MoneyValue =
  { readonly kind: 'blank' } | { readonly kind: 'amount'; readonly digits: string };

export const blankMoney: MoneyValue = Object.freeze({ kind: 'blank' });

export type MoneyChangeSource = 'typing' | 'paste' | 'clear';

/** Leading zeros carry nothing: `'005'` is 5 cents; `'0'` (a typed zero) stays a recorded zero. */
export function normalizeDigits(digits: string): string {
  const trimmed = digits.replace(/^0+/, '');
  return trimmed.length === 0 ? '0' : trimmed;
}

export interface MoneyInputProps extends Omit<
  ComponentPropsWithoutRef<'input'>,
  'value' | 'defaultValue' | 'onChange' | 'type' | 'inputMode'
> {
  readonly value: MoneyValue;
  readonly onValueChange: (value: MoneyValue, source: MoneyChangeSource) => void;
  /** `count`: a Close day product row, where blank (**não informado**) and zero differ. */
  readonly variant?: 'amount' | 'count';
}

/**
 * Typing digits builds the value right to left in cents (`1234` → R$ 12,34); clearing returns to
 * blank, never zero; typing `0` records a zero. The cursor stays at the end. `R$` is a fixed
 * adornment hidden from assistive technology; the spoken amount (or **não informado**) is the
 * field's description.
 */
export function MoneyInput({
  value,
  onValueChange,
  variant = 'amount',
  className,
  ...props
}: MoneyInputProps) {
  const format = useFormat();
  const spokenId = useId();
  const input = useRef<HTMLInputElement>(null);
  const control = useControlProps({
    ...props,
    'aria-describedby': [props['aria-describedby'], spokenId].filter(Boolean).join(' '),
  });
  const display = value.kind === 'blank' ? '' : format.moneyDigits(value.digits);

  // Keep the caret at the end after every re-format (separators move as digits are added).
  useLayoutEffect(() => {
    const element = input.current;
    if (element !== null && document.activeElement === element) {
      element.setSelectionRange(display.length, display.length);
    }
  }, [display]);

  // Edits are applied to the digit string, right to left: a typed digit is appended (or starts over
  // when the whole value was selected); any deletion removes the last digit, and removing the last
  // one returns to blank. Native `beforeinput` carries the intent; `onChange` is the fallback.
  const latest = useRef({ value, onValueChange });
  useEffect(() => {
    latest.current = { value, onValueChange };
  });
  useEffect(() => {
    const element = input.current;
    if (element === null) return;
    const onBeforeInput = (event: InputEvent) => {
      if (event.inputType === 'insertFromPaste' || event.inputType === 'insertFromDrop') return;
      const { value: current, onValueChange: emit } = latest.current;
      const whole =
        element.value.length > 0 &&
        element.selectionStart === 0 &&
        element.selectionEnd === element.value.length;
      if (event.inputType.startsWith('insert')) {
        event.preventDefault();
        const typed = (event.data ?? '').replace(/\D/g, '');
        if (typed.length === 0) return;
        const base = whole || current.kind === 'blank' ? '' : current.digits;
        emit({ kind: 'amount', digits: normalizeDigits(base + typed) }, 'typing');
      } else if (event.inputType.startsWith('delete')) {
        event.preventDefault();
        if (current.kind === 'blank') return;
        const rest = whole ? '' : current.digits.slice(0, -1);
        emit(
          rest.length === 0 ? blankMoney : { kind: 'amount', digits: rest },
          rest.length === 0 ? 'clear' : 'typing',
        );
      }
    };
    element.addEventListener('beforeinput', onBeforeInput);
    return () => element.removeEventListener('beforeinput', onBeforeInput);
  }, []);

  const accept = (text: string) => {
    const digits = text.replace(/\D/g, '');
    if (digits.length === 0) {
      if (value.kind !== 'blank') onValueChange(blankMoney, 'clear');
      return;
    }
    const next = normalizeDigits(digits);
    if (value.kind === 'amount' && value.digits === next) return;
    onValueChange({ kind: 'amount', digits: next }, 'typing');
  };

  const paste = (event: ClipboardEvent<HTMLInputElement>) => {
    event.preventDefault();
    const digits = format.moneyFromText(event.clipboardData.getData('text'));
    // Text that is not a BRL amount changes nothing (no guessed value).
    if (digits !== null)
      onValueChange({ kind: 'amount', digits: normalizeDigits(digits) }, 'paste');
  };

  return (
    <div className="flex min-w-0 flex-col">
      <div className="relative flex min-w-[min(100%,12em)] items-center">
        <span
          aria-hidden="true"
          className="pointer-events-none absolute start-inset-control-x type-money text-text-muted"
        >
          R$
        </span>
        <input
          {...control}
          ref={input}
          type="text"
          inputMode="numeric"
          autoComplete="off"
          value={display}
          placeholder={
            variant === 'count' || props.placeholder === undefined
              ? 'não informado'
              : props.placeholder
          }
          onChange={(event) => accept(event.target.value)}
          onPaste={paste}
          className={cn(
            controlChrome,
            'ps-[calc(var(--spacing-inset-control-x)+2.25em)] text-end type-money',
            className,
          )}
        />
      </div>
      <span id={spokenId} className="sr-only">
        {value.kind === 'blank' ? 'não informado' : format.moneySpoken(value.digits)}
      </span>
    </div>
  );
}

/**
 * A system-calculated or opening amount: plain right-aligned text with its provenance label
 * (**calculado pelo sistema** / **informado pelo operador**), never a disabled input. The amount
 * arrives formatted by the screen (`@loto/core`).
 */
export function ReadOnlyAmount({
  amount,
  provenance,
  className,
}: {
  readonly amount: ReactNode;
  readonly provenance?: ReactNode;
  readonly className?: string;
}) {
  return (
    <span className={cn('inline-flex flex-col items-end gap-stack-xs', className)}>
      <span className="type-money text-text-primary">{amount}</span>
      {provenance ? <span className="type-helper text-text-muted">{provenance}</span> : null}
    </span>
  );
}
