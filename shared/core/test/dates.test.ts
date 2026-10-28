import { describe, expect, it, vi } from 'vitest';
import {
  addDays,
  addMonths,
  calendarDateFromParts,
  calendarDateParts,
  compareCalendarDates,
  daysBetween,
  daysInMonth,
  formatDate,
  formatDateWithWeekday,
  formatInstant,
  formatInstantDate,
  formatInstantTime,
  formatLongDate,
  formatMonthField,
  formatMonthYear,
  formatWallDateTime,
  formatWallTime,
  isLeapYear,
  monthName,
  parseCalendarDate,
  parseDateInput,
  parseInstant,
  parseWallTime,
  weekdayName,
  weekdayOf,
  weekdayShortName,
  type CalendarDate,
} from '../src/index';
import { integer, seeded } from './support/random';

const d = (text: string) => {
  const date = parseCalendarDate(text);
  if (date === null) throw new Error(`bad fixture ${text}`);
  return date;
};

// Test-only oracle: the engine's proleptic-Gregorian UTC calendar (the helpers never use Date).
function oracle(date: CalendarDate, days: number) {
  const { year, month, day } = calendarDateParts(date);
  const value = new Date(0);
  value.setUTCFullYear(year, month - 1, day + days);
  return {
    iso: `${String(value.getUTCFullYear()).padStart(4, '0')}-${String(value.getUTCMonth() + 1).padStart(2, '0')}-${String(value.getUTCDate()).padStart(2, '0')}`,
    weekday: value.getUTCDay(),
  };
}

describe('calendar validation', () => {
  it('accepts only real dates in years 0001–9999', () => {
    for (const text of ['2026-07-08', '2024-02-29', '2000-02-29', '0001-01-01', '9999-12-31']) {
      expect(parseCalendarDate(text)).toBe(text);
    }
    for (const text of [
      '2026-02-29',
      '1900-02-29',
      '2026-04-31',
      '2026-13-01',
      '2026-00-10',
      '2026-01-00',
      '0000-01-01',
      '2026-7-8',
      '2026-07-08T00:00:00',
      '2026-07-08Z',
      ' 2026-07-08',
      '２０２６-07-08',
    ]) {
      expect(parseCalendarDate(text)).toBeNull();
    }
  });

  it('knows leap years and month lengths', () => {
    expect([1900, 2000, 2024, 2026, 2100, 2400].map(isLeapYear)).toEqual([
      false,
      true,
      true,
      false,
      false,
      true,
    ]);
    expect(daysInMonth(2024, 2)).toBe(29);
    expect(daysInMonth(2026, 2)).toBe(28);
    expect([4, 6, 9, 11].map((month) => daysInMonth(2026, month))).toEqual([30, 30, 30, 30]);
    expect(() => calendarDateFromParts(2026, 2, 29)).toThrow(RangeError);
    expect(calendarDateFromParts(26, 1, 5)).toBe('0026-01-05');
  });
});

describe('calendar arithmetic (no Date, no zone)', () => {
  it('crosses month, year and leap boundaries', () => {
    expect(addDays(d('2026-12-31'), 1)).toBe('2027-01-01');
    expect(addDays(d('2027-01-01'), -1)).toBe('2026-12-31');
    expect(addDays(d('2024-02-28'), 1)).toBe('2024-02-29');
    expect(addDays(d('2026-02-28'), 1)).toBe('2026-03-01');
    expect(addDays(d('2026-03-01'), -1)).toBe('2026-02-28');
    expect(addMonths(d('2026-01-31'), 1)).toBe('2026-02-28');
    expect(addMonths(d('2024-01-31'), 1)).toBe('2024-02-29');
    expect(addMonths(d('2026-03-31'), -1)).toBe('2026-02-28');
    expect(addMonths(d('2026-12-15'), 1)).toBe('2027-01-15');
    expect(addMonths(d('2026-01-15'), -13)).toBe('2024-12-15');
    expect(() => addDays(d('9999-12-31'), 1)).toThrow(RangeError);
    expect(() => addDays(d('0001-01-01'), -1)).toThrow(RangeError);
    expect(daysBetween(d('2026-01-01'), d('2027-01-01'))).toBe(365);
    expect(daysBetween(d('2024-03-01'), d('2024-02-01'))).toBe(-29);
    expect(compareCalendarDates(d('2026-09-24'), d('2026-10-01'))).toBe(-1);
    expect(compareCalendarDates(d('2026-09-24'), d('2026-09-24'))).toBe(0);
  });

  it('agrees with an independent UTC calendar for random dates, offsets and weekdays (seeded)', () => {
    const random = seeded(31);
    for (let run = 0; run < 3000; run += 1) {
      const date = calendarDateFromParts(
        integer(random, 1600, 2400),
        integer(random, 1, 12),
        integer(random, 1, 28),
      );
      const days = integer(random, -40000, 40000);
      const expected = oracle(date, days);
      expect(addDays(date, days)).toBe(expected.iso);
      expect(weekdayOf(date)).toBe(oracle(date, 0).weekday);
      expect(daysBetween(date, addDays(date, days))).toBe(days);
    }
  });

  it('pins known weekdays (0 = domingo)', () => {
    expect(weekdayOf(d('2026-09-24'))).toBe(4);
    expect(weekdayOf(d('2026-07-08'))).toBe(3);
    expect(weekdayOf(d('2000-01-01'))).toBe(6);
    expect(weekdayOf(d('1970-01-01'))).toBe(4);
  });
});

