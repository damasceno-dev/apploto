import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { parseCalendarDate, parseWallDateTime, toBranchClock } from '@/lib/branch-clock';

describe('branch clock', () => {
  it('keeps BranchLocalDate and BranchLocalDateTime as calendar values', () => {
    const clock = toBranchClock({
      branch: { id: 'b1', name: 'Centro', role: 'Manager' },
      branchLocalDate: '2026-10-21',
      branchLocalDateTime: '2026-10-21T23:59:58.1234567',
    });
    expect(clock).toEqual({
      branchLocalDate: '2026-10-21',
      branchLocalDateTime: '2026-10-21T23:59:58.1234567',
      date: { year: 2026, month: 10, day: 21 },
      time: { hour: 23, minute: 59, second: 58 },
      liveRole: 'Manager',
      branchId: 'b1',
    });
  });

  it('rejects instants with an offset, impossible dates and disagreeing captures', () => {
    expect(parseWallDateTime('2026-10-21T10:00:00Z')).toBeNull();
    expect(parseWallDateTime('2026-10-21T10:00:00-03:00')).toBeNull();
    expect(parseCalendarDate('2026-02-29')).toBeNull();
    expect(parseCalendarDate('2024-02-29')).toEqual({ year: 2024, month: 2, day: 29 });
    expect(parseCalendarDate('21/10/2026')).toBeNull();
    expect(
      toBranchClock({
        branch: { id: 'b1', name: 'Centro', role: 'Member' },
        branchLocalDate: '2026-10-21',
        branchLocalDateTime: '2026-10-22T00:00:01',
      }),
    ).toBeNull();
  });

  it('never consults the device clock or builds a Date', () => {
    for (const file of ['src/lib/branch-clock.ts', 'src/client/clock/use-branch-clock.ts']) {
      const source = readFileSync(file, 'utf8').replace(/\/\/.*$|\/\*[\s\S]*?\*\//gm, '');
      expect(source, file).not.toMatch(/\bDate\s*[.(]|new Date|Date\.now|getTimezoneOffset|Intl\./);
    }
  });
});
