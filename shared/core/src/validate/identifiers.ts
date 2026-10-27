// Identity and contact masks (design/specs/formatting-ptbr.md §6). Masks are presentational: a
// value is "parseable" when, after removing the mask's own punctuation (`.` `-` `/` `(` `)`) and
// spaces, exactly the expected number of ASCII digits remains. Anything else renders VERBATIM —
// never truncated, padded or partially masked. These helpers add no required-field rule.

export type IdentifierKind = 'cpf' | 'cnpj' | 'phone' | 'cep';

const punctuation = /[.\-/()\s\u00A0\u202F]/gu;

/**
 * What is submitted where the contract expects digits (§6, CPF is stored as digits): the mask's
 * punctuation and spaces removed. Other characters are kept so validation can still reject them.
 */
export function identifierDigits(value: string): string {
  return value.replace(punctuation, '');
}

function digitsOf(value: string, ...counts: number[]): string | null {
  const digits = identifierDigits(value);
  return /^\d+$/.test(digits) && counts.includes(digits.length) ? digits : null;
}

/** `000.000.000-00` when exactly 11 digits are parseable; otherwise the value verbatim. */
export function formatCpf(value: string): string {
  const d = digitsOf(value, 11);
  return d === null ? value : `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

/** `00.000.000/0000-00` when exactly 14 digits are parseable; otherwise verbatim. */
export function formatCnpj(value: string): string {
  const d = digitsOf(value, 14);
  return d === null
    ? value
    : `${d.slice(0, 2)}.${d.slice(2, 5)}.${d.slice(5, 8)}/${d.slice(8, 12)}-${d.slice(12)}`;
}

/** 11 digits → `(00) 00000-0000`, 10 → `(00) 0000-0000`; anything else verbatim. */
export function formatPhone(value: string): string {
  const d = digitsOf(value, 10, 11);
  if (d === null) return value;
  const split = d.length === 11 ? 7 : 6;
  return `(${d.slice(0, 2)}) ${d.slice(2, split)}-${d.slice(split)}`;
}

/** `00000-000` when exactly 8 digits are parseable; otherwise verbatim. */
export function formatCep(value: string): string {
  const d = digitsOf(value, 8);
  return d === null ? value : `${d.slice(0, 5)}-${d.slice(5)}`;
}

/** The display mask of `kind`. */
export function formatIdentifier(kind: IdentifierKind, value: string): string {
  switch (kind) {
    case 'cpf':
      return formatCpf(value);
    case 'cnpj':
      return formatCnpj(value);
    case 'phone':
      return formatPhone(value);
    case 'cep':
      return formatCep(value);
  }
}

// Input masks (§6: "input mask auto-punctuates"): the punctuation appears as the digits arrive.
// Each mask is its digit groups and the separator written before each later group.
const inputMasks: Readonly<
  Record<Exclude<IdentifierKind, 'phone'>, readonly [number[], string[]]>
> = {
  cpf: [
    [3, 3, 3, 2],
    ['.', '.', '-'],
  ],
  cnpj: [
    [2, 3, 3, 4, 2],
    ['.', '.', '/', '-'],
  ],
  cep: [[5, 3], ['-']],
};

function grouped(digits: string, groups: readonly number[], separators: readonly string[]): string {
  let out = '';
  let at = 0;
  groups.forEach((size, index) => {
    if (at >= digits.length) return;
    if (index > 0) out += separators[index - 1] ?? '';
    out += digits.slice(at, at + size);
    at += size;
  });
  return out;
}

/**
 * The text an identifier field shows while it is typed: punctuation is added progressively as
 * digits arrive (`5299` → `529.9`, `52998224725` → `529.982.247-25`; a phone takes the landline
 * shape up to 10 digits and the mobile one at 11). A complete value equals `formatIdentifier`'s.
 * Text with anything but digits, mask punctuation and spaces, or more digits than the mask holds,
 * is returned verbatim — never truncated or forced into the mask. Blank stays blank.
 */
export function maskIdentifierInput(kind: IdentifierKind, text: string): string {
  const digits = identifierDigits(text);
  if (digits.length === 0) return /^[.\-/()\s\u00A0\u202F]*$/u.test(text) ? '' : text;
  if (!/^\d+$/.test(digits)) return text;
  if (kind === 'phone') {
    if (digits.length > 11) return text;
    const area = digits.slice(0, 2);
    const rest = digits.slice(2);
    if (rest.length === 0) return `(${area}`;
    const split = digits.length === 11 ? 5 : 4;
    const line = rest.length > split ? `${rest.slice(0, split)}-${rest.slice(split)}` : rest;
    return `(${area}) ${line}`;
  }
  const [groups, separators] = inputMasks[kind];
  const capacity = groups.reduce((sum, size) => sum + size, 0);
  return digits.length > capacity ? text : grouped(digits, groups, separators);
}

function checkDigit(digits: string, weights: readonly number[]): number {
  let sum = 0;
  weights.forEach((weight, index) => {
    sum += Number(digits[index]) * weight;
  });
  const rest = sum % 11;
  return rest < 2 ? 0 : 11 - rest;
}

const repeated = (digits: string) => /^(\d)\1*$/.test(digits);

/**
 * A CPF with valid check digits (punctuation allowed). Repeated-digit sequences such as
 * `111.111.111-11`, which satisfy the arithmetic, are rejected.
 */
export function isValidCpf(value: string): boolean {
  const d = digitsOf(value, 11);
  if (d === null || repeated(d)) return false;
  const first = checkDigit(d, [10, 9, 8, 7, 6, 5, 4, 3, 2]);
  const second = checkDigit(d, [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]);
  return first === Number(d[9]) && second === Number(d[10]);
}

/**
 * A numeric CNPJ with valid check digits (punctuation allowed); repeated digits rejected. The
 * alphanumeric CNPJ format is not covered: formatting §6 defines CNPJ as 14 digits.
 */
export function isValidCnpj(value: string): boolean {
  const d = digitsOf(value, 14);
  if (d === null || repeated(d)) return false;
  const first = checkDigit(d, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
  const second = checkDigit(d, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
  return first === Number(d[12]) && second === Number(d[13]);
}
