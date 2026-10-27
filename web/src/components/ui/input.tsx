'use client';

import {
  useEffect,
  useId,
  useLayoutEffect,
  useRef,
  useState,
  type ComponentPropsWithoutRef,
  type ReactNode,
} from 'react';
import { cn } from '@/lib/cn';
import { useField } from './form-field';
import { useFormat, type IdentifierKind } from './format-port';
import { CloseIcon, EyeIcon, EyeOffIcon, SearchIcon } from './icons';

// Input (design/specs/components/input.md): text, e-mail, password, textarea, search and masked
// identifiers. Values are kept exactly as typed; trimming happens only where the contract trims.

/** The field chrome every text-like control shares (also used by Select, MoneyInput, DatePicker). */
export const controlChrome = cn(
  'w-full min-w-0 rounded-control border border-border-control bg-surface-default text-text-primary',
  'type-control px-inset-control-x min-h-size-control-height pointer-coarse:min-h-size-control-height-touch',
  'placeholder:text-text-placeholder hover:border-border-control-hover motion-control transition-colors',
  'aria-invalid:border-border-control-invalid aria-invalid:hover:border-border-control-invalid',
  'disabled:cursor-not-allowed disabled:border-action-disabled-border disabled:bg-action-disabled-bg disabled:text-action-disabled-fg',
);

/**
 * Merges the surrounding field's id, description and validity into a control's props. The field
 * only adds: a control disabled or required by its own props stays so, and a consumer's explicit
 * `aria-invalid` wins over the field's.
 */
export function useControlProps<
  P extends {
    id?: string;
    'aria-describedby'?: string;
    'aria-invalid'?: ComponentPropsWithoutRef<'input'>['aria-invalid'];
    disabled?: boolean;
    required?: boolean;
  },
>(props: P) {
  const field = useField();
  const describedBy =
    [field?.describedBy, props['aria-describedby']].filter(Boolean).join(' ') || undefined;
  return {
    ...props,
    id: props.id ?? field?.id,
    'aria-describedby': describedBy,
    'aria-invalid': props['aria-invalid'] ?? (field?.invalid || undefined),
    disabled: props.disabled || field?.disabled || undefined,
    required: props.required || field?.required || undefined,
  };
}

type InputProps = Omit<ComponentPropsWithoutRef<'input'>, 'type'> & {
  readonly type?: 'text' | 'email' | 'tel' | 'url';
  readonly ref?: React.Ref<HTMLInputElement>;
  /** A trailing action inside the field (clear, show password). */
  readonly trailing?: ReactNode;
  readonly leading?: ReactNode;
};

export function Input({ type = 'text', className, trailing, leading, ...props }: InputProps) {
  const control = useControlProps(props);
  const email = type === 'email';
  const input = (
    <input
      type={type}
      // E-mail: no auto-capitalization or correction; trimmed only at submit by the screen.
      autoCapitalize={email ? 'none' : props.autoCapitalize}
      autoCorrect={email ? 'off' : props.autoCorrect}
      spellCheck={email ? false : props.spellCheck}
      inputMode={email ? 'email' : props.inputMode}
      {...control}
      className={cn(
        controlChrome,
        leading ? 'ps-[calc(var(--spacing-inset-control-x)*2+var(--spacing-size-icon))]' : null,
        trailing ? 'pe-size-touch-target-min' : null,
        className,
      )}
    />
  );
  if (!leading && !trailing) return input;
  return (
    <div className="relative flex min-w-0 items-center">
      {leading ? (
        <span className="pointer-events-none absolute start-inset-control-x flex text-text-muted">
          {leading}
        </span>
      ) : null}
      {input}
      {trailing ? <span className="absolute end-0 flex">{trailing}</span> : null}
    </div>
  );
}

/** A trailing icon action inside a field, with a touch-sized hit area and a name. */
function FieldAction({
  label,
  pressed,
  onPress,
  children,
}: {
  readonly label: string;
  readonly pressed?: boolean;
  readonly onPress: () => void;
  readonly children: ReactNode;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      aria-pressed={pressed}
      onClick={onPress}
      className="flex size-size-touch-target-min items-center justify-center rounded-control text-action-ghost-fg hover:text-action-ghost-fg-hover hover:bg-action-ghost-bg-hover"
    >
      {children}
    </button>
  );
}