describe('date display (§4)', () => {
  it('formats dd/MM/yyyy, long dates and lowercase names', () => {
    expect(formatDate(d('2026-07-08'))).toBe('08/07/2026');
    expect(formatDate(d('0026-01-05'))).toBe('05/01/0026');
    expect(formatLongDate(d('2026-07-08'))).toBe('8 de julho de 2026');
    expect(formatLongDate(d('2026-10-01'))).toBe('1 de outubro de 2026');
    expect(formatDateWithWeekday(d('2026-09-24'))).toBe('quinta-feira, 24 de setembro de 2026');
    expect(formatDateWithWeekday(d('2026-09-22'))).toBe('terça-feira, 22 de setembro de 2026');
    expect(formatMonthYear(2026, 3)).toBe('março de 2026');
    expect(formatMonthField(2026, 9)).toBe('09/2026');
    expect(Array.from({ length: 12 }, (_, index) => monthName(index + 1))).toEqual([
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
    ]);
    expect(Array.from({ length: 7 }, (_, index) => weekdayName(index))).toEqual([
      'domingo',
      'segunda-feira',
      'terça-feira',
      'quarta-feira',
      'quinta-feira',
      'sexta-feira',
      'sábado',
    ]);
    expect(Array.from({ length: 7 }, (_, index) => weekdayShortName(index))).toEqual([
      'dom.',
      'seg.',
      'ter.',
      'qua.',
      'qui.',
      'sex.',
      'sáb.',
    ]);
    for (const bad of [0, 13, 1.5]) expect(() => monthName(bad)).toThrow(RangeError);
    for (const bad of [-1, 7]) expect(() => weekdayName(bad)).toThrow(RangeError);
    expect(() => formatMonthYear(2026, 13)).toThrow(RangeError);
    expect(() => formatMonthField(0, 1)).toThrow(RangeError);
  });

  it('parses the dd/MM/yyyy input strictly and validates the calendar', () => {
    expect(parseDateInput('24/09/2026')).toBe('2026-09-24');
    expect(parseDateInput(' 29/02/2024 ')).toBe('2024-02-29');
    for (const text of [
      '29/02/2026',
      '31/04/2026',
      '00/01/2026',
      '24/9/2026',
      '2026-09-24',
      '24-09-2026',
      '24/09/26',
      '',
      '24/09/2026x',
    ]) {
      expect(parseDateInput(text)).toBeNull();
    }
    const random = seeded(5);
    for (let run = 0; run < 500; run += 1) {
      const date = calendarDateFromParts(
        integer(random, 1, 9999),
        integer(random, 1, 12),
        integer(random, 1, 28),
      );
      expect(parseDateInput(formatDate(date))).toBe(date);
    }
  });
});

describe('branch wall clock (§4: render exactly as returned)', () => {
  it('formats times and offset-free date-times without conversion', () => {
    expect(formatWallTime('09:05')).toBe('09:05');
    expect(formatWallTime('14:30:59')).toBe('14:30');
    expect(formatWallTime('23:59:59.9999999')).toBe('23:59');
    expect(formatWallDateTime('2026-07-08T14:30:00')).toBe('08/07/2026 14:30');
    expect(formatWallDateTime('2026-12-31T23:59:59.1234567')).toBe('31/12/2026 23:59');
    expect(parseWallTime('24:00')).toBeNull();
    expect(parseWallTime('12:60')).toBeNull();
    expect(parseWallTime('9:05')).toBeNull();
    for (const bad of [
      '2026-07-08T14:30:00Z',
      '2026-07-08T14:30:00-03:00',
      '2026-07-08 14:30',
      '2026-02-30T10:00',
    ]) {
      expect(() => formatWallDateTime(bad)).toThrow(RangeError);
    }
  });
});

