import {
  addDays,
  addMonths,
  calendarDateFromParts,
  calendarDateParts,
  compareCalendarDates,
  daysInMonth,
  formatDate,
  formatDateWithWeekday,
  formatMonthField,
  formatMonthYear,
  MAX_CALENDAR_YEAR,
  MIN_CALENDAR_YEAR,
  parseDateInput,
  parseWallTime,
  weekdayOf,
  weekdayShortName,
  type CalendarDate,
} from '@loto/core';
import { useEffect, useRef, useState } from 'react';
import { Pressable, ScrollView, TextInput, View } from 'react-native';
import { useTheme } from '@/theme/provider';
import { isCompact, radius, spacing, textStyle } from '@/theme/style';
import { Button } from './button';
import { FocusRing } from './focus';
import { useFieldControl } from './form-field';
import { ReadOnlyValue, useControlChrome, type FieldControlProps } from './input';
import { Overlay } from './overlay';
import { AppText, Glyph } from './text';

export const INVALID_DATE = 'Data inválida. Use dd/mm/aaaa.';
export const INVALID_TIME = 'Hora inválida. Use hh:mm.';
const FIRST_DAY = calendarDateFromParts(MIN_CALENDAR_YEAR, 1, 1);
const LAST_DAY = calendarDateFromParts(MAX_CALENDAR_YEAR, 12, 31);

export interface DateBounds {
  /** Branch-local today from `GET /branch/current` — never the device clock. */
  readonly today?: CalendarDate;
  /** Hints from known rules; the server stays authoritative and its message renders verbatim. */
  readonly min?: CalendarDate;
  readonly max?: CalendarDate;
}

function withinBounds(date: CalendarDate, { min, max }: DateBounds) {
  return (
    (min === undefined || compareCalendarDates(date, min) >= 0) &&
    (max === undefined || compareCalendarDates(date, max) <= 0)
  );
}

/** The month grid (Sunday first) of `year/month`, null cells padding the first week. */
export function monthGrid(year: number, month: number): (CalendarDate | null)[] {
  const first = calendarDateFromParts(year, month, 1);
  const cells: (CalendarDate | null)[] = Array.from({ length: weekdayOf(first) }, () => null);
  for (let day = 1; day <= daysInMonth(year, month); day += 1)
    cells.push(calendarDateFromParts(year, month, day));
  while (cells.length % 7 !== 0) cells.push(null);
  return cells;
}

