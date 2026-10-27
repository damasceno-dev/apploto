// BRL money (design/specs/formatting-ptbr.md §2–§3). Money is display-only by default: the server
// computes every financial outcome. These helpers convert between the two explicit representations
// — integer cents (`Cents`) and the JSON wire number mirroring numeric(14,2) — format them, and
// parse typed or pasted input. An untagged number is never guessed to be one or the other.
import { exactDecimalOf, groupThousands, MINUS_SIGN, NO_BREAK_SPACE } from './decimal';

/** Integer cents. Only `asCents`, `centsFromWire` and the input parsers produce it. */
export type Cents = number & { readonly __cents: true };

/** numeric(14,2): 12 integer digits and 2 decimals, `999.999.999.999,99` either sign. */
export const MAX_CENTS = 99_999_999_999_999;

/** Integer cents within numeric(14,2); throws `RangeError` otherwise. `-0` becomes `0`. */
export function asCents(value: number): Cents {
  if (!Number.isSafeInteger(value) || Math.abs(value) > MAX_CENTS) {
    throw new RangeError(`Not integer cents within numeric(14,2): ${String(value)}`);
  }
  return (value === 0 ? 0 : value) as Cents;
}

/** Whether a wire number is a numeric(14,2) value: finite, at most 2 decimals, within range. */
export function isWireMoney(value: number): boolean {
  if (!Number.isFinite(value)) return false;
  const exact = exactDecimalOf(value);
  if (exact.scale > 2) return false;
  const cents = exact.digits * BigInt(10) ** BigInt(2 - exact.scale);
  return cents <= BigInt(MAX_CENTS);
}

/**
 * The exact cents of a wire amount (`12.34` → 1234). A value with more than two decimals, out of
 * range or not finite is a contract violation and throws — it is never rounded into a figure.
 */
export function centsFromWire(value: number): Cents {
  if (!isWireMoney(value))
    throw new RangeError(`Not a numeric(14,2) wire amount: ${String(value)}`);
  const exact = exactDecimalOf(value);
  const cents = Number(exact.digits * BigInt(10) ** BigInt(2 - exact.scale));
  return asCents(exact.negative ? -cents : cents);
}

/**
 * The JSON number for integer cents (1234 → `12.34`). Division by 100 is correctly rounded, so the
 * result is the double nearest the decimal and serializes with at most two decimals.
 */
export function wireFromCents(cents: Cents): number {
  const value = asCents(cents) / 100;
  return value === 0 ? 0 : value;
}

function amountText(magnitude: number): string {
  const digits = String(magnitude).padStart(3, '0');
  return `${groupThousands(digits.slice(0, -2))},${digits.slice(-2)}`;
}

/** The amount without the unit (`123456` → `1.234,56`); unsigned. MoneyInput shows this beside `R$`. */
export function formatBrlAmount(cents: Cents): string {
  const value = asCents(cents);
  if (value < 0) throw new RangeError('formatBrlAmount is unsigned; use formatSignedBrl.');
  return amountText(value);
}

/**
 * Unsigned display (§2): `R$ 1.234,56`, U+00A0 after the symbol. A negative value throws instead of
 * hiding its sign: signed figures use `formatSignedBrl` and fiado balances `presentFiadoBalance`.
 */
export function formatBrl(cents: Cents): string {
  return `R$${NO_BREAK_SPACE}${formatBrlAmount(cents)}`;
}

/** `formatBrl` for a wire number (`Transaction.Value`). */
export function formatBrlWire(value: number): string {
  return formatBrl(centsFromWire(value));
}

/**
 * Signed display (§3): `+R$ 12,00`, `−R$ 12,00` (U+2212), zero unsigned `R$ 0,00`. For values
 * signed by nature: cash variance, impact deltas.
 */
export function formatSignedBrl(cents: Cents): string {
  const value = asCents(cents);
  const unsigned = `R$${NO_BREAK_SPACE}${amountText(Math.abs(value))}`;
  if (value === 0) return unsigned;
  return `${value > 0 ? '+' : MINUS_SIGN}${unsigned}`;
}

/** `formatSignedBrl` for a wire number (`VarianceValue`, `VarianceDelta`, …). */
export function formatSignedBrlWire(value: number): string {
  return formatSignedBrl(centsFromWire(value));
}

