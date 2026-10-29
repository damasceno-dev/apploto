import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import * as api from '@loto/api';
import { describe, expect, it } from 'vitest';
import { enumLabel, enumLabels, generatedMovementMarker, roleLabels } from '../src/index';

// copy-guidelines.md §5 is the dictionary: every map is checked against the document's own text,
// and every key set against the generated enum the API exports at run time.
const copy = readFileSync(
  fileURLToPath(new URL('../../../design/specs/copy-guidelines.md', import.meta.url)),
  'utf8',
);
const section = copy.slice(
  copy.indexOf('## 5. Enum label maps'),
  copy.indexOf('## 6. Error display'),
);
const documented = new Map<string, Map<string, string>>();
for (const line of section.split('\n')) {
  const name = /^\*\*(\w+)\*\*/.exec(line)?.[1];
  if (name === undefined) continue;
  const pairs = new Map<string, string>();
  for (const match of line.matchAll(/`(\w+)` → "([^"]+)"/g))
    pairs.set(match[1] as string, match[2] as string);
  documented.set(name, pairs);
}

const generated = api as unknown as Record<string, Record<string, string> | undefined>;

describe('copy §5 enum label maps', () => {
  it('covers exactly the 33 documented enums', () => {
    expect(documented.size).toBe(33);
    expect(Object.keys(enumLabels).sort()).toEqual([...documented.keys()].sort());
  });

  it.each(Object.entries(enumLabels))(
    '%s: keys equal the generated enum, labels equal the copy',
    (name, labels) => {
      const values = generated[name];
      expect(values, `@loto/api exports ${name}`).toBeDefined();
      expect(Object.keys(labels).sort()).toEqual(Object.values(values ?? {}).sort());
      expect(new Map(Object.entries(labels))).toEqual(documented.get(name));
    },
  );

  it('keeps the generated-movement marker and the en dash in aging ranges', () => {
    expect(generatedMovementMarker).toBe('Automática');
    expect(section).toContain('"Automática"');
    expect(enumLabels.AgingBucket.Days0To30.codePointAt(1)).toBe(0x2013);
  });

  it('returns an unknown value verbatim and flagged, never a guess', () => {
    expect(enumLabel(roleLabels, 'Manager')).toEqual({ known: true, label: 'Gerente' });
    expect(enumLabel(roleLabels, 'Owner')).toEqual({ known: false, label: 'Owner' });
    expect(enumLabel(roleLabels, 'toString')).toEqual({ known: false, label: 'toString' });
    expect(enumLabel(roleLabels, '__proto__')).toEqual({ known: false, label: '__proto__' });
    expect(enumLabel(enumLabels.DailyCloseStatus, 'Submitted').label).toBe('Enviado');
    expect(enumLabel(enumLabels.TransactionStatus, 'Draft').label).toBe('Rascunho');
  });
});
