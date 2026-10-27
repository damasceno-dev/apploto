// Plain numbers, counts and percentages (design/specs/formatting-ptbr.md §7) and absent values
// (§8). Hand-rolled from the number's exact decimal text: comma decimals, dot grouping, U+2212 for
// a negative. Absent (`null`/`undefined`) is the em dash — never `0`, which is a meaningful value.
import { exactDecimalOf, groupThousands, MINUS_SIGN, roundHalfAwayFromZero } from './decimal';

/** The em dash shown for an absent value (§8). */
export const EM_DASH = '\u2014';

/** Integer count, dot-grouped: `3`, `42`, `1.234`. Throws for a non-integer. */
export function formatCount(value: number): string {
  if (!Number.isSafeInteger(value)) throw new RangeError(`Not an integer count: ${String(value)}`);
  const digits = groupThousands(String(Math.abs(value)));
  return value < 0 ? `${MINUS_SIGN}${digits}` : digits;
}

/**
 * A decimal with a comma separator (`1,5`, `1.234,5`), exactly as many decimals as the value has,
 * or rounded to `maxFractionDigits` halves away from zero when given (trailing zeros dropped).
 */
export function formatDecimal(value: number, maxFractionDigits?: number): string {
  const exact = exactDecimalOf(value);
  let { digits, scale } = exact;
  if (maxFractionDigits !== undefined) {
    if (!Number.isInteger(maxFractionDigits) || maxFractionDigits < 0 || maxFractionDigits > 20) {
      throw new RangeError(`Not a fraction digit count: ${maxFractionDigits}`);
    }
    if (scale > maxFractionDigits) {
      digits = roundHalfAwayFromZero(digits, scale - maxFractionDigits);
      scale = maxFractionDigits;
    }
  }
  let text = digits.toString().padStart(scale + 1, '0');
  let fraction = scale > 0 ? text.slice(-scale) : '';
  text = scale > 0 ? text.slice(0, -scale) : text;
  fraction = fraction.replace(/0+$/, '');
  const body = `${groupThousands(text)}${fraction === '' ? '' : `,${fraction}`}`;
  return exact.negative && /[1-9]/.test(body) ? `${MINUS_SIGN}${body}` : body;
}

/** A percentage given in percent units (12.5 → `12,5%`), comma decimal, no space before `%`. */
export function formatPercent(percent: number, maxFractionDigits?: number): string {
  return `${formatDecimal(percent, maxFractionDigits)}%`;
}

/** Installment position: `3/12` (position/total, no spaces). */
export function formatInstallment(position: number, total: number): string {
  if (
    !Number.isSafeInteger(position) ||
    !Number.isSafeInteger(total) ||
    position < 1 ||
    position > total
  ) {
    throw new RangeError(`Not an installment position: ${position}/${total}`);
  }
  return `${position}/${total}`;
}

/** The value through `format`, or the em dash when absent. Zero is formatted, never dashed. */
export function formatOptional<T>(
  value: T | null | undefined,
  format: (value: T) => string,
): string {
  return value === null || value === undefined ? EM_DASH : format(value);
}