describe('UTC audit instants → the caller-supplied device zone (§4)', () => {
  it.each([
    // São Paulo (UTC−03:00, no DST since 2019): the operator at the branch.
    ['2026-07-08T17:30:00Z', 'America/Sao_Paulo', '08/07/2026 14:30'],
    ['2026-01-01T02:00:00Z', 'America/Sao_Paulo', '31/12/2025 23:00'],
    ['2026-07-08T14:30:00-03:00', 'America/Sao_Paulo', '08/07/2026 14:30'],
    ['2026-07-08T17:30:00.1234567Z', 'America/Sao_Paulo', '08/07/2026 14:30'],
    ['2026-07-08T17:30:59.999Z', 'America/Sao_Paulo', '08/07/2026 14:30'],
    // Manaus (UTC−04:00) and Fernando de Noronha (UTC−02:00): other Brazilian zones.
    ['2026-07-08T17:30:00Z', 'America/Manaus', '08/07/2026 13:30'],
    ['2026-07-08T17:30:00Z', 'America/Noronha', '08/07/2026 15:30'],
    // A remote manager: New York across the 2026 DST start (08/03 at 02:00 local).
    ['2026-03-08T06:59:00Z', 'America/New_York', '08/03/2026 01:59'],
    ['2026-03-08T07:00:00Z', 'America/New_York', '08/03/2026 03:00'],
    // …and the DST end (01/11 at 02:00 local, the 01:xx hour repeats).
    ['2026-11-01T05:30:00Z', 'America/New_York', '01/11/2026 01:30'],
    ['2026-11-01T06:30:00Z', 'America/New_York', '01/11/2026 01:30'],
    // Half- and quarter-hour offsets.
    ['2026-07-08T17:30:00Z', 'Asia/Kolkata', '08/07/2026 23:00'],
    ['2026-07-08T17:30:00Z', 'Pacific/Chatham', '09/07/2026 06:15'],
    ['2026-07-08T00:00:00Z', 'UTC', '08/07/2026 00:00'],
    ['2026-07-08T23:59:00+14:00', 'UTC', '08/07/2026 09:59'],
  ])('%s in %s → %s', (value, zone, text) => {
    expect(formatInstant(value, zone)).toBe(text);
    expect(formatInstantDate(value, zone)).toBe(text.slice(0, 10));
    expect(formatInstantTime(value, zone)).toBe(text.slice(11));
  });

  it('prints midnight as 00:00 (never 24:00)', () => {
    expect(formatInstantTime('2026-07-08T03:00:00Z', 'America/Sao_Paulo')).toBe('00:00');
  });

  it('rejects a conversion that leaves the supported years 0001–9999, at both ends', () => {
    // Intl reports 1 BC as year "1" with era BC: never shown as 31/12/0001.
    expect(() => formatInstant('0001-01-01T00:00:00Z', 'Etc/GMT+1')).toThrow(RangeError);
    expect(() => formatInstantDate('0001-01-01T00:30:00+01:00', 'UTC')).toThrow(RangeError);
    expect(formatInstant('0001-01-01T00:00:00Z', 'UTC')).toBe('01/01/0001 00:00');
    expect(formatInstant('0001-01-01T00:00:00Z', 'Etc/GMT-1')).toBe('01/01/0001 01:00');
    expect(() => formatInstant('9999-12-31T23:00:00Z', 'Asia/Tokyo')).toThrow(RangeError);
    expect(() => formatInstantTime('9999-12-31T23:59:00Z', 'Etc/GMT-1')).toThrow(RangeError);
    expect(formatInstant('9999-12-31T23:59:00Z', 'UTC')).toBe('31/12/9999 23:59');
    expect(formatInstant('9999-12-31T23:00:00Z', 'Etc/GMT+1')).toBe('31/12/9999 22:00');
  });

  it('rejects offset-free values, impossible instants and unknown zones', () => {
    expect(parseInstant('2026-07-08T14:30:00')).toBeNull();
    expect(parseInstant('2026-02-30T00:00:00Z')).toBeNull();
    expect(parseInstant('2026-07-08T24:00:00Z')).toBeNull();
    expect(parseInstant('2026-07-08T10:00:00+24:00')).toBeNull();
    expect(parseInstant('2026-07-08')).toBeNull();
    expect(parseInstant('2026-07-08T17:30:00Z')).toBe(Date.UTC(2026, 6, 8, 17, 30));
    expect(parseInstant('0099-01-01T00:00:00Z')).toBe(-683_368 * 86_400_000); // 1871 years × 365 + 453 leap days before 1970;
    expect(() => formatInstant('2026-07-08T14:30:00', 'America/Sao_Paulo')).toThrow(RangeError);
    expect(() => formatInstant('2026-07-08T14:30:00Z', 'Not/AZone')).toThrow(RangeError);
    expect(() => formatInstant('2026-07-08T14:30:00Z', '')).toThrow(RangeError);
  });
});

