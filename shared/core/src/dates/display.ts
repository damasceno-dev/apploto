// Calendar and wall-clock display (design/specs/formatting-ptbr.md §4). Hand-rolled pt-BR names:
// weekday and month names are lowercase; dates are `dd/MM/yyyy`, times `HH:mm` on a 24-hour clock
// without seconds. Branch wall-clock values render exactly as returned — no zone conversion.
import {
  calendarDateParts,
  MAX_CALENDAR_YEAR,
  MIN_CALENDAR_YEAR,
  parseCalendarDate,
  weekdayOf,
  type CalendarDate,
} from './calendar';

const monthNames = [
  'janeiro',
  'fevereiro',
  'março',
  'abril',
  'maio',
  'junho',
  'julho',
  'agosto',
  'setembro',
  'outubro',
  'novembro',
  'dezembro',
];
const weekdayNames = [
  'domingo',
  'segunda-feira',
  'terça-feira',
  'quarta-feira',
  'quinta-feira',
  'sexta-feira',
  'sábado',
];
const weekdayAbbreviations = ['dom.', 'seg.', 'ter.', 'qua.', 'qui.', 'sex.', 'sáb.'];

const two = (value: number) => String(value).padStart(2, '0');
const four = (value: number) => String(value).padStart(4, '0');

function checkMonth(year: number, month: number): void {
  if (
    !Number.isInteger(year) ||
    !Number.isInteger(month) ||
    year < MIN_CALENDAR_YEAR ||
    year > MAX_CALENDAR_YEAR ||
    month < 1 ||
    month > 12
  ) {
    throw new RangeError(`Not a calendar month: ${year}-${month}`);
  }
}

/** `2026-07-08` → `08/07/2026`. */
export function formatDate(date: CalendarDate): string {
  const { year, month, day } = calendarDateParts(date);
  return `${two(day)}/${two(month)}/${four(year)}`;
}

/** Typed `dd/MM/yyyy` (surrounding spaces ignored) → calendar date, or null for any other text or an impossible date. */
export function parseDateInput(text: string): CalendarDate | null {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(text.trim());
  if (match === null) return null;
  return parseCalendarDate(`${match[3]}-${match[2]}-${match[1]}`);
}

/** Month name, lowercase: 9 → `setembro`. */
export function monthName(month: number): string {
  const name = monthNames[month - 1];
  if (name === undefined || !Number.isInteger(month)) throw new RangeError(`Not a month: ${month}`);
  return name;
}

/** Weekday name, lowercase and hyphenated: 2 → `terça-feira` (0 = domingo). */
export function weekdayName(weekday: number): string {
  const name = weekdayNames[weekday];
  if (name === undefined || !Number.isInteger(weekday))
    throw new RangeError(`Not a weekday: ${weekday}`);
  return name;
}

/** Weekday abbreviation for calendar headers: 2 → `ter.` (0 = domingo). */
export function weekdayShortName(weekday: number): string {
  const name = weekdayAbbreviations[weekday];
  if (name === undefined || !Number.isInteger(weekday))
    throw new RangeError(`Not a weekday: ${weekday}`);
  return name;
}

/** Header long date `d 'de' MMMM 'de' yyyy`: `8 de julho de 2026`. */
export function formatLongDate(date: CalendarDate): string {
  const { year, month, day } = calendarDateParts(date);
  return `${day} de ${monthName(month)} de ${four(year)}`;
}

/** The full name of a calendar day: `quinta-feira, 24 de setembro de 2026`. */
export function formatDateWithWeekday(date: CalendarDate): string {
  return `${weekdayName(weekdayOf(date))}, ${formatLongDate(date)}`;
}

/** Calendar heading: `setembro de 2026`. */
export function formatMonthYear(year: number, month: number): string {
  checkMonth(year, month);
  return `${monthName(month)} de ${four(year)}`;
}

/** Month field: `09/2026`. */
export function formatMonthField(year: number, month: number): string {
  checkMonth(year, month);
  return `${two(month)}/${four(year)}`;
}

export interface WallTime {
  readonly hour: number;
  readonly minute: number;
  readonly second: number;
}

const timePattern = /^(\d{2}):(\d{2})(?::(\d{2})(?:\.\d{1,9})?)?$/;

/** A branch wall-clock time (`HH:mm`, `HH:mm:ss`, `HH:mm:ss.fffffff`); null for anything else. */
export function parseWallTime(value: string): WallTime | null {
  const match = timePattern.exec(value);
  if (match === null) return null;
  const [hour, minute, second] = [Number(match[1]), Number(match[2]), Number(match[3] ?? '0')];
  return hour > 23 || minute > 59 || second > 59 ? null : { hour, minute, second };
}

/** `14:30:59` → `14:30`: seconds are dropped like a clock, never rounded up into the next minute. */
export function formatWallTime(value: string): string {
  const time = parseWallTime(value);
  if (time === null) throw new RangeError(`Not a wall-clock time: ${value}`);
  return `${two(time.hour)}:${two(time.minute)}`;
}

/**
 * An offset-free branch wall time (`2026-07-08T14:30:00`, `ClockIn`) → `08/07/2026 14:30`, exactly
 * as returned. A value with `Z` or an offset is a UTC instant and throws — use `formatInstant`.
 */
export function formatWallDateTime(value: string): string {
  const separator = value.indexOf('T');
  const date = separator === 10 ? parseCalendarDate(value.slice(0, 10)) : null;
  const time = date === null ? null : parseWallTime(value.slice(11));
  if (date === null || time === null) throw new RangeError(`Not a branch wall time: ${value}`);
  return `${formatDate(date)} ${two(time.hour)}:${two(time.minute)}`;
}
