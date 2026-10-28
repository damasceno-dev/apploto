// Pure editing logic behind the native inputs: progressive identifier masks with caret
// preservation (formatting §6) and cents-entry money digits (formatting §2). Formatting itself is
// always `@loto/core`; this module only decides which text the user meant to produce.
import {
  formatMoneyDigits,
  identifierDigits,
  maskIdentifierInput,
  normalizeMoneyDigits,
  parseBrl,
  moneyDigitsFromCents,
  type IdentifierKind,
} from '@loto/core';

export interface Selection {
  readonly start: number;
  readonly end: number;
}

const separators: Readonly<Record<IdentifierKind, string>> = {
  cpf: '.-',
  cnpj: './-',
  cep: '-',
  phone: '() -',
};

const isDigit = (char: string | undefined) => char !== undefined && char >= '0' && char <= '9';
// Characters a money field can hold: digits, core's separators, (no-break) spaces and `R$`.
const moneyChars = /^[\d.,\s\u00a0R$]*$/;
const digitsIn = (text: string) => [...text].filter(isDigit).join('');

/** The span a native change replaced: what `before` lost there and what `after` put in. */
function changedSpan(before: string, after: string) {
  let start = 0;
  while (start < before.length && start < after.length && after[start] === before[start])
    start += 1;
  let end = 0;
  while (
    end < before.length - start &&
    end < after.length - start &&
    after[after.length - 1 - end] === before[before.length - 1 - end]
  )
    end += 1;
  return {
    removed: before.slice(start, before.length - end),
    inserted: after.slice(start, after.length - end),
  };
}

// A formatted amount (`1.234,56`) with any number of digits typed at the end, or cut short before
// its decimals. With the caret pinned to the end, every keystroke result has this shape.
const typedShape = /^\d{1,3}(?:\.\d{3})*(?:,\d*)?$/;

/**
 * Whether `raw` is `displayed` edited by typing or deleting at the end, where the caret stays:
 * digits only, a formatted amount with digits typed after it, or a shortened `displayed`; and its
 * changed span adds digits plus at most the separators it removed (re-formatting moves them). A
 * replacement that brings its own separators (`1.5`, `1,2,3`), spaces or letters is not an edit
 * and must parse as a whole amount.
 */
function editsDisplayed(displayed: string, raw: string): boolean {
  if (!/^\d*$/.test(raw) && !typedShape.test(raw) && !displayed.startsWith(raw)) return false;
  const { removed, inserted } = changedSpan(displayed, raw);
  const available = [...removed].filter((char) => !isDigit(char));
  for (const char of inserted) {
    if (isDigit(char)) continue;
    const index = available.indexOf(char);
    if (index < 0) return false;
    available.splice(index, 1);
  }
  return true;
}

/** Index just after the `count`-th digit of `text` (0 → the start). */
export function caretAfterDigits(text: string, count: number): number {
  if (count <= 0) return 0;
  let seen = 0;
  for (let index = 0; index < text.length; index += 1) {
    if (isDigit(text[index])) seen += 1;
    if (seen === count) return index + 1;
  }
  return text.length;
}

/**
 * The masked text and caret after a native change from `previous` to `raw`. When exactly one
 * generated separator of a well-formed mask was deleted, the neighbouring digit is deleted instead
 * (otherwise the mask would put the separator straight back). Removing an invalid character or
 * editing passthrough text (letters, overlong values, alphanumeric CNPJ) keeps every digit as typed.
 */