function CalendarSheet({
  visible,
  onDismiss,
  selected,
  onSelect,
  bounds,
  title,
  returnFocus,
}: {
  readonly visible: boolean;
  readonly onDismiss: () => void;
  readonly selected: CalendarDate | null;
  readonly onSelect: (date: CalendarDate) => void;
  readonly bounds: DateBounds;
  readonly title: string;
  readonly returnFocus: React.RefObject<View | null>;
}) {
  const { colors, width, fontScale } = useTheme();
  // Enlarged text cannot fit both buttons beside the month title: the title gets its own line and
  // the buttons wrap below it, keeping the font at the chosen size.
  const stacked = isCompact(width, fontScale);
  // The grid keeps seven columns. When enlarged text no longer fits two digits in a seventh of the
  // screen, the cells take the width the text needs and the grid scrolls sideways; weekday headers
  // shorten to their initial (each day still speaks its full date).
  const digitsWidth = Math.ceil(
    2 * 0.7 * (textStyle('table').fontSize as number) * fontScale + 2 * spacing.inline.xs,
  );
  const cell = stacked ? Math.max(spacing.size['touch-target-min'], digitsWidth) : null;
  const cellWidth = cell ?? (`${100 / 7}%` as const);
  const start = selected ?? bounds.today ?? bounds.max ?? bounds.min ?? null;
  // Remounted on every opening (`key`), so it starts at the selected (or branch) month.
  const [shown, setShown] = useState(() => (start ? calendarDateParts(start) : null));
  const [typedYear, setTypedYear] = useState('');
  if (shown === null)
    return (
      <Overlay
        visible={visible}
        onDismiss={onDismiss}
        title={title}
        variant="bottom-sheet"
        returnFocus={returnFocus}
      >
        <AppText variant="body" tone="secondary">
          Digite a data no campo (dd/mm/aaaa).
        </AppText>
      </Overlay>
    );
  const month = calendarDateFromParts(shown.year, shown.month, 1);
  const atStart = shown.year === MIN_CALENDAR_YEAR && shown.month === 1;
  const atEnd = shown.year === MAX_CALENDAR_YEAR && shown.month === 12;
  const move = (months: number) => {
    // Bounds-safe even if invoked at an end of the calendar: it stays put instead of throwing.
    const target = shown.year * 12 + (shown.month - 1) + months;
    if (target < MIN_CALENDAR_YEAR * 12 || target > MAX_CALENDAR_YEAR * 12 + 11) return;
    setShown(calendarDateParts(addMonths(month, months)));
  };
  return (
    <Overlay
      visible={visible}
      onDismiss={onDismiss}
      title={title}
      variant="bottom-sheet"
      returnFocus={returnFocus}
    >
      {stacked ? (
        <AppText variant="body-strong" accessibilityRole="header" testID="calendar-month">
          {formatMonthYear(shown.year, shown.month)}
        </AppText>
      ) : null}
      <View
        style={{
          flexDirection: 'row',
          flexWrap: 'wrap',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: spacing.inline.sm,
        }}
      >
        <Button
          label="Mês anterior"
          variant="secondary"
          disabled={atStart}
          onPress={() => move(-1)}
        />
        {stacked ? null : (
          <AppText
            variant="body-strong"
            accessibilityRole="header"
            align="center"
            testID="calendar-month"
            style={{ flex: 1 }}
          >
            {formatMonthYear(shown.year, shown.month)}
          </AppText>
        )}
        <Button label="Próximo mês" variant="secondary" disabled={atEnd} onPress={() => move(1)} />
      </View>
      <View
        style={{
          flexDirection: 'row',
          flexWrap: 'wrap',
          gap: spacing.inline.sm,
          alignItems: 'center',
        }}
      >
        <AppText variant="helper" tone="secondary" style={{ flexShrink: 1 }}>
          Ir para o ano
        </AppText>
        <TextInput
          accessibilityLabel="Ano"
          keyboardType="number-pad"
          maxLength={4}
          value={typedYear}
          onChangeText={setTypedYear}
          onSubmitEditing={() => {
            const year = Number(typedYear);
            if (/^\d{4}$/.test(typedYear) && year >= MIN_CALENDAR_YEAR && year <= MAX_CALENDAR_YEAR)
              setShown({ ...shown, year });
          }}
          placeholder="aaaa"
          placeholderTextColor={colors.sem.text.placeholder}
          allowFontScaling
          style={{
            // Four digits at the chosen text size, plus the control padding.
            minWidth: 72 * Math.max(1, fontScale),
            minHeight: spacing.size['touch-target-min'],
            borderWidth: spacing.size['border-width'],
            borderColor: colors.sem.border.control,
            borderRadius: radius.control,
            paddingHorizontal: spacing.inline.md,
            color: colors.sem.text.primary,
          }}
        />
      </View>
      <ScrollView horizontal scrollEnabled={cell !== null} contentContainerStyle={{ flexGrow: 1 }}>
        <View
          role="grid"
          testID="calendar-grid"
          style={{
            flexDirection: 'row',
            flexWrap: 'wrap',
            width: cell === null ? '100%' : cell * 7,
          }}
        >
          {[0, 1, 2, 3, 4, 5, 6].map((weekday) => (
            <View
              key={`h${weekday}`}
              style={{
                width: cellWidth,
                alignItems: 'center',
                paddingVertical: spacing.stack.xs,
              }}
            >
              <AppText
                variant="table-header"
                tone="muted"
                numberOfLines={1}
                accessibilityLabel={weekdayShortName(weekday)}
              >
                {cell === null ? weekdayShortName(weekday) : weekdayShortName(weekday).charAt(0)}
              </AppText>
            </View>
          ))}
          {monthGrid(shown.year, shown.month).map((date, index) => {
            if (date === null) return <View key={`e${index}`} style={{ width: cellWidth }} />;
            const isSelected = selected !== null && compareCalendarDates(date, selected) === 0;
            const isToday =
              bounds.today !== undefined && compareCalendarDates(date, bounds.today) === 0;
            const enabled = withinBounds(date, bounds);
            const { day } = calendarDateParts(date);
            return (
              <Pressable
                key={date}
                testID={`day-${date}`}
                accessibilityRole="button"
                accessibilityLabel={`${formatDateWithWeekday(date)}${isToday ? ', hoje' : ''}`}
                accessibilityState={{ selected: isSelected, disabled: !enabled }}
                onPress={() => {
                  if (enabled) onSelect(date);
                }}
                style={{
                  width: cellWidth,
                  minHeight: spacing.size['touch-target-min'],
                  alignItems: 'center',
                  justifyContent: 'center',
                  borderRadius: radius.control,
                  backgroundColor: isSelected ? colors.sem.action.primary.bg : undefined,
                  borderWidth: isToday && !isSelected ? spacing.size['border-width'] : 0,
                  borderColor: colors.sem.action.primary.bg,
                }}
              >
                <AppText
                  variant="table"
                  numberOfLines={1}
                  color={
                    isSelected
                      ? colors.sem.action.primary.fg
                      : enabled
                        ? colors.sem.text.primary
                        : colors.sem.text.disabled
                  }
                >
                  {String(day)}
                </AppText>
              </Pressable>
            );
          })}
        </View>
      </ScrollView>
      {bounds.today ? (
        <Button
          label="Hoje"
          variant="link"
          disabled={!withinBounds(bounds.today, bounds)}
          onPress={() => onSelect(bounds.today as CalendarDate)}
        />
      ) : null}
    </Overlay>
  );
}

