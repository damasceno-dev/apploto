import {
  asCalendarDate,
  type CalendarDate,
  type PresentationFormat,
} from '@/components/ui/format-port';

// GALLERY FIXTURE — NOT A FORMATTER. `@loto/core` (Shared M3) owns pt-BR formatting and does not
// exist yet. These finite tables hold reference strings from design/specs/formatting-ptbr.md for
// the examples the gallery shows; anything outside them is shown UNFORMATTED (raw digits, ISO
// dates) so it can never pass for a working amount or date flow. Never import this outside
// `src/app/styleguide`.

const counts: Record<number, string> = { 1234: '1.234' };
const money: Record<string, readonly [display: string, spoken: string]> = {
  '0': ['0,00', 'zero reais'],
  '5': ['0,05', 'cinco centavos'],
  '1234': ['12,34', 'doze reais e trinta e quatro centavos'],
  '94310': ['943,10', 'novecentos e quarenta e três reais e dez centavos'],
  '243700': ['2.437,00', 'dois mil quatrocentos e trinta e sete reais'],
  '99999999999': ['999.999.999,99', 'novecentos e noventa e nove milhões… (exemplo)'],
};
const dates: Record<string, readonly [short: string, long: string]> = {
  '2026-09-23': ['23/09/2026', 'quarta-feira, 23 de setembro de 2026'],
  '2026-09-24': ['24/09/2026', 'quinta-feira, 24 de setembro de 2026'],
  '2026-09-25': ['25/09/2026', 'sexta-feira, 25 de setembro de 2026'],
  '2026-10-01': ['01/10/2026', 'quinta-feira, 1 de outubro de 2026'],
};
const months = [
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
];
const monthYears: Record<string, readonly [heading: string, field: string]> = {
  '2026-08': ['agosto de 2026', '08/2026'],
  '2026-09': ['setembro de 2026', '09/2026'],
  '2026-10': ['outubro de 2026', '10/2026'],
};
const monthKey = (year: number, month: number) => `${year}-${String(month).padStart(2, '0')}`;
const weekdays = ['dom.', 'seg.', 'ter.', 'qua.', 'qui.', 'sex.', 'sáb.'];
const identifiers: Record<string, string> = {
  '12345678909': '123.456.789-09',
  '12345678000195': '12.345.678/0001-95',
  '61912345678': '(61) 91234-5678',
  '70040010': '70040-010',
};

export const galleryFixtureFormat: PresentationFormat = {
  count: (value) => counts[value] ?? String(value),
  moneyDigits: (digits) => money[digits]?.[0] ?? digits,
  moneySpoken: (digits) => money[digits]?.[1] ?? `${digits} (centavos, sem formatador)`,
  moneyFromText: (text) =>
    text.trim() === 'R$ 1.234,56' || text.trim() === '1.234,56' ? '123456' : null,
  date: (value) => dates[value]?.[0] ?? value,
  dateFromInput: (text) =>
    (Object.entries(dates).find(([, [short]]) => short === text.trim())?.[0] as
      CalendarDate | undefined) ?? asCalendarDate(text.trim()),
  dateLong: (value) => dates[value]?.[1] ?? value,
  monthYear: (year, month) => monthYears[monthKey(year, month)]?.[0] ?? monthKey(year, month),
  month: (year, month) => monthYears[monthKey(year, month)]?.[1] ?? monthKey(year, month),
  monthName: (month) => months[month - 1] ?? String(month),
  weekdayShort: (weekday) => weekdays[weekday] ?? String(weekday),
  identifier: (_kind, value) => identifiers[value] ?? value,
  identifierDigits: (_kind, value) => value.replace(/\D/g, ''),
};
