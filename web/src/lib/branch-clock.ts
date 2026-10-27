// Branch business clock (catalog: Server time and current-day authority). `GET /branch/current`
// returns `BranchLocalDate` (`YYYY-MM-DD`) and `BranchLocalDateTime` (offset-free branch wall
// time) from one server capture. They stay calendar values: this module never builds a `Date`,
// never applies a device timezone and never advances time on its own.
import type { ResponseGetCurrentBranchSummaryJson } from '@loto/api';
import { calendarDateParts, parseCalendarDate as parseIsoDate } from '@loto/core';

export interface CalendarDate {
  readonly year: number;
  readonly month: number;
  readonly day: number;
}

export interface WallTime {
  readonly hour: number;
  readonly minute: number;
  readonly second: number;
}

export interface BranchClock {
  /** Exactly as the API sent it, for requests and comparisons. */
  readonly branchLocalDate: string;
  readonly branchLocalDateTime: string;
  readonly date: CalendarDate;
  readonly time: WallTime;
  /** The live role from current membership; compare it with the session's. */
  readonly liveRole: ResponseGetCurrentBranchSummaryJson['branch']['role'];
  readonly branchId: string;
}

const dateTimePattern = /^(\d{4}-\d{2}-\d{2})T(\d{2}):(\d{2})(?::(\d{2})(?:\.\d{1,7})?)?$/;

/** The parts of a real calendar date (`@loto/core` validation), or null. */
export function parseCalendarDate(value: string): CalendarDate | null {
  const date = parseIsoDate(value);
  return date === null ? null : calendarDateParts(date);
}

/** Parses the offset-free wall time; a value with an offset or `Z` is rejected, not converted. */
export function parseWallDateTime(value: string): { date: CalendarDate; time: WallTime } | null {
  const match = dateTimePattern.exec(value);
  if (!match) return null;
  const date = parseCalendarDate(match[1] ?? '');
  const [hour, minute, second] = [Number(match[2]), Number(match[3]), Number(match[4] ?? '0')];
  if (date === null || hour > 23 || minute > 59 || second > 59) return null;
  return { date, time: { hour, minute, second } };
}

export function toBranchClock(response: ResponseGetCurrentBranchSummaryJson): BranchClock | null {
  const date = parseCalendarDate(response.branchLocalDate);
  const wall = parseWallDateTime(response.branchLocalDateTime);
  if (date === null || wall === null || !sameDate(date, wall.date)) return null;
  return {
    branchLocalDate: response.branchLocalDate,
    branchLocalDateTime: response.branchLocalDateTime,
    date,
    time: wall.time,
    liveRole: response.branch.role,
    branchId: response.branch.id,
  };
}

export function sameDate(left: CalendarDate, right: CalendarDate): boolean {
  return left.year === right.year && left.month === right.month && left.day === right.day;
}