export function editIdentifier(
  kind: IdentifierKind,
  previous: string,
  raw: string,
  previousSelection: Selection,
): { readonly text: string; readonly caret: number } {
  let text = raw;
  let at = Math.min(raw.length, Math.max(0, previousSelection.start));
  if (raw.length === previous.length - 1 && previousSelection.start === previousSelection.end) {
    const p = previousSelection.start;
    const backward = p > 0 && raw === previous.slice(0, p - 1) + previous.slice(p);
    const forward = !backward && raw === previous.slice(0, p) + previous.slice(p + 1);
    let removed = -1;
    if (backward) removed = p - 1;
    else if (forward) removed = p;
    else {
      removed = 0;
      while (removed < raw.length && raw[removed] === previous[removed]) removed += 1;
    }
    at = backward ? p - 1 : removed;
    const removedChar = previous[removed] ?? '';
    if (
      separators[kind].includes(removedChar) &&
      maskIdentifierInput(kind, digitsIn(previous)) === previous
    ) {
      let index = -1;
      if (forward) {
        for (let i = removed; i < raw.length; i += 1)
          if (isDigit(raw[i])) {
            index = i;
            break;
          }
      } else {
        for (let i = removed - 1; i >= 0; i -= 1)
          if (isDigit(raw[i])) {
            index = i;
            break;
          }
      }
      if (index >= 0) {
        text = raw.slice(0, index) + raw.slice(index + 1);
        at = index;
      }
    }
  } else if (raw.length > previous.length) {
    // Insertion (typing or paste) ends after the inserted text.
    at =
      previousSelection.start +
      (raw.length - (previous.length - (previousSelection.end - previousSelection.start)));
  }
  const next = maskIdentifierInput(kind, text);
  if (next === text) return { text: next, caret: Math.min(at, next.length) };
  return { text: next, caret: caretAfterDigits(next, digitsIn(text.slice(0, at)).length) };
}

/** Digits suitable for submission where the contract expects digits only (verbatim otherwise). */
export function identifierSubmitValue(kind: IdentifierKind, text: string): string {
  return maskIdentifierInput(kind, identifierDigits(text)) === text ? identifierDigits(text) : text;
}

/**
 * Money entry (money-input.md): `''` is blank (**não informado**) and differs from `'0'`, a
 * recorded zero. The field shows `displayed` (core's `formatMoneyDigits`) with the caret kept at
 * the end, so a deletion removes the last cent digit (back to blank after the first), one typed
 * digit is appended right to left, and a multi-character insertion is a paste parsed by core's
 * `parseBrl` (signed, malformed or out-of-range pastes are ignored). A 15th digit is refused.
 */
export function editMoneyDigits(current: string, displayed: string, raw: string): string {
  if (raw.length < displayed.length) return current.slice(0, -1);
  const { inserted } = changedSpan(displayed, raw);
  if (inserted.length === 1) {
    if (!isDigit(inserted)) return current;
    return normalizeMoneyDigits(current + inserted) ?? current;
  }
  if (inserted.length === 0) return current;
  const parsed = parseBrl(inserted);
  return parsed.ok ? moneyDigitsFromCents(parsed.cents) : current;
}

/** What the field shows for an entry state: blank stays empty; digits through core. */
export function displayMoneyDigits(digits: string): string {
  return digits === '' ? '' : formatMoneyDigits(digits);
}

/**
 * A native MoneyInput change → the new entry digits. Native keystrokes can arrive before the
 * field shows the re-rendered (formatted) value, so the result never depends on which text the
 * change was made against: the entry is the digits the field now holds, leading zeros dropped
 * (`0,012` and `12` both mean `12`). Only two cases need more: a change that leaves no significant
 * digit (blank after a deletion, a recorded zero after typing `0`), and a paste — more than one
 * new digit at once, or text with `R$` — which core's `parseBrl` reads as a whole amount.
 * Any other change (a replacement bringing its own separators, spaces or symbols) must parse as
 * a whole amount; a malformed one (`-1`, `abc1`, `R1`, `1$`, `1,,`, `1.5`, `1,2,3`) keeps
 * `previous`, so digit extraction never turns an invalid paste into an amount. `previous` is the last entry this
 * field emitted (not a possibly stale prop).
 */
export function editMoneyText(previous: string, displayed: string, raw: string): string {
  if (!moneyChars.test(raw)) return previous;
  if (/R\$/.test(raw) && raw.length > displayed.length)
    return editMoneyDigits(previous, displayed, raw);
  if (/R\$/.test(raw) || !editsDisplayed(displayed, raw)) {
    const parsed = parseBrl(raw);
    return parsed.ok ? moneyDigitsFromCents(parsed.cents) : previous;
  }
  const typed = digitsIn(raw);
  const significant = typed.replace(/^0+/, '');
  const before = previous.replace(/^0+/, '');
  if (significant.length > before.length + 1) return editMoneyDigits(previous, displayed, raw);
  if (significant === '') {
    const shortened = raw.length < displayed.length || typed.length < digitsIn(displayed).length;
    return typed === '' || shortened ? '' : '0';
  }
  return normalizeMoneyDigits(significant) ?? previous;
}
