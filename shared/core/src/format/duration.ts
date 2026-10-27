// Durations (design/specs/formatting-ptbr.md §5). Time-entry endpoints send DECIMAL hours
// (`TotalHours`, `BalanceHours`, `DailyTargetHours = 7.33` means 7h 20min, not 7h 33min) and
// totals can exceed 24h, so these are durations, never clock times. The single conversion is
// `minutes = round(|hours| × 60)`, to the nearest minute with halves away from zero, sign reapplied
// — computed on the number's exact decimal text so binary rounding cannot move a half-minute.
// This formats an API figure; it applies no target, time bank or policy rule.
import { exactDecimalOf, MINUS_SIGN, roundHalfAwayFromZero } from './decimal';

function wholeMinutes(hours: number): { readonly negative: boolean; readonly minutes: number } {
  const exact = exactDecimalOf(hours);
  const minutes = Number(roundHalfAwayFromZero(exact.digits * BigInt(60), exact.scale));
  if (!Number.isSafeInteger(minutes))
    throw new RangeError(`Duration out of range: ${String(hours)}`);
  return { negative: exact.negative && minutes !== 0, minutes };
}

function render(minutes: number): string {
  return `${Math.floor(minutes / 60)}h ${String(minutes % 60).padStart(2, '0')}min`;
}

/** Unsigned duration: `8h 30min`, `173h 05min`, `0h 45min`. A negative value throws (use `formatSignedDuration`). */
export function formatDuration(hours: number): string {
  const { negative, minutes } = wholeMinutes(hours);
  if (negative) throw new RangeError('formatDuration is unsigned; use formatSignedDuration.');
  return render(minutes);
}

/** Signed balance (banco de horas): `+2h 15min`, `−2h 15min` (U+2212), zero unsigned `0h 00min`. */
export function formatSignedDuration(hours: number): string {
  const { negative, minutes } = wholeMinutes(hours);
  if (minutes === 0) return render(0);
  return `${negative ? MINUS_SIGN : '+'}${render(minutes)}`;
}
