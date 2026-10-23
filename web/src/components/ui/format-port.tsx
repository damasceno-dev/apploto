'use client';

import { createContext, useContext, type ReactNode } from 'react';

/** A business calendar date exactly as the API sends it: `YYYY-MM-DD`, never a JS `Date`. */
export type CalendarDate = string & { readonly __calendarDate: true };

const calendarPattern = /^(\d{4})-(\d{2})-(\d{2})$/;

/** Accepts only a real calendar date string (no time, no zone); never constructs a `Date`. */
export function asCalendarDate(value: string): CalendarDate | null {
  const match = calendarPattern.exec(value);
  if (match === null) return null;
  const [year, month, day] = [Number(match[1]), Number(match[2]), Number(match[3])];
  if (month < 1 || month > 12 || day < 1 || day > daysInMonth(year, month)) return null;
  return value as CalendarDate;
}

export function daysInMonth(year: number, month: number): number {
  if (month === 2) return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0 ? 29 : 28;
  return [4, 6, 9, 11].includes(month) ? 30 : 31;
}

export type IdentifierKind = 'cpf' | 'cnpj' | 'phone' | 'cep';

/**
 * The presentation formatting the components need, as one narrow port. Its implementation belongs
 * to `@loto/core` (formatting §2, §4, §6, §7 — Shared M3), which does not exist yet. Web provides
 * **no** fallback: until M4 wires core here, a component that needs formatting fails loudly instead
 * of guessing. Tests pass explicit doubles; the gallery passes labelled fixture tables.
 */
export interface PresentationFormat {
  /** Integer count, dot-grouped (§7): `1.234`. */
  readonly count: (value: number) => string;
  /** Integer cents as a digit string (`'1234'`, `'0'`) → amount without the unit (`12,34`). */
  readonly moneyDigits: (digits: string) => string;
  /** The same amount as speech for assistive technology (`doze reais e trinta e quatro centavos`). */
  readonly moneySpoken: (digits: string) => string;
  /** Pasted or typed text → integer-cents digit string, or null when it is not a BRL amount. */
  readonly moneyFromText: (text: string) => string | null;
  /** `YYYY-MM-DD` → `dd/MM/yyyy` (§4). */
  readonly date: (value: CalendarDate) => string;
  /** Typed `dd/MM/yyyy` → calendar date, or null when it is not a valid date (§4). */
  readonly dateFromInput: (text: string) => CalendarDate | null;
  /** Full day name for a calendar cell: `quinta-feira, 24 de setembro de 2026` (§4). */
  readonly dateLong: (value: CalendarDate) => string;
  /** `setembro de 2026` for a calendar heading (§4: month names lowercase). */
  readonly monthYear: (year: number, month: number) => string;
  /** `MM/yyyy` for the month field. */
  readonly month: (year: number, month: number) => string;
  /** Month name, lowercase (§4): 9 → `setembro`. */
  readonly monthName: (month: number) => string;
  /** Weekday header abbreviation, 0 = domingo (`dom.`). */
  readonly weekdayShort: (weekday: number) => string;
  /** Presentational mask (§6); a value that does not fit renders verbatim. */
  readonly identifier: (kind: IdentifierKind, value: string) => string;
  /** What is submitted where the contract expects digits only (§6). */
  readonly identifierDigits: (kind: IdentifierKind, value: string) => string;
}

const FormatContext = createContext<PresentationFormat | null>(null);

export function FormatProvider({
  format,
  children,
}: {
  readonly format: PresentationFormat;
  readonly children: ReactNode;
}) {
  return <FormatContext.Provider value={format}>{children}</FormatContext.Provider>;
}

/** The formatting port. Throws when none is provided: there is no Web-local formatter. */
export function useFormat(): PresentationFormat {
  const format = useContext(FormatContext);
  if (format === null) {
    throw new Error(
      'No PresentationFormat: wire @loto/core formatters (Shared M3) through FormatProvider.',
    );
  }
  return format;
}
