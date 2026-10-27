// Business calendar dates (design/specs/formatting-ptbr.md §4: date-only, branch-local). A date is
// the `YYYY-MM-DD` string the API sends and stays a calendar value: integer arithmetic only, no JS
// `Date`, no device clock and no time zone, so it can never shift by a day. "Today" is the branch
// clock's (`GET /branch/current`), passed in by the caller — never read here.

/** A real proleptic-Gregorian calendar date, years 0001–9999, exactly as `YYYY-MM-DD`. */
export type CalendarDate = string & { readonly __calendarDate: true };

export interface CalendarParts {
  readonly year: number;
  readonly month: number;
  readonly day: number;
}

export const MIN_CALENDAR_YEAR = 1;
export const MAX_CALENDAR_YEAR = 9999;

export function isLeapYear(year: number): boolean {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

export function daysInMonth(year: number, month: number): number {
  if (month === 2) return isLeapYear(year) ? 29 : 28;
  return month === 4 || month === 6 || month === 9 || month === 11 ? 30 : 31;
}

function validParts(year: number, month: number, day: number): boolean {
  return (
    Number.isInteger(year) &&
    Number.isInteger(month) &&
    Number.isInteger(day) &&
    year >= MIN_CALENDAR_YEAR &&
    year <= MAX_CALENDAR_YEAR &&
    month >= 1 &&
    month <= 12 &&
    day >= 1 &&
    day <= daysInMonth(year, month)
  );
}

const isoPattern = /^(\d{4})-(\d{2})-(\d{2})$/;

/** Accepts only a real calendar date string (no time, no zone); null otherwise. */
export function parseCalendarDate(value: string): CalendarDate | null {
  const match = isoPattern.exec(value);
  if (match === null) return null;
  return validParts(Number(match[1]), Number(match[2]), Number(match[3]))
    ? (value as CalendarDate)
    : null;
}

/** The calendar date of integer parts; throws `RangeError` for an impossible date. */
export function calendarDateFromParts(year: number, month: number, day: number): CalendarDate {
  if (!validParts(year, month, day)) {
    throw new RangeError(`Not a calendar date: ${year}-${month}-${day}`);
  }
  const pad = (value: number, width: number) => String(value).padStart(width, '0');
  return `${pad(year, 4)}-${pad(month, 2)}-${pad(day, 2)}` as CalendarDate;
}

export function calendarDateParts(date: CalendarDate): CalendarParts {
  const year = Number(date.slice(0, 4));
  const month = Number(date.slice(5, 7));
  const day = Number(date.slice(8, 10));
  if (!validParts(year, month, day)) throw new RangeError(`Not a calendar date: ${String(date)}`);
  return { year, month, day };
}

// Days since 1970-01-01 in the proleptic Gregorian calendar (H. Hinnant's civil algorithms).
function daysFromCivil({ year, month, day }: CalendarParts): number {
  const y = month <= 2 ? year - 1 : year;
  const era = Math.floor(y / 400);
  const yearOfEra = y - era * 400;
  const dayOfYear = Math.floor((153 * ((month + 9) % 12) + 2) / 5) + day - 1;
  const dayOfEra =
    yearOfEra * 365 + Math.floor(yearOfEra / 4) - Math.floor(yearOfEra / 100) + dayOfYear;
  return era * 146097 + dayOfEra - 719468;
}

function civilFromDays(days: number): CalendarParts {
  const z = days + 719468;
  const era = Math.floor(z / 146097);
  const dayOfEra = z - era * 146097;
  const yearOfEra = Math.floor(
    (dayOfEra -
      Math.floor(dayOfEra / 1460) +
      Math.floor(dayOfEra / 36524) -
      Math.floor(dayOfEra / 146096)) /
      365,
  );
  const dayOfYear =
    dayOfEra - (365 * yearOfEra + Math.floor(yearOfEra / 4) - Math.floor(yearOfEra / 100));
  const shifted = Math.floor((5 * dayOfYear + 2) / 153);
  const day = dayOfYear - Math.floor((153 * shifted + 2) / 5) + 1;
  const month = shifted < 10 ? shifted + 3 : shifted - 9;
  return { year: yearOfEra + era * 400 + (month <= 2 ? 1 : 0), month, day };
}

/** 0 = domingo … 6 = sábado (Brazilian calendars start the week on Sunday). */
export function weekdayOf(date: CalendarDate): number {
  // 1970-01-01 was a Thursday (4).
  return (((daysFromCivil(calendarDateParts(date)) + 4) % 7) + 7) % 7;
}

/** The date `days` later (or earlier); throws `RangeError` outside years 0001–9999. */
export function addDays(date: CalendarDate, days: number): CalendarDate {
  if (!Number.isInteger(days)) throw new RangeError(`Not a whole number of days: ${days}`);
  const { year, month, day } = civilFromDays(daysFromCivil(calendarDateParts(date)) + days);
  return calendarDateFromParts(year, month, day);
}

/** The same day `months` later or earlier, clamped to the target month's length (31/01 + 1 → 28/02). */
export function addMonths(date: CalendarDate, months: number): CalendarDate {
  if (!Number.isInteger(months)) throw new RangeError(`Not a whole number of months: ${months}`);
  const { year, month, day } = calendarDateParts(date);
  const index = year * 12 + (month - 1) + months;
  const nextYear = Math.floor(index / 12);
  const nextMonth = index - nextYear * 12 + 1;
  return calendarDateFromParts(
    nextYear,
    nextMonth,
    Math.min(day, daysInMonth(nextYear, nextMonth)),
  );
}

/** Whole days from `from` to `to` (negative when `to` is earlier). */
export function daysBetween(from: CalendarDate, to: CalendarDate): number {
  return daysFromCivil(calendarDateParts(to)) - daysFromCivil(calendarDateParts(from));
}

/** ISO calendar strings order lexicographically: −1, 0 or 1. */
export function compareCalendarDates(a: CalendarDate, b: CalendarDate): number {
  return a < b ? -1 : a > b ? 1 : 0;
}
