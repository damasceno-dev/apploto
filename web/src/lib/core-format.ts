// The app's PresentationFormat (Web M3 7.4): the components' formatting port implemented by
// `@loto/core` (formatting §2, §4, §6, §7). Pure and isomorphic — the same answers in the server
// render and in the browser — with no device clock, zone or locale. This is the only
// implementation of the port that ships; there is no Web-local formatter.
import {
  centsFromMoneyDigits,
  formatCount,
  formatDate,
  formatDateWithWeekday,
  formatIdentifier,
  formatMoneyDigits,
  formatMonthField,
  formatMonthYear,
  identifierDigits,
  maskIdentifierInput,
  moneyDigitsFromCents,
  monthName,
  parseBrl,
  parseDateInput,
  spellBrl,
  weekdayShortName,
} from '@loto/core';
import type { PresentationFormat } from '@/components/ui/format-port';

export const coreFormat: PresentationFormat = Object.freeze<PresentationFormat>({
  count: formatCount,
  moneyDigits: formatMoneyDigits,
  moneySpoken: (digits: string) => spellBrl(centsFromMoneyDigits(digits)),
  // A signed, malformed or out-of-range paste is not an amount for an unsigned field: null.
  moneyFromText: (text: string) => {
    const parsed = parseBrl(text);
    return parsed.ok ? moneyDigitsFromCents(parsed.cents) : null;
  },
  date: formatDate,
  dateFromInput: parseDateInput,
  dateLong: formatDateWithWeekday,
  monthYear: formatMonthYear,
  month: formatMonthField,
  monthName,
  weekdayShort: weekdayShortName,
  identifier: formatIdentifier,
  identifierInput: maskIdentifierInput,
  identifierDigits: (_kind, value: string) => identifierDigits(value),
});
