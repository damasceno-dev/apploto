// Exact decimal reading of a JS number (internal; not exported from the entry). A JSON number
// such as `12.34` is held by the engine as the nearest binary double, but its shortest round-trip
// text (`String(value)`, ECMAScript Number::toString) is exactly the decimal the server wrote for
// every value with at most 17 significant digits — numeric(14,2) needs at most 14. Reading that text
// instead of multiplying the double keeps conversions free of binary rounding (`0.175 × 60`).

/** A non-negative decimal as an integer scaled by 10^`scale`: `12.34` → `{ digits: 1234n, scale: 2 }`. */
export interface ExactDecimal {
  readonly negative: boolean;
  readonly digits: bigint;
  readonly scale: number;
}

const numberText = /^(-?)(\d+)(?:\.(\d+))?(?:e([+-]\d+))?$/;

/** The exact decimal of a finite number's shortest text; `-0` reads as positive zero. */
export function exactDecimalOf(value: number): ExactDecimal {
  if (!Number.isFinite(value)) throw new RangeError(`Not a finite number: ${String(value)}`);
  const match = numberText.exec(String(value));
  if (match === null) throw new RangeError(`Unreadable number: ${String(value)}`);
  const [, sign = '', whole = '0', fraction = '', exponentText = '0'] = match;
  const exponent = Number(exponentText);
  let digits = BigInt(whole + fraction);
  let scale = fraction.length - exponent;
  if (scale < 0) {
    digits *= BigInt(10) ** BigInt(-scale);
    scale = 0;
  }
  return { negative: sign === '-' && digits !== BigInt(0), digits, scale };
}

/** `digits / 10^scale` rounded to an integer, halves away from zero (sign handled by the caller). */
export function roundHalfAwayFromZero(digits: bigint, scale: number): bigint {
  if (scale <= 0) return digits * BigInt(10) ** BigInt(-scale);
  const divisor = BigInt(10) ** BigInt(scale);
  const quotient = digits / divisor;
  const remainder = digits % divisor;
  return remainder * BigInt(2) >= divisor ? quotient + BigInt(1) : quotient;
}

/** Dot every three integer digits (formatting §2, §7): `1234567` → `1.234.567`. */
export function groupThousands(integerDigits: string): string {
  let out = '';
  for (let index = 0; index < integerDigits.length; index += 1) {
    const fromEnd = integerDigits.length - index;
    if (index > 0 && fromEnd % 3 === 0) out += '.';
    out += integerDigits[index];
  }
  return out;
}

/** The display minus (formatting §3): U+2212, never the hyphen-minus. */
export const MINUS_SIGN = '\u2212';

/** The space between `R$` and the digits (formatting §2): U+00A0, so the symbol never wraps alone. */
export const NO_BREAK_SPACE = '\u00A0';