export interface DateFieldProps extends FieldControlProps, DateBounds {
  readonly value: CalendarDate | null;
  readonly onValueChange: (value: CalendarDate | null) => void;
}

/**
 * Business date (date-picker.md): `dd/MM/yyyy` typing is always possible and partial text is kept,
 * never auto-corrected; the calendar sheet is a convenience over core's calendar-only arithmetic
 * (years 0001–9999, no `Date` instant, no OS picker clipping). Disabled or read-only never opens
 * the calendar and never emits a date.
 */
export function DateField({
  value,
  onValueChange,
  today,
  min,
  max,
  testID,
  ...own
}: DateFieldProps) {
  const control = useFieldControl(own);
  const { colors } = useTheme();
  const [text, setText] = useState(value ? formatDate(value) : '');
  const [invalid, setInvalid] = useState(false);
  const [open, setOpen] = useState(false);
  const [openings, setOpenings] = useState(0);
  const [focused, setFocused] = useState(false);
  const input = useRef<TextInput>(null);
  const button = useRef<View>(null);
  const chrome = useControlChrome({
    focused,
    invalid: control.invalid || invalid,
    disabled: control.disabled,
  });
  const { registerFocus } = control;
  useEffect(() => {
    registerFocus(() => input.current?.focus());
    return () => registerFocus(null);
  }, [registerFocus]);
  // An external value change (a stepper, a reset) rewrites the text.
  const lastValue = useRef(value);
  useEffect(() => {
    if (lastValue.current === value) return;
    lastValue.current = value;
    setText(value ? formatDate(value) : '');
    setInvalid(false);
  }, [value]);
  if (control.readOnly)
    return (
      <ReadOnlyValue
        value={value ? formatDate(value) : '—'}
        label={control.accessibilityLabel}
        testID={testID}
      />
    );
  const commit = (raw: string) => {
    if (control.disabled) return;
    if (raw.trim() === '') {
      setInvalid(false);
      lastValue.current = null;
      onValueChange(null);
      return;
    }
    const parsed = parseDateInput(raw);
    setInvalid(parsed === null);
    if (parsed !== null) {
      lastValue.current = parsed;
      onValueChange(parsed);
    }
  };
  return (
    <View style={{ gap: spacing.stack.xs }}>
      <View style={chrome.container}>
        <TextInput
          ref={input}
          testID={testID}
          value={text}
          editable={!control.disabled}
          onChangeText={setText}
          onBlur={() => {
            setFocused(false);
            commit(text);
          }}
          onFocus={() => setFocused(true)}
          onSubmitEditing={() => commit(text)}
          keyboardType="numbers-and-punctuation"
          placeholder="dd/mm/aaaa"
          placeholderTextColor={chrome.placeholder}
          selectionColor={chrome.selection}
          accessibilityLabel={control.accessibilityLabel}
          accessibilityHint={[
            control.accessibilityHint,
            'Formato dd/mm/aaaa',
            invalid ? `Erro: ${INVALID_DATE}` : undefined,
          ]
            .filter(Boolean)
            .join('. ')}
          accessibilityState={{ disabled: control.disabled }}
          aria-required={control.required}
          allowFontScaling
          style={chrome.text}
        />
        <Pressable
          ref={button}
          testID={testID ? `${testID}-calendar` : undefined}
          accessibilityRole="button"
          accessibilityLabel={`Abrir calendário${control.accessibilityLabel ? ` de ${control.accessibilityLabel}` : ''}`}
          accessibilityState={{ disabled: control.disabled, expanded: open }}
          onPress={() => {
            if (control.disabled) return;
            setOpenings((count) => count + 1);
            setOpen(true);
          }}
          style={{
            minWidth: spacing.size['touch-target-comfortable'],
            minHeight: spacing.size['touch-target-comfortable'],
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Glyph
            name="calendar"
            color={control.disabled ? colors.sem.action.disabled.fg : colors.sem.action.ghost.fg}
            size={18}
          />
        </Pressable>
        <FocusRing visible={focused} />
      </View>
      {invalid ? (
        <AppText variant="helper" color={colors.sem.feedback.danger.fg}>
          {INVALID_DATE}
        </AppText>
      ) : null}
      <CalendarSheet
        key={openings}
        visible={open && !control.disabled}
        onDismiss={() => setOpen(false)}
        selected={value}
        bounds={{ today, min, max }}
        title={control.accessibilityLabel ?? 'Calendário'}
        returnFocus={button}
        onSelect={(date) => {
          setOpen(false);
          if (control.disabled || control.readOnly) return;
          // Every choice rewrites the text, including reselecting the current date after an edit.
          setText(formatDate(date));
          setInvalid(false);
          lastValue.current = date;
          onValueChange(date);
        }}
      />
    </View>
  );
}

/** `DateFrom` / `DateTo` pair: **De** / **Até**, side by side when wide, stacked when compact. */
export function DateRangeField({
  from,
  to,
  onChange,
  disabled,
  readOnly,
  today,
  testID,
}: {
  readonly from: CalendarDate | null;
  readonly to: CalendarDate | null;
  readonly onChange: (range: { from: CalendarDate | null; to: CalendarDate | null }) => void;
  readonly disabled?: boolean;
  readonly readOnly?: boolean;
  readonly today?: CalendarDate;
  readonly testID?: string;
}) {
  const { width, fontScale } = useTheme();
  const stacked = isCompact(width, fontScale);
  return (
    <View
      testID={testID}
      style={{ flexDirection: stacked ? 'column' : 'row', gap: spacing.form['column-gap'] }}
    >
      <View style={{ flex: stacked ? undefined : 1, gap: spacing.stack.xs }}>
        <AppText variant="label">De</AppText>
        <DateField
          value={from}
          today={today}
          max={to ?? undefined}
          accessibilityLabel="De"
          disabled={disabled}
          readOnly={readOnly}
          onValueChange={(value) => onChange({ from: value, to })}
          testID={testID ? `${testID}-from` : undefined}
        />
      </View>
      <View style={{ flex: stacked ? undefined : 1, gap: spacing.stack.xs }}>
        <AppText variant="label">Até</AppText>
        <DateField
          value={to}
          today={today}
          min={from ?? undefined}
          accessibilityLabel="Até"
          disabled={disabled}
          readOnly={readOnly}
          onValueChange={(value) => onChange({ from, to: value })}
          testID={testID ? `${testID}-to` : undefined}
        />
      </View>
    </View>
  );
}

/**
 * Work Queue day: **Dia anterior** · date · **Próximo dia**, bounded at 0001-01-01 / 9999-12-31 (or
 * `max`). Disabled or read-only — its own or the enclosing FormField's — never emits a day.
 */
export function DayStepper({
  value,
  onValueChange,
  max,
  disabled: ownDisabled,
  readOnly: ownReadOnly,
  testID,
}: {
  readonly value: CalendarDate;
  readonly onValueChange: (value: CalendarDate) => void;
  readonly max?: CalendarDate;
  readonly disabled?: boolean;
  readonly readOnly?: boolean;
  readonly testID?: string;
}) {
  const control = useFieldControl({ disabled: ownDisabled, readOnly: ownReadOnly });
  if (control.readOnly)
    return (
      <ReadOnlyValue value={formatDate(value)} label={control.accessibilityLabel} testID={testID} />
    );
  const { disabled } = control;
  const atStart = compareCalendarDates(value, FIRST_DAY) <= 0;
  const atEnd = compareCalendarDates(value, max ?? LAST_DAY) >= 0;
  return (
    <View
      testID={testID}
      style={{
        flexDirection: 'row',
        flexWrap: 'wrap',
        alignItems: 'center',
        gap: spacing.size['touch-target-gap'],
      }}
    >
      <Button
        label="Dia anterior"
        variant="secondary"
        disabled={disabled || atStart}
        onPress={() => {
          if (!disabled && !atStart) onValueChange(addDays(value, -1));
        }}
      />
      <AppText variant="body-strong" accessibilityLabel={formatDateWithWeekday(value)}>
        {formatDate(value)}
      </AppText>
      <Button
        label="Próximo dia"
        variant="secondary"
        disabled={disabled || atEnd}
        onPress={() => {
          if (!disabled && !atEnd) onValueChange(addDays(value, 1));
        }}
      />
    </View>
  );
}

/**
 * Month and year (`MM/yyyy`). Without a value or a known reference month it waits rather than
 * guessing a year; year/month moves stop at 0001 and 9999. Disabled or read-only — its own or the
 * enclosing FormField's — never emits a month.
 */
export function MonthField({
  value,
  onValueChange,
  reference,
  disabled: ownDisabled,
  readOnly: ownReadOnly,
  testID,
}: {
  readonly value: { readonly year: number; readonly month: number } | null;
  readonly onValueChange: (value: { year: number; month: number }) => void;
  /** The branch's current month (from the branch clock) used when no value is chosen yet. */
  readonly reference?: { readonly year: number; readonly month: number };
  readonly disabled?: boolean;
  readonly readOnly?: boolean;
  readonly testID?: string;
}) {
  const control = useFieldControl({ disabled: ownDisabled, readOnly: ownReadOnly });
  const shown = value ?? reference ?? null;
  if (control.readOnly)
    return (
      <ReadOnlyValue
        value={value ? formatMonthField(value.year, value.month) : '—'}
        label={control.accessibilityLabel}
        testID={testID}
      />
    );
  const disabled = control.disabled || shown === null;
  const move = (months: number) => {
    if (shown === null || control.disabled || control.readOnly) return;
    const target = shown.year * 12 + (shown.month - 1) + months;
    if (target < MIN_CALENDAR_YEAR * 12 || target > MAX_CALENDAR_YEAR * 12 + 11) return;
    const next = calendarDateParts(
      addMonths(calendarDateFromParts(shown.year, shown.month, 1), months),
    );
    onValueChange({ year: next.year, month: next.month });
  };
  const at = (year: number, month: number) =>
    shown !== null && shown.year === year && shown.month === month;
  return (
    <View testID={testID} style={{ gap: spacing.stack.sm }}>
      <AppText
        variant="body-strong"
        accessibilityLabel={
          shown ? formatMonthYear(shown.year, shown.month) : 'Mês de referência indisponível'
        }
      >
        {shown ? formatMonthField(shown.year, shown.month) : 'Mês de referência indisponível'}
      </AppText>
      <View
        style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.size['touch-target-gap'] }}
      >
        <Button
          label="Ano anterior"
          variant="secondary"
          disabled={disabled || (shown?.year ?? 0) <= MIN_CALENDAR_YEAR}
          onPress={() => move(-12)}
        />
        <Button
          label="Mês anterior"
          variant="secondary"
          disabled={disabled || at(MIN_CALENDAR_YEAR, 1)}
          onPress={() => move(-1)}
        />
        <Button
          label="Próximo mês"
          variant="secondary"
          disabled={disabled || at(MAX_CALENDAR_YEAR, 12)}
          onPress={() => move(1)}
        />
        <Button
          label="Próximo ano"
          variant="secondary"
          disabled={disabled || (shown?.year ?? MAX_CALENDAR_YEAR) >= MAX_CALENDAR_YEAR}
          onPress={() => move(12)}
        />
      </View>
    </View>
  );
}

