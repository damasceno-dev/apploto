// Client fiado balance presentation (design/specs/formatting-ptbr.md §3, "a display mapping, not a
// signed value"). The API signs fiado balances as `Out − In`; the screen shows debt unsigned, zero
// as **quitado** and credit as its absolute value with an explicit `+` and the mandatory label.
// This maps one API-provided figure: it never sums, nets or recomputes balances across clients.
import { asCents, centsFromWire, formatBrl, type Cents } from './brl';

export type FiadoBalanceKind = 'debt' | 'settled' | 'credit';

export interface FiadoBalancePresentation {
  /** `debt` (> 0), `settled` (= 0) or `credit` (< 0): the screen picks its domain color from it. */
  readonly kind: FiadoBalanceKind;
  /** The API's signed `Out − In` value, unchanged. */
  readonly signedCents: Cents;
  /** `R$ 290,00`, `+R$ 10,00`, or null when settled (no amount is shown). */
  readonly amount: string | null;
  /** `em aberto`, `quitado` or `crédito do cliente` — never omitted (color and sign are not enough). */
  readonly label: string;
  /** The whole display: `R$ 290,00 · em aberto`, `quitado`, `+R$ 10,00 · crédito do cliente`. */
  readonly text: string;
}

export function presentFiadoBalance(signedCents: Cents): FiadoBalancePresentation {
  const value = asCents(signedCents);
  if (value === 0) {
    return { kind: 'settled', signedCents: value, amount: null, label: 'quitado', text: 'quitado' };
  }
  const debt = value > 0;
  const amount = debt ? formatBrl(value) : `+${formatBrl(asCents(-value))}`;
  const label = debt ? 'em aberto' : 'crédito do cliente';
  return {
    kind: debt ? 'debt' : 'credit',
    signedCents: value,
    amount,
    label,
    text: `${amount} · ${label}`,
  };
}

/** `presentFiadoBalance` for a wire number (`OutstandingTotal`, `RunningBalance`, …). */
export function presentFiadoBalanceWire(value: number): FiadoBalancePresentation {
  return presentFiadoBalance(centsFromWire(value));
}
