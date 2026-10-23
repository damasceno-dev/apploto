'use client';

import {
  Fragment,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type ComponentPropsWithoutRef,
  type KeyboardEvent,
  type ReactNode,
} from 'react';
import { cn } from '@/lib/cn';
import { Button } from './button';
import { useField } from './form-field';
import { CheckIcon, ChevronDownIcon, SearchIcon } from './icons';
import { controlChrome, useControlProps } from './input';
import { Dialog, useCompactOutsideDialog } from './dialog';
import { foldText, Popover } from './popover';

// Select (design/specs/components/select.md): native for short static lists, a listbox for rich
// options, a searchable combobox for long complete lists (local filtering only — never over one
// page of a paged endpoint), and the no-default form (**Escolha…**). Options arrive already scoped
// by role; excluded values are absent, never disabled.

export interface SelectOption {
  readonly value: string;
  readonly label: string;
  readonly description?: string;
  readonly group?: string;
  /** Only when an explicit server activity field says so: **(inativo)**. */
  readonly inactive?: boolean;
}

/** The options' load state: the form never treats a failed load as an empty field. */
export type OptionsStatus =
  | { readonly kind: 'ready' }
  | { readonly kind: 'loading' }
  | { readonly kind: 'failed'; readonly retry: () => void };

const ready: OptionsStatus = { kind: 'ready' };
const outsideOptions = ' (fora das opções atuais)';
export const noDefaultPlaceholder = 'Escolha…';

function optionLabel(option: SelectOption): string {
  return option.inactive ? `${option.label} (inativo)` : option.label;
}

/** A styled native `<select>` for short static lists (status filter, role, page size). */
export function NativeSelect({
  options,
  className,
  ...props
}: Omit<ComponentPropsWithoutRef<'select'>, 'children'> & {
  readonly options: readonly SelectOption[];
}) {
  const control = useControlProps(props);
  return (
    <div className="relative flex min-w-0 items-center">
      <select {...control} className={cn(controlChrome, 'appearance-none pe-10', className)}>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {optionLabel(option)}
          </option>
        ))}
      </select>
      <ChevronDownIcon className="pointer-events-none absolute end-inset-control-x text-text-muted" />
    </div>
  );
}

interface ListboxBaseProps {
  /** The field cannot be used now: it neither opens nor changes value, by any path. */
  readonly disabled?: boolean;
  readonly options: readonly SelectOption[];
  readonly value: string | null;
  readonly onValueChange: (value: string) => void;
  readonly status?: OptionsStatus;
  /** Why the list is empty (**Nenhuma conta disponível para você**). */
  readonly emptyMessage?: ReactNode;
  /** A carried-over value missing from the current options: its own label (shown with a marker). */
  readonly missingLabel?: string;
  /** No default: the trigger reads **Escolha…** until the user chooses. */
  readonly placeholder?: string;
  readonly id?: string;
  readonly className?: string;
  readonly 'aria-describedby'?: string;
}

function useOptionsView(
  options: readonly SelectOption[],
  value: string | null,
  missingLabel?: string,
) {
  const selected = options.find((option) => option.value === value) ?? null;
  const shown =
    selected !== null
      ? optionLabel(selected)
      : value !== null && missingLabel !== undefined
        ? `${missingLabel}${outsideOptions}`
        : null;
  return { selected, shown };
}

/** The loading / failed / empty presentations shared by both list selects. */
function OptionsState({
  status,
  emptyMessage,
}: {
  readonly status: OptionsStatus;
  readonly emptyMessage?: ReactNode;
}) {
  if (status.kind === 'failed') {
    return (
      <div className="flex flex-wrap items-center gap-inline-md type-helper text-feedback-danger-fg">
        <Button variant="text" size="small" onClick={status.retry}>
          Tentar novamente
        </Button>
      </div>
    );
  }
  return emptyMessage ? <p className="type-helper text-text-muted">{emptyMessage}</p> : null;
}