/** Wall-clock `HH:mm`, 24-hour, as returned — never converted between time zones. */
export function TimeField({
  value,
  onValueChange,
  testID,
  ...own
}: FieldControlProps & {
  readonly value: string | null;
  readonly onValueChange: (value: string | null) => void;
}) {
  const control = useFieldControl(own);
  const { colors } = useTheme();
  const [text, setText] = useState(value ?? '');
  const [invalid, setInvalid] = useState(false);
  const [focused, setFocused] = useState(false);
  const input = useRef<TextInput>(null);
  const chrome = useControlChrome({
    focused,
    invalid: control.invalid || invalid,
    disabled: control.disabled,
  });
  const { registerFocus } = control;
  useEffect(() => {
    registerFocus(() => input.current?.focus());
    return () => registerFocus(null);
  }, [registerFocus]);
  // An external value change (a reset, a reload) rewrites the text; a re-render with the same value,
  // or the value this field just emitted, keeps a partial edit as typed.
  const lastValue = useRef(value);
  useEffect(() => {
    if (lastValue.current === value) return;
    lastValue.current = value;
    setText(value ?? '');
    setInvalid(false);
  }, [value]);
  if (control.readOnly)
    return (
      <ReadOnlyValue value={value ?? '—'} label={control.accessibilityLabel} testID={testID} />
    );
  const commit = () => {
    if (control.disabled) return;
    if (text.trim() === '') {
      setInvalid(false);
      lastValue.current = null;
      onValueChange(null);
      return;
    }
    const ok = /^\d{2}:\d{2}$/.test(text.trim()) && parseWallTime(text.trim()) !== null;
    setInvalid(!ok);
    if (ok) {
      lastValue.current = text.trim();
      onValueChange(text.trim());
    }
  };
  return (
    <View style={{ gap: spacing.stack.xs }}>
      <View style={chrome.container}>
        <TextInput
          ref={input}
          testID={testID}
          value={text}
          editable={!control.disabled}
          onChangeText={setText}
          onFocus={() => setFocused(true)}
          onBlur={() => {
            setFocused(false);
            commit();
          }}
          onSubmitEditing={commit}
          keyboardType="numbers-and-punctuation"
          maxLength={5}
          placeholder="hh:mm"
          placeholderTextColor={chrome.placeholder}
          accessibilityLabel={control.accessibilityLabel}
          accessibilityHint={[
            control.accessibilityHint,
            'Formato hh:mm, 24 horas',
            invalid ? `Erro: ${INVALID_TIME}` : undefined,
          ]
            .filter(Boolean)
            .join('. ')}
          accessibilityState={{ disabled: control.disabled }}
          allowFontScaling
          style={[chrome.text, { fontVariant: ['tabular-nums'] }]}
        />
        <FocusRing visible={focused} />
      </View>
      {invalid ? (
        <AppText variant="helper" color={colors.sem.feedback.danger.fg}>
          {INVALID_TIME}
        </AppText>
      ) : null}
    </View>
  );
}