/**
 * Masked password with **Mostrar senha** / **Ocultar senha** (a pressed toggle). Paste and password
 * managers work. The value lives only in the active journey's memory; it never enters device
 * storage (the screen clears it per its authentication rule).
 */
export function PasswordInput({
  autoComplete,
  className,
  ...props
}: Omit<ComponentPropsWithoutRef<'input'>, 'type' | 'autoComplete'> & {
  readonly autoComplete: 'current-password' | 'new-password';
}) {
  const [shown, setShown] = useState(false);
  const control = useControlProps(props);
  return (
    <div className="relative flex min-w-0 items-center">
      <input
        type={shown ? 'text' : 'password'}
        autoComplete={autoComplete}
        autoCapitalize="none"
        autoCorrect="off"
        spellCheck={false}
        {...control}
        className={cn(controlChrome, 'pe-size-touch-target-min', className)}
      />
      <span className="absolute end-0 flex">
        <FieldAction
          label={shown ? 'Ocultar senha' : 'Mostrar senha'}
          pressed={shown}
          onPress={() => setShown((value) => !value)}
        >
          {shown ? <EyeOffIcon /> : <EyeIcon />}
        </FieldAction>
      </span>
    </div>
  );
}

/**
 * Multi-line text (Notes, reasons). It grows with its content up to a cap, then scrolls; near the
 * limit a **{n}/{max}** counter appears, linked as its description and announced politely.
 */
export function Textarea({
  maxLength,
  className,
  value,
  ...props
}: ComponentPropsWithoutRef<'textarea'> & { readonly value: string; readonly maxLength?: number }) {
  const counterId = useId();
  const near = maxLength !== undefined && value.length >= Math.floor(maxLength * 0.8);
  const control = useControlProps({
    ...props,
    'aria-describedby':
      [props['aria-describedby'], near ? counterId : null].filter(Boolean).join(' ') || undefined,
  });
  return (
    <div className="flex min-w-0 flex-col gap-stack-xs">
      <textarea
        {...control}
        value={value}
        maxLength={maxLength}
        className={cn(
          controlChrome,
          'min-h-[calc(5lh+var(--spacing-4))] max-h-[calc(15lh+var(--spacing-4))] resize-y py-2 [field-sizing:content] overscroll-auto',
          className,
        )}
      />
      {near ? (
        <span id={counterId} aria-live="polite" className="self-end type-helper text-text-muted">
          {value.length}/{maxLength}
        </span>
      ) : null}
    </div>
  );
}

/**
 * Local filtering of a complete collection only (Clients). The clear button empties the field and
 * returns focus to it; the results sentence (pre-formatted by the screen) is announced politely
 * once typing pauses.
 */