export type ParseBrlError = 'empty' | 'malformed' | 'signed' | 'out-of-range';

export type ParseBrlResult =
  | { readonly ok: true; readonly cents: Cents }
  | { readonly ok: false; readonly error: ParseBrlError };

// pt-BR numerals (§1): the comma is the only decimal separator; a dot only groups thousands, so
// every group after the first has exactly three digits. `1.234` is 1234 and `1.5` is malformed —
// never read as one and a half. The same grammar backs `evaluateMoneyExpression`.
const ungrouped = /^(\d+)(?:,(\d{1,2}))?$/;
const grouped = /^(\d{1,3}(?:\.\d{3})+)(?:,(\d{1,2}))?$/;
const space = /[\s\u00A0\u202F]/u;

/** The cents of a pt-BR numeral (`1.234,5` → 123450), or null when it is not one. */
export function centsOfNumeral(text: string): bigint | null {
  const match = ungrouped.exec(text) ?? grouped.exec(text);
  if (match === null) return null;
  const whole = (match[1] ?? '').replace(/\./g, '');
  const fraction = (match[2] ?? '').padEnd(2, '0');
  return BigInt(whole) * BigInt(100) + BigInt(fraction);
}

/**
 * Pasted or typed text → integer cents (§2 input). Tolerated: an optional `R$` prefix, spaces
 * (including U+00A0 / U+202F) around it and at the ends, dot grouping, 1–2 decimals after a comma.
 * Rejected by category: blank (`empty`), a `+`/`-`/`−` sign (`signed` — MoneyInput is unsigned),
 * more than 12 integer digits (`out-of-range`), anything else (`malformed`).
 */
export function parseBrl(text: string): ParseBrlResult {
  let rest = text.trim();
  if (rest.length === 0) return { ok: false, error: 'empty' };
  if (/^[+\-\u2212]/u.test(rest) || /^R\$[\s\u00A0\u202F]*[+\-\u2212]/u.test(rest)) {
    return { ok: false, error: 'signed' };
  }
  if (rest.startsWith('R$')) rest = rest.slice(2).replace(/^[\s\u00A0\u202F]+/u, '');
  if (rest.length === 0 || space.test(rest)) return { ok: false, error: 'malformed' };
  const cents = centsOfNumeral(rest);
  if (cents === null) return { ok: false, error: 'malformed' };
  if (cents > BigInt(MAX_CENTS)) return { ok: false, error: 'out-of-range' };
  return { ok: true, cents: asCents(Number(cents)) };
}

// The MoneyInput entry model (§2 input): the exact typed state is a digit string of integer cents,
// built right to left (`1234` is R$ 12,34). Blank is the screen's absence of a value; `'0'` is a
// recorded zero. These helpers never do arithmetic on the amount.

/** 12 integer digits + 2 decimals. A keystroke that would exceed it is refused, never truncated. */
export const MAX_MONEY_DIGITS = 14;

/** Leading zeros dropped (`'005'` → `'5'`, `'000'` → `'0'`); null if not digits or over the cap. */
export function normalizeMoneyDigits(digits: string): string | null {
  if (!/^\d+$/.test(digits)) return null;
  const trimmed = digits.replace(/^0+/, '');
  const normalized = trimmed.length === 0 ? '0' : trimmed;
  return normalized.length > MAX_MONEY_DIGITS ? null : normalized;
}

/** Integer cents of an entry digit string; throws `RangeError` when it is not one. */
export function centsFromMoneyDigits(digits: string): Cents {
  const normalized = normalizeMoneyDigits(digits);
  if (normalized === null) throw new RangeError(`Not a money entry: ${digits}`);
  return asCents(Number(normalized));
}

/** The entry digit string of non-negative cents (1234 → `'1234'`, 0 → `'0'`). */
export function moneyDigitsFromCents(cents: Cents): string {
  const value = asCents(cents);
  if (value < 0) throw new RangeError('A money entry is unsigned.');
  return String(value);
}

/** An entry digit string as the field shows it, without the unit: `'1234'` → `12,34`. */
export function formatMoneyDigits(digits: string): string {
  return formatBrlAmount(centsFromMoneyDigits(digits));
}

export { MINUS_SIGN, NO_BREAK_SPACE };
