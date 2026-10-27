'use client';

import { createContext, useContext, type ReactNode } from 'react';
import { parseCalendarDate, type CalendarDate, type IdentifierKind } from '@loto/core';

// Calendar dates and identifier kinds are `@loto/core`'s own types (one calendar validation for
// web and mobile): `YYYY-MM-DD` strings, never a JS `Date`.
export type { CalendarDate, IdentifierKind } from '@loto/core';

/** Accepts only a real calendar date string (no time, no zone); never constructs a `Date`. */
export const asCalendarDate: (value: string) => CalendarDate | null = parseCalendarDate;

export { daysInMonth } from '@loto/core';

/**
 * The presentation formatting the components need, as one narrow port (formatting §2, §4, §6, §7).
 * The app's implementation is `coreFormat` (`@/lib/core-format`, over `@loto/core`), provided at the
 * root by `AppProviders`. There is no Web-local formatter or fallback: without a provider a
 * component fails loudly instead of guessing. Unit tests pass an explicit double to prove a
 * component renders the port's answer.
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
  /**
   * The text while typing (§6 "input mask auto-punctuates"): punctuation added as digits arrive
   * (`5299` → `529.9`); unsupported or overlong text verbatim.
   */
  readonly identifierInput: (kind: IdentifierKind, text: string) => string;
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
      'No PresentationFormat: render inside FormatProvider (AppProviders provides coreFormat).',
    );
  }
  return format;
}