export function SearchInput({
  value,
  onValueChange,
  resultsAnnouncement,
  clearLabel = 'Limpar busca',
  className,
  ...props
}: Omit<ComponentPropsWithoutRef<'input'>, 'type' | 'value' | 'onChange'> & {
  readonly value: string;
  readonly onValueChange: (value: string) => void;
  /** e.g. **3 clientes encontrados**, formatted by the screen. */
  readonly resultsAnnouncement?: string;
  readonly clearLabel?: string;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [announced, setAnnounced] = useState('');
  useEffect(() => {
    // Announce after typing pauses, never on every keystroke.
    const timer = window.setTimeout(() => setAnnounced(resultsAnnouncement ?? ''), 600);
    return () => window.clearTimeout(timer);
  }, [resultsAnnouncement, value]);
  return (
    <div className="flex min-w-0 flex-col">
      <Input
        {...props}
        ref={inputRef}
        type="text"
        role="searchbox"
        value={value}
        onChange={(event) => onValueChange(event.target.value)}
        leading={<SearchIcon />}
        trailing={
          value.length > 0 ? (
            <FieldAction
              label={clearLabel}
              onPress={() => {
                onValueChange('');
                inputRef.current?.focus();
              }}
            >
              <CloseIcon />
            </FieldAction>
          ) : null
        }
        className={className}
      />
      <span role="status" aria-live="polite" className="sr-only">
        {announced}
      </span>
    </div>
  );
}

const isDigit = (char: string) => char >= '0' && char <= '9';

/** The separators each typing mask generates (formatting §6). */
const maskSeparators: Readonly<Record<IdentifierKind, string>> = {
  cpf: '.-',
  cnpj: './-',
  cep: '-',
  phone: '() -',
};
const digitCount = (text: string) => [...text].filter(isDigit).length;

/** The caret index just after the `digits`-th digit of `text` (0 → the start). */
function caretAfterDigits(text: string, digits: number): number {
  if (digits <= 0) return 0;
  let seen = 0;
  for (let index = 0; index < text.length; index += 1) {
    if (isDigit(text[index] as string)) seen += 1;
    if (seen === digits) return index + 1;
  }
  return text.length;
}

/**
 * CPF, CNPJ, phone or CEP (formatting §6). The input mask auto-punctuates as digits arrive
 * (`identifierInput` from the format port, `@loto/core`); text the mask cannot hold renders
 * verbatim, never truncated or padded. The caret stays after the same digit when punctuation is
 * added or moves, and deleting a punctuation mark deletes the digit beside it (otherwise the mask
 * would put it straight back). The screen submits `identifierDigits` where the contract expects
 * digits.
 */
export function IdentifierInput({
  kind,
  value,
  onValueChange,
  ...props
}: Omit<ComponentPropsWithoutRef<'input'>, 'type' | 'value' | 'onChange'> & {
  readonly kind: IdentifierKind;
  readonly value: string;
  readonly onValueChange: (value: string) => void;
}) {
  const format = useFormat();
  const input = useRef<HTMLInputElement>(null);
  // Digits before the caret to restore once the masked text renders (null: leave the caret alone).
  const caret = useRef<number | null>(null);

  useLayoutEffect(() => {
    const element = input.current;
    const digits = caret.current;
    caret.current = null;
    if (element === null || digits === null || document.activeElement !== element) return;
    const at = caretAfterDigits(element.value, digits);
    element.setSelectionRange(at, at);
  });

  return (
    <Input
      {...props}
      ref={input}
      inputMode={kind === 'cpf' || kind === 'cep' ? 'numeric' : kind === 'phone' ? 'tel' : 'text'}
      autoComplete={kind === 'phone' ? 'tel' : kind === 'cep' ? 'postal-code' : 'off'}
      value={value}
      onChange={(event) => {
        let raw = event.target.value;
        let at = event.target.selectionStart ?? raw.length;
        const inputType = (event.nativeEvent as InputEvent).inputType ?? '';
        // One generated mask separator was deleted: delete the adjacent digit instead, or the mask
        // would put the separator straight back. Only when the previous text was exactly the mask
        // of its digits — removing an invalid character or editing passthrough text (overlong,
        // letters) keeps every digit.
        const digitsOnly = (text: string) => [...text].filter(isDigit).join('');
        let removed = 0;
        while (removed < raw.length && raw[removed] === value[removed]) removed += 1;
        if (
          inputType.startsWith('delete') &&
          raw.length === value.length - 1 &&
          maskSeparators[kind].includes(value[removed] ?? '') &&
          format.identifierInput(kind, digitsOnly(value)) === value
        ) {
          const forward = inputType === 'deleteContentForward';
          let index = -1;
          if (forward) {
            for (let i = at; i < raw.length; i += 1)
              if (isDigit(raw[i] as string)) {
                index = i;
                break;
              }
          } else {
            for (let i = at - 1; i >= 0; i -= 1)
              if (isDigit(raw[i] as string)) {
                index = i;
                break;
              }
          }
          if (index >= 0) {
            raw = raw.slice(0, index) + raw.slice(index + 1);
            if (!forward) at = index;
          }
        }
        const next = format.identifierInput(kind, raw);
        if (next !== event.target.value) caret.current = digitCount(raw.slice(0, at));
        onValueChange(next);
      }}
    />
  );
}
