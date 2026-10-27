'use client';

import { useSyncExternalStore } from 'react';
import { formatInstant, formatInstantDate, formatInstantTime } from '@loto/core';

// UTC audit instants (formatting §4: `CreatedAt`, `UpdatedAt`, `SubmittedAt`, …) are shown in the
// DEVICE time zone. The server cannot know it, so the server render and the hydrating render show
// the same empty `<time>` and the text appears once the browser's zone is read — never a guessed
// zone and never a hydration mismatch. A caller that knows the zone (tests, an agreed zone) passes
// `timeZone` and the text is identical on the server and in the browser.

const noSubscription = () => () => undefined;

/** The browser's IANA zone after hydration; null during the server render and hydration. */
export function useDeviceTimeZone(): string | null {
  return useSyncExternalStore(
    noSubscription,
    () => Intl.DateTimeFormat().resolvedOptions().timeZone,
    () => null,
  );
}

const formatters = { datetime: formatInstant, date: formatInstantDate, time: formatInstantTime };

export function AuditInstant({
  value,
  show = 'datetime',
  timeZone,
  className,
}: {
  /** The API's ISO instant with `Z` or an offset. */
  readonly value: string;
  readonly show?: keyof typeof formatters;
  /** An explicit zone; otherwise the device's, read after hydration. */
  readonly timeZone?: string;
  readonly className?: string;
}) {
  const device = useDeviceTimeZone();
  const zone = timeZone ?? device;
  return (
    <time dateTime={value} className={className} data-time-zone={zone ?? undefined}>
      {zone === null ? '' : formatters[show](value, zone)}
    </time>
  );
}