// Android API 36 Hermes reproduced hybrid calendar parts before 1582. The offset remains
// usable; the formatter must derive the civil date from epoch + offset, including seconds.
describe('native historical calendar regression', () => {
  it('ignores hybrid calendar parts and retains historical offset seconds', () => {
    const Original = Intl.DateTimeFormat;
    class HybridFormatter extends Original {
      override formatToParts(value?: Date | number) {
        return super.formatToParts(value).map((part) => {
          if (part.type === 'year') return { ...part, value: '0001' };
          if (part.type === 'day') return { ...part, value: '03' };
          return part;
        });
      }
    }
    vi.stubGlobal('Intl', { ...Intl, DateTimeFormat: HybridFormatter });
    try {
      // A separate alias avoids a formatter cached by an earlier case.
      expect(formatInstant('0001-01-01T00:00:00Z', 'Etc/UTC')).toBe('01/01/0001 00:00');
      expect(() => formatInstant('0001-01-01T00:30:00Z', 'Etc/GMT+2')).toThrow(RangeError);
      expect(formatInstant('1880-01-01T12:00:00Z', 'US/Eastern')).toBe('01/01/1880 07:03');
      expect(formatInstant('1582-10-04T00:00:00Z', 'Etc/UTC')).toBe('04/10/1582 00:00');
      expect(formatInstant('1582-10-15T00:00:00Z', 'Etc/UTC')).toBe('15/10/1582 00:00');
    } finally {
      vi.unstubAllGlobals();
    }
  });
  it('fails closed when the engine supplies no usable offset', () => {
    const Original = Intl.DateTimeFormat;
    class MissingOffset extends Original {
      override formatToParts(value?: Date | number) {
        return super
          .formatToParts(value)
          .map((part) =>
            part.type === 'timeZoneName' ? { ...part, value: 'Eastern Time' } : part,
          );
      }
    }
    vi.stubGlobal('Intl', { ...Intl, DateTimeFormat: MissingOffset });
    try {
      expect(() => formatInstant('2026-07-08T00:00:00Z', 'Canada/Eastern')).toThrow(RangeError);
    } finally {
      vi.unstubAllGlobals();
    }
  });
});

describe('iOS Hermes split zone-offset regression', () => {
  it('does not mistake a GMT prefix with misclassified numeric suffix parts for UTC', () => {
    const Original = Intl.DateTimeFormat;
    class SplitOffset extends Original {
      override formatToParts(value?: Date | number) {
        return super.formatToParts(value).flatMap((part) => {
          if (part.type !== 'timeZoneName' || part.value === 'GMT') return [part];
          return [
            { type: 'timeZoneName', value: 'GMT' },
            ...Array.from(part.value.slice(3), (value, index) => ({
              type: index % 2 === 0 ? 'literal' : 'hour',
              value,
            })),
          ] as Intl.DateTimeFormatPart[];
        });
      }
    }
    vi.stubGlobal('Intl', { ...Intl, DateTimeFormat: SplitOffset });
    try {
      expect(formatInstant('2026-07-08T17:30:00Z', 'Brazil/East')).toBe('08/07/2026 14:30');
      expect(formatInstant('2026-07-08T17:30:00Z', 'Asia/Calcutta')).toBe('08/07/2026 23:00');
      expect(formatInstant('1880-01-01T12:00:00Z', 'America/Detroit')).toBe('01/01/1880 06:27');
      expect(() => formatInstant('0001-01-01T00:30:00Z', 'Etc/GMT+3')).toThrow(RangeError);
    } finally {
      vi.unstubAllGlobals();
    }
  });
});

describe('Android zone-prefix regression', () => {
  it('does not append a following clock to a complete zone name or bare UTC', () => {
    const Original = Intl.DateTimeFormat;
    class PrefixOffset extends Original {
      override formatToParts(value?: Date | number) {
        const parts = super.formatToParts(value);
        const zone = parts.find((part) => part.type === 'timeZoneName')!;
        return [
          zone,
          { type: 'literal', value: ' ' },
          ...parts.filter((part) => part.type !== 'timeZoneName'),
        ] as Intl.DateTimeFormatPart[];
      }
    }
    vi.stubGlobal('Intl', { ...Intl, DateTimeFormat: PrefixOffset });
    try {
      expect(formatInstant('2026-07-08T17:30:00Z', 'Etc/GMT+4')).toBe('08/07/2026 13:30');
      expect(formatInstant('0001-01-01T00:00:00Z', 'UCT')).toBe('01/01/0001 00:00');
    } finally {
      vi.unstubAllGlobals();
    }
  });
});
