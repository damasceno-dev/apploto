import { type ResponseTimeEntryJson } from '@loto/api';
import { parseCalendarDate } from '@loto/core';
import {
  dayInput,
  dayTrail,
  isDayInput,
  memberTap,
  periodFor,
  reportContext,
  resourceStamp,
  snapshotPayload,
} from '../../src/time/contracts';
import { isClockAttempt } from '../../src/time/attempt';
import { validateTimeResource } from '../../src/time/resources';
import { branchId, userId, rig, response } from './support';
const today = parseCalendarDate('2026-10-30')!;
const id = '30000000-0000-4000-8000-000000000001';
const segmentId = '40000000-0000-4000-8000-000000000001';
export const entry: ResponseTimeEntryJson = {
  id,
  branchId,
  operatorId: userId,
  operatorName: 'Pessoa teste',
  date: today,
  status: 'Present',
  totalHours: 3,
  balanceHours: -5,
  isInProgress: false,
  active: true,
  createdAt: '2026-10-30T12:00:00Z',
  segments: [
    {
      id: segmentId,
      clockIn: today + 'T08:00:00',
      clockOut: today + 'T11:00:00',
      active: true,
      createdAt: '2026-10-30T12:00:00Z',
    },
  ],
};
it('preserves a source day window and handles leap years and the server’s exact 366-day span limit', () => {
  expect(periodFor(today)).toEqual({ from: '2026-10-01', to: '2026-10-31' });
  expect(periodFor(today, '2024-02-29', '2024-02-29')).toEqual({
    from: '2024-02-29',
    to: '2024-02-29',
  });
  expect(periodFor(today, '2024-01-01', '2025-01-01')).not.toBeNull();
  expect(dayTrail(periodFor(today, '2024-01-01', '2025-01-01')!, [])).toHaveLength(367);
  for (const window of [
    ['2024-01-01', '2025-01-02'],
    ['2026-02-30', '2026-03-01'],
    ['2026-10-30', '2026-10-01'],
    ['2026-10-01', undefined],
  ])
    expect(periodFor(today, ...window)).toBeNull();
});
it.each(['Member', 'Manager', 'Admin'] as const)(
  'keeps %s own/operator/team contexts mutually exclusive',
  (role) => {
    expect(reportContext(role, true, id)).toEqual({ Mine: true });
    expect(reportContext(role, false, id)).toEqual(
      role === 'Member' ? { Mine: true } : { OperatorId: id },
    );
    expect(reportContext(role, false)).toEqual(role === 'Member' ? { Mine: true } : {});
  },
);
it('sends explicit overnight Close on yesterday, and today Open without Segments', () => {
  expect(memberTap(userId, parseCalendarDate('2026-10-29')!, 'Close')).toEqual({
    operatorId: userId,
    date: '2026-10-29',
    status: 'Present',
    action: 'Close',
  });
  expect(memberTap(userId, today, 'Open')).not.toHaveProperty('segments');
});
it('retains every persisted active id and immutable snapshot ClockIn, with null ids for new rows', () => {
  const input = dayInput(entry);
  input.segments.push({ id: null, clockIn: today + 'T12:00:00', clockOut: null });
  expect(snapshotPayload(input, entry).segments?.map((s) => s.id)).toEqual([segmentId, null]);
  expect(snapshotPayload(input, entry)).not.toHaveProperty('action');
  expect(() => snapshotPayload({ ...input, segments: [] }, entry)).toThrow('Releia');
  input.segments[0]!.clockIn = today + 'T09:00:00';
  expect(() => snapshotPayload(input, entry)).toThrow('Releia');
});
it('retained incomplete input has an exact schema and cannot smuggle authority or another identity', () => {
  expect(
    isDayInput({
      ...dayInput(entry),
      segments: [{ id: segmentId, clockIn: '2026-', clockOut: null }],
    }),
  ).toBe(true);
  for (const extra of [{ etag: '123' }, { token: 'private' }, { unknown: true }])
    expect(isDayInput({ ...dayInput(entry), ...extra })).toBe(false);
  expect(isDayInput({ ...dayInput(entry), operatorId: 'invalid' })).toBe(false);
  expect(isDayInput({ ...dayInput(entry), date: '2026-02-30' })).toBe(false);
});
it('does not treat changing live totals as stale audit, but detects changed segments/status', () => {
  expect(resourceStamp({ ...entry, totalHours: 5 })).toBe(resourceStamp(entry));
  expect(resourceStamp({ ...entry, status: 'Vacation' })).not.toBe(resourceStamp(entry));
  expect(resourceStamp({ ...entry, segments: [] })).not.toBe(resourceStamp(entry));
});
it('fills exactly missing dates with no target or invented absence, across a policy change', () => {
  const days = [
    {
      date: '2026-10-29T00:00:00',
      status: 'Present' as const,
      totalHours: 6,
      balanceHours: -2,
      isInProgress: false,
    },
    { date: today, status: 'Present' as const, totalHours: 6, balanceHours: 0, isInProgress: true },
  ];
  const rows = dayTrail({ from: parseCalendarDate('2026-10-28')!, to: today }, days);
  expect(rows.map((r) => r.date)).toEqual(['2026-10-28', '2026-10-29', '2026-10-30']);
  expect(rows[0]!.entry).toBeNull();
  expect(rows.slice(1).map((r) => r.entry!.totalHours - r.entry!.balanceHours)).toEqual([8, 6]);
});
it('resource restoration freshly checks branch ownership and refuses other milestones', async () => {
  const r = rig();
  await r.ready();
  r.intercept(async (path) => (path === '/timeentry/' + id ? response(entry) : undefined));
  expect(await validateTimeResource(r.engine, '/gestao/ponto/' + id)).toBe(true);
  expect(await validateTimeResource(r.engine, '/fechamentos/' + id)).toBe(false);
  r.intercept(async (path) =>
    path === '/timeentry/' + id ? response({ ...entry, branchId: id }) : undefined,
  );
  expect(await validateTimeResource(r.engine, '/gestao/ponto/' + id)).toBe(false);
});

it('retained tap context carries only an owned date/action and no replay or precondition metadata', () => {
  expect(isClockAttempt({ operatorId: userId, date: '2026-10-30', action: 'Close' })).toBe(true);
  expect(isClockAttempt({ operatorId: userId, date: '2026-02-30', action: 'Open' })).toBe(false);
  expect(
    isClockAttempt({ operatorId: userId, date: '2026-10-30', action: 'Close', etag: '123' }),
  ).toBe(false);
});
