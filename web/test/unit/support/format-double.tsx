import type { ReactNode } from 'react';
import {
  asCalendarDate,
  FormatProvider,
  type CalendarDate,
  type PresentationFormat,
} from '@/components/ui/format-port';

/**
 * An explicit TEST DOUBLE of the `@loto/core` presentation port: every output is bracketed so a
 * test proves the component rendered the port's answer rather than formatting anything itself. It
 * is not a formatter and never ships.
 */
export const formatDouble: PresentationFormat = {
  count: (value) => `#${value}`,
  moneyDigits: (digits) => `[${digits}]`,
  moneySpoken: (digits) => `falado:${digits}`,
  moneyFromText: (text) => (/^R\$ ?[\d.]+,\d{2}$/.test(text) ? text.replace(/\D/g, '') : null),
  date: (value) => `d:${value}`,
  dateFromInput: (text) => {
    const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(text);
    return match ? asCalendarDate(`${match[3]}-${match[2]}-${match[1]}`) : null;
  },
  dateLong: (value: CalendarDate) => `longo:${value}`,
  monthYear: (year, month) => `mês:${month}/${year}`,
  month: (year, month) => `${String(month).padStart(2, '0')}/${year}`,
  monthName: (month) => `m${month}`,
  weekdayShort: (weekday) => `s${weekday}`,
  identifier: (kind, value) => `${kind}<${value}>`,
  identifierInput: (kind, value) => `${kind}<${value}>`,
  identifierDigits: (_kind, value) => value.replace(/\D/g, ''),
};

export function WithFormat({ children }: { readonly children: ReactNode }) {
  return <FormatProvider format={formatDouble}>{children}</FormatProvider>;
}

/** jsdom has no `matchMedia`: the components' compact queries answer `compact`. */
export function installMatchMedia(compact = false) {
  window.matchMedia = ((query: string) => ({
    matches: query.includes('40em') ? compact : false,
    media: query,
    onchange: null,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
    dispatchEvent: () => false,
  })) as typeof window.matchMedia;
}
