// Calendar arithmetic for the date controls: integers over `YYYY-MM-DD` strings. No JS `Date`, no
// device clock and no time zone — a business date is a calendar value (formatting §4), and "today"
// is whatever the branch clock says (`GET /branch/current`), passed in by the screen.
import {
  addDays,
  addMonths,
  calendarDateFromParts,
  calendarDateParts,
  compareCalendarDates,
  daysInMonth,
  weekdayOf,
  type CalendarDate,
  type CalendarParts,
} from '@loto/core';

// The calendar arithmetic itself is `@loto/core`'s (one implementation for web and mobile); these
// names are the components' existing API over it.
export type { CalendarParts };
export { addDays, addMonths, weekdayOf };

export const partsOf: (date: CalendarDate) => CalendarParts = calendarDateParts;

/** Throws `RangeError` for an impossible date. */
export const calendarDate: (year: number, month: number, day: number) => CalendarDate =
  calendarDateFromParts;

/** ISO calendar strings order lexicographically. */
export const compareDates: (a: CalendarDate, b: CalendarDate) => number = compareCalendarDates;

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
