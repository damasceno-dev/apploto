// Spoken money for assistive technology (design/specs/components/money-input.md, Accessibility):
// **doze reais e trinta e quatro centavos**, blank is the field's own **não informado**, zero is
// **zero reais**. Hand-rolled pt-BR cardinals, so web and native speak the same words without an
// engine spell-out facility. Masculine forms throughout (*real*, *centavo*).
import { asCents, type Cents } from './brl';

const units = [
  'zero',
  'um',
  'dois',
  'três',
  'quatro',
  'cinco',
  'seis',
  'sete',
  'oito',
  'nove',
  'dez',
  'onze',
  'doze',
  'treze',
  'catorze',
  'quinze',
  'dezesseis',
  'dezessete',
  'dezoito',
  'dezenove',
];
const tens = [
  '',
  '',
  'vinte',
  'trinta',
  'quarenta',
  'cinquenta',
  'sessenta',
  'setenta',
  'oitenta',
  'noventa',
];
const hundreds = [
  '',
  'cento',
  'duzentos',
  'trezentos',
  'quatrocentos',
  'quinhentos',
  'seiscentos',
  'setecentos',
  'oitocentos',
  'novecentos',
];

/** 1–999 in words; the parts of a group are joined by **e** (`trezentos e quarenta e cinco`). */
function groupWords(value: number): string {
  if (value === 100) return 'cem';
  const parts: string[] = [];
  const hundred = Math.floor(value / 100);
  const rest = value % 100;
  if (hundred > 0) parts.push(hundreds[hundred] as string);
  if (rest > 0) {
    if (rest < 20) parts.push(units[rest] as string);
    else {
      const unit = rest % 10;
      parts.push(
        unit === 0
          ? (tens[Math.floor(rest / 10)] as string)
          : `${tens[Math.floor(rest / 10)]} e ${units[unit]}`,
      );
    }
  }
  return parts.join(' e ');
}

const scales: readonly (readonly [singular: string, plural: string])[] = [
  ['bilhão', 'bilhões'],
  ['milhão', 'milhões'],
  ['mil', 'mil'],
  ['', ''],
];

/**
 * 0–999 999 999 999 in words. Between groups, **e** precedes the last non-zero group when it is
 * below 100 or a whole hundred (`mil e um`, `um milhão e duzentos mil`), a space otherwise
 * (`dois mil quatrocentos e trinta e sete`). One thousand is **mil**, never *um mil*.
 */
function integerWords(value: number): string {
  if (value === 0) return 'zero';
  const groups = [
    Math.floor(value / 1e9) % 1000,
    Math.floor(value / 1e6) % 1000,
    Math.floor(value / 1e3) % 1000,
    value % 1000,
  ];
  let lastNonZero = -1;
  groups.forEach((group, index) => {
    if (group > 0) lastNonZero = index;
  });
  let out = '';
  groups.forEach((group, index) => {
    if (group === 0) return;
    const [singular, plural] = scales[index] as readonly [string, string];
    const phrase =
      index === 2 && group === 1
        ? 'mil'
        : singular === ''
          ? groupWords(group)
          : `${groupWords(group)} ${group === 1 ? singular : plural}`;
    if (out !== '')
      out += index === lastNonZero && (group < 100 || group % 100 === 0) ? ' e ' : ' ';
    out += phrase;
  });
  return out;
}

/**
 * Non-negative cents as spoken pt-BR money: `zero reais`, `um real`, `cinco centavos`,
 * `doze reais e trinta e quatro centavos`, `um milhão de reais` (**de** after exact millions or
 * billions). A negative amount throws: use `spellSignedBrl`.
 */
export function spellBrl(cents: Cents): string {
  const value = asCents(cents);
  if (value < 0) throw new RangeError('spellBrl is unsigned; use spellSignedBrl.');
  const reais = Math.floor(value / 100);
  const centavos = value % 100;
  if (reais === 0 && centavos === 0) return 'zero reais';
  const parts: string[] = [];
  if (reais > 0) {
    const unit = reais === 1 ? 'real' : 'reais';
    const of = reais >= 1e6 && reais % 1e6 === 0 ? 'de ' : '';
    parts.push(`${integerWords(reais)} ${of}${unit}`);
  }
  if (centavos > 0)
    parts.push(`${integerWords(centavos)} ${centavos === 1 ? 'centavo' : 'centavos'}`);
  return parts.join(' e ');
}

/** Signed money as speech: `mais doze reais`, `menos doze reais`, zero `zero reais`. */
export function spellSignedBrl(cents: Cents): string {
  const value = asCents(cents);
  if (value === 0) return 'zero reais';
  return `${value > 0 ? 'mais' : 'menos'} ${spellBrl(asCents(Math.abs(value)))}`;
}