function ListboxOptions({
  id,
  options,
  selectedValue,
  activeIndex,
  optionId,
  onChoose,
  onActivate,
}: {
  readonly id: string;
  readonly options: readonly SelectOption[];
  readonly selectedValue: string | null;
  readonly activeIndex: number;
  readonly optionId: (index: number) => string;
  readonly onChoose: (option: SelectOption) => void;
  readonly onActivate: (index: number) => void;
}) {
  const activeRef = useRef<HTMLLIElement | null>(null);
  useEffect(() => {
    activeRef.current?.scrollIntoView?.({ block: 'nearest' });
  }, [activeIndex]);
  return (
    <ul id={id} role="listbox" className="py-1" tabIndex={-1}>
      {options.map((option, index) => {
        const heading =
          option.group !== undefined && option.group !== options[index - 1]?.group
            ? option.group
            : null;
        const selected = option.value === selectedValue;
        const active = index === activeIndex;
        return (
          <Fragment key={option.value}>
            {heading ? (
              <li
                role="presentation"
                className="px-inset-control-x pt-2 pb-1 type-overline text-text-muted"
              >
                {heading}
              </li>
            ) : null}
            <li
              id={optionId(index)}
              ref={active ? activeRef : undefined}
              role="option"
              aria-selected={selected}
              onPointerDown={(event) => event.preventDefault()}
              onPointerMove={() => onActivate(index)}
              onClick={() => onChoose(option)}
              className={cn(
                'flex min-h-size-touch-target-min cursor-pointer items-center gap-inline-sm px-inset-control-x py-1',
                selected && 'bg-surface-selected',
                active && 'bg-surface-hover outline-2 -outline-offset-2 outline-focus-ring',
                active &&
                  selected &&
                  '[background-image:linear-gradient(var(--loto-sem-surface-selected),var(--loto-sem-surface-selected))]',
              )}
            >
              <span className="flex w-size-icon shrink-0">{selected ? <CheckIcon /> : null}</span>
              <span className="flex min-w-0 flex-col">
                <span className="type-control">{optionLabel(option)}</span>
                {option.description ? (
                  <span className="type-helper text-text-muted">{option.description}</span>
                ) : null}
              </span>
            </li>
          </Fragment>
        );
      })}
    </ul>
  );
}

function typeAheadMatch(options: readonly SelectOption[], query: string, from: number): number {
  const folded = foldText(query);
  for (let step = 1; step <= options.length; step += 1) {
    const index = (from + step) % options.length;
    const option = options[index];
    if (option !== undefined && foldText(option.label).startsWith(folded)) return index;
  }
  return -1;
}

/**
 * A select-only combobox with a listbox (rich options: account with type and number, type with
 * category). Focus stays on the trigger; arrows, Home/End, Page Up/Down and type-ahead move the
 * active option; Enter or Space chooses; Escape closes and keeps the value.
 */
export function Select({
  options,
  value,
  onValueChange,
  status = ready,
  disabled = false,
  emptyMessage,
  missingLabel,
  placeholder,
  id,
  className,
  ...props
}: ListboxBaseProps) {
  const field = useField();
  const generated = useId();
  const triggerId = id ?? field?.id ?? `${generated}-trigger`;
  const listId = `${generated}-list`;
  const optionId = (index: number) => `${generated}-option-${index}`;
  const trigger = useRef<HTMLButtonElement>(null);
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const typed = useRef({ text: '', at: 0 });
  const { selected, shown } = useOptionsView(options, value, missingLabel);
  const usable = status.kind === 'ready' && options.length > 0;
  // A disabled field (its own prop or its FormField) is closed to every opening and choice path.
  const available = usable && !disabled && !field?.disabled;
  const expanded = open && available;
  const describedBy =
    [field?.describedBy, props['aria-describedby']].filter(Boolean).join(' ') || undefined;

  const openAt = (index: number) => {
    if (!available) return;
    setActive(Math.max(0, Math.min(index, options.length - 1)));
    setOpen(true);
  };
  const close = () => {
    setOpen(false);
    trigger.current?.focus();
  };
  const choose = (option: SelectOption) => {
    if (!available) return;
    onValueChange(option.value);
    close();
  };
  const selectedIndex = selected === null ? -1 : options.indexOf(selected);

  const onKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    const key = event.key;
    if (!expanded) {
      if (['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(key)) {
        event.preventDefault();
        openAt(selectedIndex >= 0 ? selectedIndex : key === 'ArrowUp' ? options.length - 1 : 0);
      } else if (key === 'Home' || key === 'End') {
        event.preventDefault();
        openAt(key === 'Home' ? 0 : options.length - 1);
      } else if (key.length === 1 && !event.altKey && !event.ctrlKey && !event.metaKey) {
        const match = typeAheadMatch(options, key, selectedIndex);
        if (match >= 0) openAt(match);
      }
      return;
    }
    const last = options.length - 1;
    const move = (index: number) => {
      event.preventDefault();
      setActive(Math.max(0, Math.min(index, last)));
    };
    if (key === 'ArrowDown') move(active + 1);
    else if (key === 'ArrowUp') move(active - 1);
    else if (key === 'Home') move(0);
    else if (key === 'End') move(last);
    else if (key === 'PageDown') move(active + 10);
    else if (key === 'PageUp') move(active - 10);
    else if (key === 'Enter' || key === ' ') {
      event.preventDefault();
      const option = options[active];
      if (option !== undefined) choose(option);
    } else if (key === 'Escape') {
      event.preventDefault();
      close();
    } else if (key === 'Tab') {
      setOpen(false);
    } else if (key.length === 1 && !event.altKey && !event.ctrlKey && !event.metaKey) {
      const now = event.timeStamp;
      typed.current = {
        text: now - typed.current.at < 500 ? typed.current.text + key : key,
        at: now,
      };
      const match = typeAheadMatch(
        options,
        typed.current.text,
        typed.current.text.length > 1 ? active - 1 : active,
      );
      if (match >= 0) setActive(match);
    }
  };

  const triggerText =
    status.kind === 'loading'
      ? 'Carregando…'
      : status.kind === 'failed'
        ? 'Não foi possível carregar as opções'
        : (shown ?? placeholder ?? noDefaultPlaceholder);

  return (
    <div className={cn('flex min-w-0 flex-col gap-stack-xs', className)}>
      <button
        ref={trigger}
        id={triggerId}
        type="button"
        role="combobox"
        aria-haspopup="listbox"
        aria-expanded={expanded}
        aria-controls={expanded ? listId : undefined}
        aria-activedescendant={expanded && active >= 0 ? optionId(active) : undefined}
        aria-describedby={describedBy}
        aria-invalid={field?.invalid || undefined}
        aria-disabled={!available || undefined}
        onClick={() => (expanded ? close() : openAt(selectedIndex >= 0 ? selectedIndex : 0))}
        onKeyDown={onKeyDown}
        className={cn(
          controlChrome,
          'flex items-center justify-between gap-inline-sm text-start',
          shown === null && 'text-text-placeholder',
          !available && 'cursor-not-allowed',
        )}
      >
        <span className="min-w-0 py-1 [overflow-wrap:anywhere]">{triggerText}</span>
        <ChevronDownIcon className="text-text-muted" />
      </button>
      {status.kind !== 'ready' || options.length === 0 ? (
        <OptionsState status={status} emptyMessage={emptyMessage} />
      ) : null}
      <Popover open={expanded} anchor={trigger} onDismiss={() => setOpen(false)} matchWidth>
        <ListboxOptions
          id={listId}
          options={options}
          selectedValue={value}
          activeIndex={active}
          optionId={optionId}
          onChoose={choose}
          onActivate={setActive}
        />
      </Popover>
    </div>
  );
}

