// UTC audit instants (design/specs/formatting-ptbr.md §4: `CreatedAt`, `SubmittedAt`, `PaidAt`, …)
// are converted to the DEVICE time zone, then formatted. Shared never reads the device: the caller
// passes the IANA zone it resolved (`Intl.DateTimeFormat().resolvedOptions().timeZone` on the
// client, or an explicitly agreed zone so a server render matches). The zone rules come from the
// engine's `Intl.DateTimeFormat` — the one engine-dependent formatter in this package. Its output
// is assembled from numeric parts only (no locale strings). Strategy: web-verified candidate,
// provisional for native until Mobile M1 measures Hermes.

const instantPattern =
  /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,9}))?)?(Z|[+-]\d{2}:\d{2})$/;

/**
 * Epoch milliseconds of an ISO 8601 instant that carries `Z` or an offset. An offset-free value is
 * a branch wall time, not an instant, and returns null instead of being guessed into a zone.
 */
export function parseInstant(value: string): number | null {
  const match = instantPattern.exec(value);
  if (match === null) return null;
  const [year, month, day, hour, minute, second] = [1, 2, 3, 4, 5, 6].map((index) =>
    Number(match[index] ?? '0'),
  ) as [number, number, number, number, number, number];
  const fraction = (match[7] ?? '').padEnd(3, '0').slice(0, 3);
  const zone = match[8] as string;
  const leap = (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
  const monthDays = month === 2 ? (leap ? 29 : 28) : [4, 6, 9, 11].includes(month) ? 30 : 31;
  if (year < 1 || month < 1 || month > 12 || day < 1 || day > monthDays) return null;
  if (hour > 23 || minute > 59 || second > 59) return null;
  let offsetMinutes = 0;
  if (zone !== 'Z') {
    const [hours, minutes] = [Number(zone.slice(1, 3)), Number(zone.slice(4, 6))];
    if (hours > 23 || minutes > 59) return null;
    offsetMinutes = (zone.startsWith('-') ? -1 : 1) * (hours * 60 + minutes);
  }
  // Date.UTC treats years 0–99 as 1900–1999, so set the full year explicitly.
  const utc = new Date(0);
  utc.setUTCFullYear(year, month - 1, day);
  utc.setUTCHours(hour, minute, second, Number(fraction));
  return utc.getTime() - offsetMinutes * 60_000;
}

interface ZonedParts {
  readonly year: string;
  readonly month: string;
  readonly day: string;
  readonly hour: string;
  readonly minute: string;
}

// A formatter per zone; construction validates the zone (an unknown zone throws RangeError).
const formatters = new Map<string, Intl.DateTimeFormat>();

function zonedParts(value: string, timeZone: string): ZonedParts {
  const epoch = parseInstant(value);
  if (epoch === null) throw new RangeError(`Not a UTC instant with an offset: ${value}`);
  let formatter = formatters.get(timeZone);
  if (formatter === undefined) {
    formatter = new Intl.DateTimeFormat('en-US', {
      timeZone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
      // `year` is the year of the era: without the era, 1 BC would read as year 0001.
      era: 'short',
    });
    formatters.set(timeZone, formatter);
  }
  const parts: Record<string, string> = {};
  for (const part of formatter.formatToParts(new Date(epoch))) parts[part.type] = part.value;
  const { era, year, month, day, hour, minute } = parts;
  if (
    era === undefined ||
    year === undefined ||
    month === undefined ||
    day === undefined ||
    hour === undefined ||
    minute === undefined
  ) {
    throw new RangeError(`The engine returned no date parts for ${value} in ${timeZone}`);
  }
  // The converted local date must stay in the supported calendar (0001–9999 AD), like every
  // other date this package formats: a conversion across either end is rejected, not mislabelled.
  const absoluteYear = Number(year);
  if (era !== 'AD' || !Number.isInteger(absoluteYear) || absoluteYear < 1 || absoluteYear > 9999) {
    throw new RangeError(`${value} in ${timeZone} falls outside the years 0001–9999`);
  }
  return {
    year: year.padStart(4, '0'),
    month: month.padStart(2, '0'),
    day: day.padStart(2, '0'),
    // Some engines print midnight as 24 even with h23; normalize it.
    hour: hour === '24' ? '00' : hour.padStart(2, '0'),
    minute: minute.padStart(2, '0'),
  };
}

/** A UTC instant in the given IANA zone as `dd/MM/yyyy HH:mm` (seconds dropped, never rounded). */
export function formatInstant(value: string, timeZone: string): string {
  const { year, month, day, hour, minute } = zonedParts(value, timeZone);
  return `${day}/${month}/${year} ${hour}:${minute}`;
}

/** The instant's date in the given zone: `dd/MM/yyyy`. */
export function formatInstantDate(value: string, timeZone: string): string {
  const { year, month, day } = zonedParts(value, timeZone);
  return `${day}/${month}/${year}`;
}

/** The instant's time in the given zone: `HH:mm`. */
export function formatInstantTime(value: string, timeZone: string): string {
  const { hour, minute } = zonedParts(value, timeZone);
  return `${hour}:${minute}`;
}
