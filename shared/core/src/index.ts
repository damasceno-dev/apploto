// `@loto/core` entry (Shared M3): pure TS helpers that implement design/specs/formatting-ptbr.md
// and copy-guidelines.md §5 for web and mobile. No React, DOM, Node, environment, storage, clock or
// device-locale access; importing has no side effects. Only what is listed here is public.

export {
  asCents,
  centsFromMoneyDigits,
  centsFromWire,
  formatBrl,
  formatBrlAmount,
  formatBrlWire,
  formatMoneyDigits,
  formatSignedBrl,
  formatSignedBrlWire,
  isWireMoney,
  MAX_CENTS,
  MAX_MONEY_DIGITS,
  MINUS_SIGN,
  moneyDigitsFromCents,
  NO_BREAK_SPACE,
  normalizeMoneyDigits,
  parseBrl,
  wireFromCents,
  type Cents,
  type ParseBrlError,
  type ParseBrlResult,
} from './format/brl';
export { spellBrl, spellSignedBrl } from './format/spoken-brl';
export {
  presentFiadoBalance,
  presentFiadoBalanceWire,
  type FiadoBalanceKind,
  type FiadoBalancePresentation,
} from './format/fiado';
export {
  evaluateMoneyExpression,
  MAX_EXPRESSION_DEPTH,
  MAX_EXPRESSION_LENGTH,
  MAX_EXPRESSION_TOKENS,
  type MoneyExpressionError,
  type MoneyExpressionOptions,
  type MoneyExpressionResult,
} from './format/money-expression';
export { formatDuration, formatSignedDuration } from './format/duration';
export {
  EM_DASH,
  formatCount,
  formatDecimal,
  formatInstallment,
  formatOptional,
  formatPercent,
} from './format/number';
export {
  addDays,
  addMonths,
  calendarDateFromParts,
  calendarDateParts,
  compareCalendarDates,
  daysBetween,
  daysInMonth,
  isLeapYear,
  MAX_CALENDAR_YEAR,
  MIN_CALENDAR_YEAR,
  parseCalendarDate,
  weekdayOf,
  type CalendarDate,
  type CalendarParts,
} from './dates/calendar';
export {
  formatDate,
  formatDateWithWeekday,
  formatLongDate,
  formatMonthField,
  formatMonthYear,
  formatWallDateTime,
  formatWallTime,
  monthName,
  parseDateInput,
  parseWallTime,
  weekdayName,
  weekdayShortName,
  type WallTime,
} from './dates/display';
export { formatInstant, formatInstantDate, formatInstantTime, parseInstant } from './dates/instant';
export {
  formatCep,
  formatCnpj,
  formatCpf,
  formatIdentifier,
  formatPhone,
  identifierDigits,
  isValidCnpj,
  isValidCpf,
  maskIdentifierInput,
  type IdentifierKind,
} from './validate/identifiers';
export * from './labels/enums';