/**
 * A long list loaded in full (clients, types), filtered locally as the user types — never a
 * pretend search over one page. The result count is announced politely with the screen's sentence.
 */
export function SearchableSelect({
  options,
  value,
  onValueChange,
  status = ready,
  disabled = false,
  emptyMessage,
  missingLabel,
  placeholder,
  resultsLabel,
  noMatchesLabel = 'Nenhuma opção encontrada',
  sheetTitle = 'Escolha uma opção',
  id,
  className,
  ...props
}: ListboxBaseProps & {
  /** The announced count sentence (**12 clientes**), formatted by the screen (`@loto/core`). */
  readonly resultsLabel: (count: number) => string;
  readonly noMatchesLabel?: string;
  /** The bottom sheet's title on compact layouts (usually the field's label). */
  readonly sheetTitle?: string;
}) {
  // Inside a dialog the list stays in the popover layer: never a second modal sheet.
  const compact = useCompactOutsideDialog();
  const sheetTrigger = useRef<HTMLButtonElement>(null);
  const field = useField();
  const generated = useId();
  const inputId = id ?? field?.id ?? `${generated}-input`;
  const listId = `${generated}-list`;
  const optionId = (index: number) => `${generated}-option-${index}`;
  const anchor = useRef<HTMLDivElement>(null);
  const { shown } = useOptionsView(options, value, missingLabel);
  const [query, setQuery] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const usable = status.kind === 'ready' && options.length > 0;
  // A disabled field (its own prop or its FormField) is closed to every opening and choice path.
  const available = usable && !disabled && !field?.disabled;
  const expanded = open && available;
  const filtered = useMemo(() => {
    if (query === null || query.trim() === '') return options;
    const folded = foldText(query.trim());
    return options.filter((option) =>
      foldText(`${option.label} ${option.description ?? ''}`).includes(folded),
    );
  }, [options, query]);
  const [announcement, setAnnouncement] = useState('');
  useEffect(() => {
    if (!open || query === null) return;
    const timer = window.setTimeout(
      () => setAnnouncement(filtered.length === 0 ? noMatchesLabel : resultsLabel(filtered.length)),
      500,
    );
    return () => window.clearTimeout(timer);
  }, [open, query, filtered.length, noMatchesLabel, resultsLabel]);

  const choose = (option: SelectOption) => {
    if (!available) return;
    onValueChange(option.value);
    setQuery(null);
    setOpen(false);
    if (compact) sheetTrigger.current?.focus();
  };
  const describedBy =
    [field?.describedBy, props['aria-describedby']].filter(Boolean).join(' ') || undefined;

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    const last = filtered.length - 1;
    const move = (index: number) => {
      event.preventDefault();
      setOpen(true);
      setActive(Math.max(0, Math.min(index, last)));
    };
    if (event.key === 'ArrowDown') move(expanded ? active + 1 : 0);
    else if (event.key === 'ArrowUp') move(expanded ? active - 1 : last);
    else if (event.key === 'Enter' && expanded) {
      event.preventDefault();
      const option = filtered[active];
      if (option !== undefined) choose(option);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      setOpen(false);
      setQuery(null);
    }
  };

  const searchBox = (sheet: boolean) => (
    <div ref={sheet ? undefined : anchor} className="relative flex min-w-0 items-center">
      <SearchIcon className="pointer-events-none absolute start-inset-control-x text-text-muted" />
      <input
        id={sheet ? `${inputId}-search` : inputId}
        type="text"
        role="combobox"
        aria-label={sheet ? sheetTitle : undefined}
        aria-autocomplete="list"
        aria-expanded={expanded}
        aria-controls={expanded ? listId : undefined}
        aria-activedescendant={expanded && filtered.length > 0 ? optionId(active) : undefined}
        aria-describedby={sheet ? undefined : describedBy}
        aria-invalid={sheet ? undefined : field?.invalid || undefined}
        disabled={!available || undefined}
        autoComplete="off"
        placeholder={
          status.kind === 'loading' ? 'Carregando…' : (placeholder ?? noDefaultPlaceholder)
        }
        value={sheet ? (query ?? '') : (query ?? shown ?? '')}
        onChange={(event) => {
          setQuery(event.target.value);
          setActive(0);
          setOpen(true);
        }}
        onFocus={() => setOpen(available)}
        onBlur={() => {
          if (sheet) return;
          setOpen(false);
          setQuery(null);
        }}
        onKeyDown={onKeyDown}
        className={cn(
          controlChrome,
          'ps-[calc(var(--spacing-inset-control-x)*2+var(--spacing-size-icon))]',
        )}
      />
    </div>
  );
  const list =
    filtered.length === 0 ? (
      <p className="px-inset-control-x py-2 type-helper text-text-muted">{noMatchesLabel}</p>
    ) : (
      <ListboxOptions
        id={listId}
        options={filtered}
        selectedValue={value}
        activeIndex={active}
        optionId={optionId}
        onChoose={choose}
        onActivate={setActive}
      />
    );

  return (
    <div className={cn('flex min-w-0 flex-col gap-stack-xs', className)}>
      {compact ? (
        // Compact layouts: a long list opens in a bottom sheet with the search pinned at the top.
        <button
          ref={sheetTrigger}
          id={inputId}
          type="button"
          aria-haspopup="dialog"
          aria-describedby={describedBy}
          data-invalid={field?.invalid || undefined}
          aria-disabled={!available || undefined}
          onClick={() => {
            if (!available) return;
            setQuery('');
            setOpen(true);
          }}
          className={cn(
            controlChrome,
            'flex items-center justify-between gap-inline-sm text-start',
            'data-invalid:border-border-control-invalid',
            shown === null && 'text-text-placeholder',
          )}
        >
          <span className="min-w-0 py-1 [overflow-wrap:anywhere]">
            {status.kind === 'loading'
              ? 'Carregando…'
              : (shown ?? placeholder ?? noDefaultPlaceholder)}
          </span>
          <ChevronDownIcon className="text-text-muted" />
        </button>
      ) : (
        searchBox(false)
      )}
      {status.kind !== 'ready' || options.length === 0 ? (
        <OptionsState status={status} emptyMessage={emptyMessage} />
      ) : null}
      <span role="status" aria-live="polite" className="sr-only">
        {announcement}
      </span>
      {compact ? (
        <Dialog
          open={expanded}
          onDismiss={() => {
            setOpen(false);
            setQuery(null);
          }}
          title={sheetTitle}
          variant="sheet"
        >
          <div className="sticky top-0 bg-surface-raised pb-stack-sm">{searchBox(true)}</div>
          {list}
        </Dialog>
      ) : (
        <Popover open={expanded} anchor={anchor} onDismiss={() => setOpen(false)} matchWidth>
          {list}
        </Popover>
      )}
    </div>
  );
}
