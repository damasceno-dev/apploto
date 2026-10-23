// Calendar arithmetic for the date controls: integers over `YYYY-MM-DD` strings. No JS `Date`, no
// device clock and no time zone — a business date is a calendar value (formatting §4), and "today"
// is whatever the branch clock says (`GET /branch/current`), passed in by the screen.
import { asCalendarDate, daysInMonth, type CalendarDate } from './format-port';

export interface CalendarParts {
  readonly year: number;
  readonly month: number;
  readonly day: number;
}

export function partsOf(date: CalendarDate): CalendarParts {
  return {
    year: Number(date.slice(0, 4)),
    month: Number(date.slice(5, 7)),
    day: Number(date.slice(8, 10)),
  };
}

export function calendarDate(year: number, month: number, day: number): CalendarDate {
  const text = `${String(year).padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
  const date = asCalendarDate(text);
  if (date === null) throw new RangeError(`Not a calendar date: ${text}`);
  return date;
}

/** 0 = domingo … 6 = sábado (Sakamoto's method; Brazilian calendars start the week on Sunday). */
export function weekdayOf(date: CalendarDate): number {
  const offsets = [0, 3, 2, 5, 0, 3, 5, 1, 4, 6, 2, 4];
  let { year } = partsOf(date);
  const { month, day } = partsOf(date);
  if (month < 3) year -= 1;
  return (
    (year +
      Math.floor(year / 4) -
      Math.floor(year / 100) +
      Math.floor(year / 400) +
      (offsets[month - 1] ?? 0) +
      day) %
    7
  );
}

export function addDays(date: CalendarDate, days: number): CalendarDate {
  let { year, month, day } = partsOf(date);
  let remaining = days;
  while (remaining !== 0) {
    if (remaining > 0) {
      const room = daysInMonth(year, month) - day;
      if (remaining <= room) {
        day += remaining;
        remaining = 0;
      } else {
        remaining -= room + 1;
        day = 1;
        [year, month] = month === 12 ? [year + 1, 1] : [year, month + 1];
      }
    } else if (-remaining < day) {
      day += remaining;
      remaining = 0;
    } else {
      remaining += day;
      [year, month] = month === 1 ? [year - 1, 12] : [year, month - 1];
      day = daysInMonth(year, month);
    }
  }
  return calendarDate(year, month, day);
}

/** The same day `months` later or earlier, clamped to the target month's length. */
export function addMonths(date: CalendarDate, months: number): CalendarDate {
  const { year, month, day } = partsOf(date);
  const index = year * 12 + (month - 1) + months;
  const nextYear = Math.floor(index / 12);
  const nextMonth = (index % 12) + 1;
  return calendarDate(nextYear, nextMonth, Math.min(day, daysInMonth(nextYear, nextMonth)));
}

/** ISO calendar strings order lexicographically. */
export function compareDates(a: CalendarDate, b: CalendarDate): number {
  return a < b ? -1 : a > b ? 1 : 0;
}

/** The weeks of a month for a grid: each week Sunday..Saturday, `null` outside the month. */
export function monthWeeks(year: number, month: number): (CalendarDate | null)[][] {
  const first = calendarDate(year, month, 1);
  const lead = weekdayOf(first);
  const cells: (CalendarDate | null)[] = Array.from({ length: lead }, () => null);
  for (let day = 1; day <= daysInMonth(year, month); day += 1)
    cells.push(calendarDate(year, month, day));
  while (cells.length % 7 !== 0) cells.push(null);
  const weeks: (CalendarDate | null)[][] = [];
  for (let index = 0; index < cells.length; index += 7) weeks.push(cells.slice(index, index + 7));
  return weeks;
}

/** `HH:mm`, 24-hour wall clock, as the screen sends and receives it (no conversion). */
export function isWallClockTime(text: string): boolean {
  return /^(?:[01]\d|2[0-3]):[0-5]\d$/.test(text);
}
