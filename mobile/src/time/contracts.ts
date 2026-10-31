import {
  TimeEntryStatus,
  type Role,
  type ResponseTimeEntryJson,
  type RequestUpsertTimeEntryJson,
  type ResponseTimeEntryBalanceSummaryDayJson,
} from '@loto/api';
import {
  addDays,
  addMonths,
  daysBetween,
  parseCalendarDate,
  parseDateInput,
  parseWallTime,
  formatWallDateTime,
  type CalendarDate,
} from '@loto/core';
import { isServerId } from '@/session/credentials';

export const datePart = (value: string) => parseCalendarDate(value.slice(0, 10));
export interface Period {
  from: CalendarDate;
  to: CalendarDate;
}
export function periodFor(today: CalendarDate, from?: string, to?: string): Period | null {
  if (from !== undefined || to !== undefined) {
    const start = from && parseCalendarDate(from),
      end = to && parseCalendarDate(to);
    return start && end && daysBetween(start, end) >= 0 && daysBetween(start, end) <= 366
      ? { from: start, to: end }
      : null;
  }
  const start = parseCalendarDate(today.slice(0, 8) + '01')!;
  return { from: start, to: addDays(addMonths(start, 1), -1) };
}
export function reportContext(role: Role, own: boolean, operatorId?: string) {
  if (role === 'Member' || own) return { Mine: true } as const;
  if (operatorId && isServerId(operatorId)) return { OperatorId: operatorId } as const;
  return {};
}
export function dayTrail(period: Period, days: readonly ResponseTimeEntryBalanceSummaryDayJson[]) {
  const rows = new Map(days.map((day) => [day.date.slice(0, 10), day]));
  const count = daysBetween(period.from, period.to);
  if (count < 0 || count > 366) return [];
  return Array.from({ length: count + 1 }, (_, index) => {
    const date = addDays(period.from, index);
    return { date, entry: rows.get(date) ?? null };
  });
}
export function memberTap(
  operatorId: string,
  date: CalendarDate,
  action: 'Open' | 'Close',
): RequestUpsertTimeEntryJson {
  return { operatorId, date, status: 'Present', action };
}
export interface SegmentInput {
  id: string | null;
  clockIn: string;
  clockOut: string | null;
}
export interface DayInput {
  operatorId: string;
  date: string;
  status: TimeEntryStatus;
  segments: SegmentInput[];
}
export function dayInput(entry: ResponseTimeEntryJson): DayInput {
  return {
    operatorId: entry.operatorId,
    date: entry.date.slice(0, 10),
    status: entry.status,
    segments: entry.segments
      .filter((s) => s.active)
      .map((s) => ({ id: s.id, clockIn: s.clockIn, clockOut: s.clockOut ?? null })),
  };
}
const wall = /^\d{4}-\d{2}-\d{2}T(?:[01]\d|2[0-3]):[0-5]\d(?::[0-5]\d(?:\.\d{1,7})?)?$/;
export function validWall(value: string): boolean {
  return wall.test(value) && datePart(value) !== null;
}
/** Retained typed input can be incomplete; only exact non-secret fields are accepted. */
export function isDayInput(value: unknown): value is DayInput {
  if (!value || typeof value !== 'object') return false;
  const v = value as DayInput;
  return (
    Object.keys(v).sort().join(',') === 'date,operatorId,segments,status' &&
    isServerId(v.operatorId) &&
    typeof v.date === 'string' &&
    parseCalendarDate(v.date) !== null &&
    Object.values(TimeEntryStatus).includes(v.status) &&
    Array.isArray(v.segments) &&
    v.segments.length <= 100 &&
    v.segments.every(
      (s) =>
        s &&
        Object.keys(s).sort().join(',') === 'clockIn,clockOut,id' &&
        (s.id === null || isServerId(s.id)) &&
        typeof s.clockIn === 'string' &&
        s.clockIn.length <= 40 &&
        (s.clockOut === null || (typeof s.clockOut === 'string' && s.clockOut.length <= 40)),
    )
  );
}
/** No omission deletes, and existing ClockIn can change only via the granular endpoint. */
export function snapshotPayload(
  input: DayInput,
  base: ResponseTimeEntryJson | null,
): RequestUpsertTimeEntryJson {
  if (
    !isDayInput(input) ||
    !parseCalendarDate(input.date) ||
    input.segments.some(
      (s) => !validWall(s.clockIn) || (s.clockOut !== null && !validWall(s.clockOut)),
    )
  )
    throw new Error('Confira a data e os horários dos segmentos. Use data e hora da filial.');
  if (base) {
    if (base.operatorId !== input.operatorId || base.date.slice(0, 10) !== input.date)
      throw new Error('O operador e a data do registro não podem mudar.');
    for (const segment of base.segments.filter((s) => s.active)) {
      const proposed = input.segments.find((s) => s.id === segment.id);
      if (!proposed || proposed.clockIn !== segment.clockIn)
        throw new Error(
          'Releia o registro. Para mudar a entrada ou remover, use a ação do segmento.',
        );
    }
    if (input.segments.some((s) => s.id && !base.segments.some((b) => b.active && b.id === s.id)))
      throw new Error('Releia os segmentos antes de salvar.');
  } else if (input.segments.some((s) => s.id !== null))
    throw new Error('Um novo segmento precisa de identificação vazia.');
  return {
    operatorId: input.operatorId,
    date: input.date,
    status: input.status,
    segments: input.segments,
  };
}
/** Compare persisted identity/audit, not live totals (running totals change on reads). No concurrency guarantee. */
export function resourceStamp(entry: ResponseTimeEntryJson | null): string {
  return JSON.stringify(
    entry && {
      id: entry.id,
      active: entry.active,
      status: entry.status,
      updatedAt: entry.updatedAt,
      segments: entry.segments.map((s) => ({
        id: s.id,
        active: s.active,
        clockIn: s.clockIn,
        clockOut: s.clockOut,
        updatedAt: s.updatedAt,
      })),
    },
  );
}
export function periodQuery(period: Period) {
  return new URLSearchParams({ from: period.from, to: period.to });
}

/** Editable pt-BR wall time, with incomplete text retained verbatim until it is valid. */
export function wallInput(value: string): string {
  return validWall(value) ? formatWallDateTime(value) : value;
}
export function wallFromInput(value: string): string {
  if (validWall(value)) return value;
  const match = /^(\d{2}\/\d{2}\/\d{4}) (\d{2}:\d{2})$/.exec(value);
  const date = match && parseDateInput(match[1]!);
  return date && parseWallTime(match![2]!) ? date + 'T' + match![2] + ':00' : value;
}
