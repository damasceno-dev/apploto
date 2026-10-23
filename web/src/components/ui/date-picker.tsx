'use client';

import {
  useEffect,
  useId,
  useRef,
  useState,
  type ComponentPropsWithoutRef,
  type KeyboardEvent,
  type ReactNode,
} from 'react';
import { cn } from '@/lib/cn';
import { Button } from './button';
import {
  addDays,
  addMonths,
  compareDates,
  isWallClockTime,
  monthWeeks,
  partsOf,
  weekdayOf,
} from './calendar';
import { Dialog, useCompactOutsideDialog } from './dialog';
import { useField } from './form-field';
import { useFormat, type CalendarDate } from './format-port';
import { CalendarIcon, ChevronLeftIcon, ChevronRightIcon, ClockIcon } from './icons';
import { controlChrome, useControlProps } from './input';
import { Popover } from './popover';

// DatePicker (design/specs/components/date-picker.md). Typing is always possible; the calendar is a
// convenience. Values are `YYYY-MM-DD` calendar strings — never a JS `Date` — and "today" is the
// branch clock's date passed in by the screen, never the device clock. Display text, names and
// parsing come from the format port (`@loto/core`). Bounds in the calendar are hints: the server's
// verbatim message decides.

export const invalidDateMessage = 'Data inválida. Use dd/mm/aaaa.';

interface CalendarProps {
  /** The month shown when opening: the value's, else today's. */
  readonly initial: CalendarDate;
  readonly selected?: CalendarDate | null;
  /** A range being shown (De/Até). */
  readonly range?: { readonly from: CalendarDate | null; readonly to: CalendarDate | null };
  /** From `GET /branch/current`; marked **hoje**. */
  readonly today?: CalendarDate | null;
  readonly min?: CalendarDate | null;
  readonly max?: CalendarDate | null;
  readonly onSelect: (date: CalendarDate) => void;
  readonly onClose: () => void;
  readonly labelledBy?: string;
}

function outside(date: CalendarDate, min?: CalendarDate | null, max?: CalendarDate | null) {
  return (
    (min != null && compareDates(date, min) < 0) || (max != null && compareDates(date, max) > 0)
  );
}

/**
 * The month grid: arrows move a day or a week, Page Up/Down a month (with Shift a year), Home/End
 * the week's edges, Enter/Space choose, Escape closes and returns focus. Day buttons carry their
 * full name (**quinta-feira, 24 de setembro de 2026**) and today reads **hoje**.
 */
