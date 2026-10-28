// UTC audit instants (design/specs/formatting-ptbr.md §4: `CreatedAt`, `SubmittedAt`, `PaidAt`, …)
// are converted to the DEVICE time zone, then formatted. Shared never reads the device: the caller
// passes the IANA zone it resolved (`Intl.DateTimeFormat().resolvedOptions().timeZone` on the
// client, or an explicitly agreed zone so a server render matches). The zone rules come from the
// engine's `Intl.DateTimeFormat` — the one engine-dependent formatter in this package. Its output
// uses only a numeric IANA offset; proleptic-Gregorian UTC getters assemble the date.
// Strategy independently accepted on Node/Web and both Android/iOS Hermes (Shared M3).
// No device clock, locale or zone is read here.

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

// Use Intl only for the IANA offset, not its calendar fields. Android Hermes uses a
// Julian/Gregorian cutover even when calendar: 'iso8601' is requested (native M1 probe).
// ECMAScript Date UTC getters use the required proleptic Gregorian calendar. Include offset
// seconds: historical IANA offsets such as New York's -04:56:02 are not whole minutes.
const formatters = new Map<string, Intl.DateTimeFormat>();

function zonedParts(value: string, timeZone: string): ZonedParts {
  const epoch = parseInstant(value);
  if (epoch === null) throw new RangeError(`Not a UTC instant with an offset: ${value}`);
  let formatter = formatters.get(timeZone);
  if (formatter === undefined) {
    formatter = new Intl.DateTimeFormat('en-US', {
      timeZone,
      timeZoneName: 'longOffset',
      hour: '2-digit',
      hourCycle: 'h23',
      numberingSystem: 'latn',
    });
    formatters.set(timeZone, formatter);
  }
  const parts = formatter.formatToParts(new Date(epoch));
  const zoneIndex = parts.findIndex((part) => part.type === 'timeZoneName');
  // iOS Hermes tags offset digits as hour/minute/literal, leaving only `GMT` in
  // timeZoneName. Reassemble its signed suffix. Android can instead place a complete
  // zone name BEFORE the clock; never append those clock parts to a complete offset.
  let offset = parts[zoneIndex]?.value;
  if (offset === 'GMT' || offset === 'UTC') {
    const suffix = parts
      .slice(zoneIndex + 1)
      .map((part) => part.value)
      .join('')
      .trim();
    if (/^[+-]/.test(suffix)) offset += suffix;
  }
  let seconds = 0;
  if (offset !== 'GMT' && offset !== 'UTC') {
    const match = /^(?:GMT|UTC)([+-])(\d{2}):(\d{2})(?::(\d{2}))?$/.exec(offset ?? '');
    if (!match || Number(match[2]) > 23 || Number(match[3]) > 59 || Number(match[4] ?? 0) > 59) {
      throw new RangeError(
        `The engine returned no numeric zone offset for ${value} in ${timeZone}`,
      );
    }
    seconds =
      (Number(match[2]) * 3600 + Number(match[3]) * 60 + Number(match[4] ?? 0)) *
      (match[1] === '-' ? -1 : 1);
  }
  const local = new Date(epoch + seconds * 1000);
  const year = local.getUTCFullYear();
  if (year < 1 || year > 9999) {
    throw new RangeError(`${value} in ${timeZone} falls outside the years 0001–9999`);
  }
  const pad = (part: number, width = 2) => String(part).padStart(width, '0');
  return {
    year: pad(year, 4),
    month: pad(local.getUTCMonth() + 1),
    day: pad(local.getUTCDate()),
    hour: pad(local.getUTCHours()),
    minute: pad(local.getUTCMinutes()),
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