export function Calendar({
  initial,
  selected = null,
  range,
  today = null,
  min = null,
  max = null,
  onSelect,
  onClose,
  labelledBy,
}: CalendarProps) {
  const format = useFormat();
  const headingId = useId();
  const [focused, setFocused] = useState(initial);
  const grid = useRef<HTMLTableElement>(null);
  const moved = useRef(false);
  const { year, month } = partsOf(focused);

  useEffect(() => {
    // Focus follows the roving day; the first focus lands when the calendar opens.
    grid.current?.querySelector<HTMLButtonElement>(`[data-date="${focused}"]`)?.focus();
    moved.current = true;
  }, [focused]);

  const onKeyDown = (event: KeyboardEvent<HTMLTableElement>) => {
    const step: Record<string, () => CalendarDate> = {
      ArrowLeft: () => addDays(focused, -1),
      ArrowRight: () => addDays(focused, 1),
      ArrowUp: () => addDays(focused, -7),
      ArrowDown: () => addDays(focused, 7),
      PageUp: () => addMonths(focused, event.shiftKey ? -12 : -1),
      PageDown: () => addMonths(focused, event.shiftKey ? 12 : 1),
      Home: () => addDays(focused, -weekdayOf(focused)),
      End: () => addDays(focused, 6 - weekdayOf(focused)),
    };
    const next = step[event.key];
    if (next !== undefined) {
      event.preventDefault();
      setFocused(next());
    } else if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      onClose();
    }
  };

  const inRange = (date: CalendarDate) =>
    range?.from != null &&
    range.to != null &&
    compareDates(date, range.from) >= 0 &&
    compareDates(date, range.to) <= 0;

  return (
    <div className="flex flex-col gap-stack-sm p-inset-panel" aria-labelledby={labelledBy}>
      <div className="flex items-center justify-between gap-inline-sm">
        <Button
          variant="text"
          iconOnly
          aria-label="Mês anterior"
          icon={<ChevronLeftIcon />}
          onClick={() => setFocused(addMonths(focused, -1))}
        />
        <h3 id={headingId} aria-live="polite" className="type-label text-text-primary">
          {format.monthYear(year, month)}
        </h3>
        <Button
          variant="text"
          iconOnly
          aria-label="Próximo mês"
          icon={<ChevronRightIcon />}
          onClick={() => setFocused(addMonths(focused, 1))}
        />
      </div>
      <table
        ref={grid}
        role="grid"
        aria-labelledby={headingId}
        onKeyDown={onKeyDown}
        className="border-separate border-spacing-0"
      >
        <thead>
          <tr>
            {[0, 1, 2, 3, 4, 5, 6].map((weekday) => (
              <th key={weekday} scope="col" className="pb-1 type-helper text-text-muted">
                {format.weekdayShort(weekday)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {monthWeeks(year, month).map((week, row) => (
            <tr key={row}>
              {week.map((date, column) => {
                if (date === null) return <td key={column} role="gridcell" />;
                const isSelected = date === selected || date === range?.from || date === range?.to;
                const isToday = date === today;
                const blocked = outside(date, min, max);
                return (
                  <td key={column} role="gridcell" aria-selected={isSelected}>
                    <button
                      type="button"
                      data-date={date}
                      tabIndex={date === focused ? 0 : -1}
                      aria-label={`${format.dateLong(date)}${isToday ? ', hoje' : ''}`}
                      aria-current={isToday ? 'date' : undefined}
                      aria-disabled={blocked || undefined}
                      onClick={() => {
                        if (!blocked) onSelect(date);
                      }}
                      className={cn(
                        'flex size-size-touch-target-min items-center justify-center rounded-control type-table motion-control',
                        isSelected
                          ? 'bg-action-primary-bg text-action-primary-fg'
                          : inRange(date)
                            ? 'bg-surface-selected text-text-primary'
                            : 'text-text-primary hover:bg-surface-hover',
                        isToday &&
                          !isSelected &&
                          'font-semibold underline decoration-2 underline-offset-4',
                        blocked && 'cursor-not-allowed text-text-disabled hover:bg-transparent',
                      )}
                    >
                      {partsOf(date).day}
                    </button>
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** The calendar's surface: anchored popover on wider layouts, bottom sheet on compact ones. */
function CalendarSurface({
  open,
  anchor,
  title,
  onClose,
  children,
}: {
  readonly open: boolean;
  readonly anchor: React.RefObject<HTMLElement | null>;
  readonly title: string;
  readonly onClose: () => void;
  readonly children: ReactNode;
}) {
  // Compact layouts use a bottom sheet — except inside a dialog, where one modal layer is the
  // limit and the calendar opens in the popover layer above it.
  const sheet = useCompactOutsideDialog();
  if (sheet) {
    return (
      <Dialog open={open} onDismiss={onClose} title={title} variant="sheet" initialFocus="title">
        {children}
      </Dialog>
    );
  }
  return (
    <Popover open={open} anchor={anchor} onDismiss={onClose} role="dialog" aria-label={title}>
      {children}
    </Popover>
  );
}

export interface DateFieldProps extends Omit<
  ComponentPropsWithoutRef<'input'>,
  'value' | 'defaultValue' | 'onChange' | 'min' | 'max'
> {
  readonly value: CalendarDate | null;
  readonly onValueChange: (value: CalendarDate | null) => void;
  /** The typed text is not a date: the screen shows `invalidDateMessage` in the field. */
  readonly onInvalidChange?: (invalid: boolean) => void;
  readonly today?: CalendarDate | null;
  readonly min?: CalendarDate | null;
  readonly max?: CalendarDate | null;
}

/**
 * A `dd/mm/aaaa` text field with a calendar button. Partial input is kept as typed and never
 * corrected into another date; it is read when the field loses focus.
 */
export function DateField({
  value,
  onValueChange,
  onInvalidChange,
  today = null,
  min = null,
  max = null,
  className,
  ...props
}: DateFieldProps) {
  const format = useFormat();
  const formatHintId = useId();
  const control = useControlProps({
    ...props,
    'aria-describedby': [props['aria-describedby'], formatHintId].filter(Boolean).join(' '),
  });
  const [text, setText] = useState(value === null ? '' : format.date(value));
  const [open, setOpen] = useState(false);
  const anchor = useRef<HTMLDivElement>(null);
  const button = useRef<HTMLButtonElement>(null);
  // A value set from outside (calendar, stepper, reset) replaces the text.
  const [shown, setShown] = useState(value);
  if (shown !== value) {
    setShown(value);
    setText(value === null ? '' : format.date(value));
  }
  // The calendar opens on the value's month, else the branch's today: until one is known (the
  // branch clock still loading) the calendar is not available — typing still is.
  const anchorDate = value ?? today ?? max ?? min;
  // Disabled (its own prop or its FormField) covers the whole composite: the text field and the
  // calendar alike. Without an anchor date the calendar waits but stays reachable (its reason).
  const disabled = control.disabled === true;
  const calendarReady = anchorDate !== null && !disabled;

  const commit = () => {
    if (disabled) return;
    const typed = text.trim();
    if (typed.length === 0) {
      onInvalidChange?.(false);
      if (value !== null) onValueChange(null);
      return;
    }
    const parsed = format.dateFromInput(typed);
    onInvalidChange?.(parsed === null);
    if (parsed !== null && parsed !== value) onValueChange(parsed);
  };

  const close = () => {
    setOpen(false);
    button.current?.focus();
  };

  return (
    <div ref={anchor} className={cn('relative flex min-w-0 items-center', className)}>
      <input
        {...control}
        type="text"
        inputMode="numeric"
        autoComplete="off"
        placeholder="dd/mm/aaaa"
        value={text}
        onChange={(event) => setText(event.target.value)}
        onBlur={commit}
        className={cn(controlChrome, 'pe-size-touch-target-min tabular-nums')}
      />
      <span id={formatHintId} className="sr-only">
        Formato dd/mm/aaaa
      </span>
      <span className="absolute end-0 flex">
        <button
          ref={button}
          type="button"
          aria-label="Abrir calendário"
          aria-haspopup="dialog"
          aria-expanded={open}
          disabled={disabled || undefined}
          aria-disabled={(!disabled && anchorDate === null) || undefined}
          title={!disabled && anchorDate === null ? 'Carregando…' : undefined}
          onClick={() => {
            if (calendarReady) setOpen((current) => !current);
          }}
          className="flex size-size-touch-target-min items-center justify-center rounded-control text-action-ghost-fg hover:bg-action-ghost-bg-hover disabled:cursor-not-allowed disabled:text-action-disabled-fg disabled:hover:bg-transparent"
        >
          <CalendarIcon />
        </button>
      </span>
      <CalendarSurface
        open={open && calendarReady}
        anchor={anchor}
        title="Calendário"
        onClose={close}
      >
        <Calendar
          initial={anchorDate ?? today ?? ('' as CalendarDate)}
          selected={value}
          today={today}
          min={min}
          max={max}
          onSelect={(date) => {
            if (!calendarReady) return;
            onInvalidChange?.(false);
            onValueChange(date);
            close();
          }}
          onClose={close}
        />
      </CalendarSurface>
    </div>
  );
}

/**
 * A `DateFrom`/`DateTo` pair: two fields (**De**, **Até**) and one shared calendar where the first
 * choice sets **De** and the second **Até**. Window limits are the server's (verbatim 400).
 */
export function DateRangeField({
  from,
  to,
  onRangeChange,
  today = null,
  max = null,
  legend,
  disabled = false,
}: {
  readonly from: CalendarDate | null;
  readonly to: CalendarDate | null;
  readonly onRangeChange: (range: { from: CalendarDate | null; to: CalendarDate | null }) => void;
  readonly today?: CalendarDate | null;
  readonly max?: CalendarDate | null;
  readonly legend: ReactNode;
  /** The whole range is unavailable: both fields and the shared calendar. */
  readonly disabled?: boolean;
}) {
  const fromId = useId();
  const toId = useId();
  const [invalid, setInvalid] = useState({ from: false, to: false });
  const [open, setOpen] = useState(false);
  const [picking, setPicking] = useState<'from' | 'to'>('from');
  const anchor = useRef<HTMLDivElement>(null);
  const button = useRef<HTMLButtonElement>(null);
  const close = () => {
    setOpen(false);
    button.current?.focus();
  };
  return (
    <fieldset className="flex min-w-0 flex-col gap-stack-xs">
      <legend className="type-label text-text-primary">{legend}</legend>
      <div ref={anchor} className="flex flex-wrap items-end gap-inline-md">
        {(['from', 'to'] as const).map((edge) => (
          <div key={edge} className="flex min-w-[min(100%,11em)] flex-1 flex-col gap-stack-xs">
            <label
              htmlFor={edge === 'from' ? fromId : toId}
              className="type-helper text-text-secondary"
            >
              {edge === 'from' ? 'De' : 'Até'}
            </label>
            <DateField
              id={edge === 'from' ? fromId : toId}
              value={edge === 'from' ? from : to}
              onValueChange={(date) =>
                onRangeChange(edge === 'from' ? { from: date, to } : { from, to: date })
              }
              onInvalidChange={(bad) => setInvalid((current) => ({ ...current, [edge]: bad }))}
              aria-invalid={invalid[edge] || undefined}
              disabled={disabled || undefined}
              today={today}
              max={max}
            />
          </div>
        ))}
        <Button
          ref={button}
          variant="secondary"
          icon={<CalendarIcon />}
          aria-haspopup="dialog"
          aria-expanded={open}
          disabled={disabled || (from ?? today ?? max) === null}
          onClick={() => {
            if (disabled) return;
            setPicking('from');
            setOpen((current) => !current);
          }}
        >
          Escolher período
        </Button>
      </div>
      {invalid.from || invalid.to ? (
        <p className="type-helper text-feedback-danger-fg">{invalidDateMessage}</p>
      ) : null}
      <CalendarSurface
        open={open && !disabled}
        anchor={anchor}
        title="Escolher período"
        onClose={close}
      >
        <p className="px-inset-panel pt-inset-panel type-helper text-text-secondary">
          {picking === 'from' ? 'Escolha a data inicial (De).' : 'Escolha a data final (Até).'}
        </p>
        <Calendar
          initial={(picking === 'to' ? (to ?? from) : from) ?? today ?? max ?? ('' as CalendarDate)}
          range={{ from, to }}
          today={today}
          max={max}
          onSelect={(date) => {
            if (disabled) return;
            if (picking === 'from' || (from !== null && compareDates(date, from) < 0)) {
              onRangeChange({ from: date, to: null });
              setPicking('to');
            } else {
              onRangeChange({ from, to: date });
              close();
            }
          }}
          onClose={close}
        />
      </CalendarSurface>
    </fieldset>
  );
}

/**
 * The Work Queue day control: **Dia anterior** · date field · **Próximo dia**. The step is calendar
 * arithmetic only; whether a day is allowed is the server's.
 */
export function DayStepper({
  value,
  onValueChange,
  today = null,
  max = null,
  nextUnavailableReason,
  label,
  disabled = false,
}: {
  readonly value: CalendarDate;
  readonly onValueChange: (value: CalendarDate) => void;
  readonly today?: CalendarDate | null;
  readonly max?: CalendarDate | null;
  /** Shown when the next day is past `max` (the reason, as adjacent text). */
  readonly nextUnavailableReason?: ReactNode;
  readonly label: string;
  /** The whole control is unavailable: steps, field and calendar. */
  readonly disabled?: boolean;
}) {
  const id = useId();
  const [invalid, setInvalid] = useState(false);
  const atMax = max !== null && compareDates(value, max) >= 0;
  return (
    <div className="flex flex-wrap items-start gap-inline-sm">
      <Button
        variant="secondary"
        disabled={disabled}
        onClick={() => onValueChange(addDays(value, -1))}
      >
        Dia anterior
      </Button>
      <div className="flex min-w-[min(100%,11em)] flex-col gap-stack-xs">
        <label htmlFor={id} className="sr-only">
          {label}
        </label>
        <DateField
          id={id}
          value={value}
          onValueChange={(date) => {
            if (date !== null) onValueChange(date);
          }}
          onInvalidChange={setInvalid}
          aria-invalid={invalid || undefined}
          disabled={disabled || undefined}
          today={today}
          max={max}
        />
        {invalid ? (
          <span className="type-helper text-feedback-danger-fg">{invalidDateMessage}</span>
        ) : null}
      </div>
      <Button
        variant="secondary"
        disabled={disabled || atMax}
        disabledReason={!disabled && atMax ? nextUnavailableReason : undefined}
        onClick={() => onValueChange(addDays(value, 1))}
      >
        Próximo dia
      </Button>
    </div>
  );
}

export interface CalendarMonth {
  readonly year: number;
  readonly month: number;
}

/** Month and year (`MM/yyyy`) for Monthly reconciliation and month filters. */
export function MonthField({
  value,
  onValueChange,
  max = null,
  disabled: ownDisabled = false,
}: {
  readonly value: CalendarMonth | null;
  readonly onValueChange: (value: CalendarMonth) => void;
  /** The latest month offered (the branch's current month); also where the picker opens. */
  readonly max?: CalendarMonth | null;
  readonly disabled?: boolean;
}) {
  const format = useFormat();
  const field = useField();
  const disabled = ownDisabled || field?.disabled === true;
  const [open, setOpen] = useState(false);
  // Opens on the value's year, else the branch's: never a year guessed from a device clock.
  const [chosenYear, setYear] = useState<number | null>(null);
  const year = chosenYear ?? value?.year ?? max?.year ?? null;
  const ready = year !== null && !disabled;
  const anchor = useRef<HTMLButtonElement>(null);
  const after = (candidate: CalendarMonth) =>
    max !== null &&
    (candidate.year > max.year || (candidate.year === max.year && candidate.month > max.month));
  const close = () => {
    setOpen(false);
    anchor.current?.focus();
  };
  return (
    <div className="flex min-w-0 flex-col">
      <button
        ref={anchor}
        id={field?.id}
        type="button"
        aria-haspopup="dialog"
        aria-expanded={open && ready}
        aria-describedby={field?.describedBy}
        disabled={disabled || undefined}
        aria-disabled={(!disabled && year === null) || undefined}
        title={!disabled && year === null ? 'Carregando…' : undefined}
        onClick={() => {
          if (ready) setOpen((current) => !current);
        }}
        className={cn(
          controlChrome,
          'flex items-center justify-between text-start tabular-nums',
          value === null && 'text-text-placeholder',
        )}
      >
        <span>{value === null ? 'mm/aaaa' : format.month(value.year, value.month)}</span>
        <CalendarIcon className="text-text-muted" />
      </button>
      <CalendarSurface open={open && ready} anchor={anchor} title="Escolher mês" onClose={close}>
        <div
          className="flex flex-col gap-stack-sm p-inset-panel"
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              event.preventDefault();
              close();
            }
          }}
        >
          <div className="flex items-center justify-between">
            <Button
              variant="text"
              iconOnly
              aria-label="Ano anterior"
              icon={<ChevronLeftIcon />}
              onClick={() => setYear((year ?? 0) - 1)}
            />
            <span aria-live="polite" className="type-label tabular-nums">
              {year}
            </span>
            <Button
              variant="text"
              iconOnly
              aria-label="Próximo ano"
              icon={<ChevronRightIcon />}
              onClick={() => setYear((year ?? 0) + 1)}
            />
          </div>
          <div className="grid grid-cols-3 gap-inline-sm">
            {Array.from({ length: 12 }, (_, index) => index + 1).map((month) => {
              const candidate = { year: year ?? 0, month };
              const current = value?.year === year && value.month === month;
              return (
                <Button
                  key={month}
                  variant={current ? 'primary' : 'text'}
                  size="small"
                  aria-pressed={current}
                  disabled={after(candidate)}
                  onClick={() => {
                    if (!ready) return;
                    onValueChange(candidate);
                    close();
                  }}
                >
                  {format.monthName(month)}
                </Button>
              );
            })}
          </div>
        </div>
      </CalendarSurface>
    </div>
  );
}

/**
 * A wall-clock time (`TransactionTime`, clock in/out): `HH:mm`, 24-hour, exactly as typed and sent
 * — no time zone conversion. `isWallClockTime` tells the screen when the text is complete.
 */
export function TimeField({
  value,
  onValueChange,
  className,
  ...props
}: Omit<ComponentPropsWithoutRef<'input'>, 'value' | 'onChange' | 'type'> & {
  readonly value: string;
  readonly onValueChange: (value: string) => void;
}) {
  const hintId = useId();
  const control = useControlProps({
    ...props,
    'aria-describedby': [props['aria-describedby'], hintId].filter(Boolean).join(' '),
  });
  return (
    <div className={cn('relative flex min-w-[min(100%,8em)] items-center', className)}>
      <input
        {...control}
        type="text"
        inputMode="numeric"
        autoComplete="off"
        placeholder="hh:mm"
        maxLength={5}
        value={value}
        aria-invalid={
          control['aria-invalid'] ??
          (value.length === 5 && !isWallClockTime(value) ? true : undefined)
        }
        onChange={(event) => onValueChange(event.target.value)}
        className={cn(controlChrome, 'pe-10 tabular-nums')}
      />
      <span id={hintId} className="sr-only">
        Formato hh:mm, 24 horas
      </span>
      <ClockIcon className="pointer-events-none absolute end-inset-control-x text-text-muted" />
    </div>
  );
}
