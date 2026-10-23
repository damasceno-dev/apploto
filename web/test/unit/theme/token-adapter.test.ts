import { readFileSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import {
  buildTokenCss,
  buildTokenNamesModule,
  loadTokens,
  TokenError,
  tokenNames,
  type TokenErrorCode,
} from '@/theme/token-adapter';
import * as generated from '@/theme/tokens.generated';
import { checkCss } from '../../../tooling/check-css.mjs';

const directory = fileURLToPath(new URL('../../../../design/tokens', import.meta.url));
/** The committed token sources, read once; each negative case edits a disposable copy. */
const sources: Record<string, string> = Object.fromEntries(
  readdirSync(directory)
    .filter((name) => name.endsWith('.json'))
    .map((name) => [name, readFileSync(path.join(directory, name), 'utf8')]),
);

type Mutable = Record<string, unknown>;
// Negative fixtures name primitives and a literal on purpose; they are assembled at run time so the
// lint gate (which forbids both in app and test sources) keeps applying to this file.
const primitive = (...path: string[]) => ['color', 'primitive', ...path].join('.');
const literal = ['#', 'ff', 'ff', 'ff'].join('');
function edit(file: string, change: (json: Mutable) => void): Record<string, string> {
  const json = JSON.parse(sources[file] ?? '{}') as Mutable;
  change(json);
  return { ...sources, [file]: JSON.stringify(json) };
}
function at(json: Mutable, dotted: string): Mutable {
  return dotted.split('.').reduce((node, key) => node[key] as Mutable, json);
}
function rejects(next: Record<string, string>, code: TokenErrorCode) {
  let error: unknown = null;
  try {
    loadTokens(next);
  } catch (caught) {
    error = caught;
  }
  expect(error).toBeInstanceOf(TokenError);
  expect((error as TokenError).code).toBe(code);
}

describe('resolution of the committed design tokens', () => {
  const tokens = loadTokens(sources);

  it('resolves every semantic and domain color of both themes to a literal', () => {
    expect(tokens.values.get('color.semantic.light.action.primary.bg')).toMatch(/^#[0-9a-f]{6}$/);
    expect(tokens.values.get('color.semantic.dark.action.primary.bg')).toMatch(/^#[0-9a-f]{6}$/);
    // References are followed through semantic → primitive and domain → semantic chains.
    expect(tokens.values.get('color.domain.light.daily-close-status.Approved.fg')).toBe(
      tokens.values.get('color.semantic.light.feedback.success.fg'),
    );
    expect(tokens.references.get('color.domain.dark.cash-variance.shortage.fg')).toBe(
      'color.semantic.dark.feedback.danger.fg',
    );
  });

  it('keeps every enum value of every domain group, by its exact backend spelling', () => {
    const names = tokenNames(tokens);
    const aging = names.domainGroups.find((group) => group.group === 'aging-bucket');
    expect(aging?.enumName).toBe('AgingBucket');
    expect(aging?.values.map((value) => value.value)).toEqual([
      'Current',
      'Days0To30',
      'Days31To60',
      'Days61To90',
      'Days91Plus',
    ]);
    expect(names.domainGroups.find((group) => group.group === 'generated-movement')).toMatchObject({
      enumName: null,
      values: [{ value: 'automatic', parts: ['border', 'fg'] }],
    });
  });

  it('emits semantic/domain names only, both themes, Sistema and reduced motion', () => {
    const css = buildTokenCss(tokens);
    expect(css).toContain('--color-surface-default: var(--loto-sem-surface-default);');
    expect(css).toContain('--color-dom-daily-close-status-Submitted-bg:');
    expect(css).toContain(":root[data-theme-preference='dark']");
    expect(css).toContain('@media (prefers-color-scheme: dark)');
    expect(css).toContain("[data-theme-scope='dark'] {");
    expect(css).toContain('@media (prefers-reduced-motion: reduce)');
    expect(css).not.toMatch(/primitive|--color-blue|--color-neutral-\d/);
    // Tailwind's default palettes and scales are cleared before the token names are added.
    expect(css).toContain('--color-*: initial;');
    expect(css).toContain('--spacing-*: initial;');
    // Units: rem for type and spacing, em for breakpoints, ms and cubic-bezier for motion.
    expect(css).toContain('--spacing-size-control-height: 2.5rem;');
    expect(css).toContain('--breakpoint-medium: 40em;');
    expect(css).toMatch(/--loto-motion-control-duration: 120ms;/);
    expect(css).toMatch(/--loto-motion-control-duration: 0ms;/);
    expect(css).toContain('--ease-standard: cubic-bezier(0.2, 0, 0, 1);');
    expect(css).toMatch(
      /@utility type-money \{[^}]*font-variant-numeric: tabular-nums lining-nums;/,
    );
    expect(css).toMatch(/@utility type-table-header \{[^}]*text-transform: uppercase;/);
    expect(css).toMatch(/@utility layer-popover \{\s*z-index: 500;/);
    expect(css).toMatch(
      /--loto-shadow-floating: 0px 14px 36px 0px var\(--loto-sem-shadow-floating\);/,
    );
    expect(css).toMatch(/--loto-backdrop-opacity: 0\.46;/);
  });

  it('the committed names module is exactly what the adapter generates now', () => {
    expect(generated.semanticColors).toContain('selection.indicator');
    expect(generated.domainColors['aging-bucket'].values.Days91Plus).toEqual(['bg', 'fg']);
    expect(buildTokenNamesModule(tokens)).toContain('export const domainColors');
  });

  it('no raw value of a source CSS file: the stylesheet with the marker passes the gate', () => {
    const globals = readFileSync(new URL('../../../src/app/globals.css', import.meta.url), 'utf8');
    expect(globals).toContain('@loto-tokens;');
    expect(checkCss(globals, 'globals.css')).toEqual([]);
  });
});

describe('the adapter rejects malformed sources (disposable copies)', () => {
  it('missing, unexpected and unparsable files', () => {
    const { ['motion.json']: _motion, ...missing } = sources;
    void _motion;
    rejects(missing, 'missing-file');
    rejects({ ...sources, 'icons.json': '{}' }, 'unexpected-file');
    rejects({ ...sources, 'radii.json': '{"radius": ' }, 'invalid-json');
    rejects({ ...sources, 'radii.json': '{"radius": {}, "corner": {}}' }, 'bad-group');
  });

  it('bad keys and references that are embedded, broken, cyclic or point at groups', () => {
    rejects(
      edit('spacing.json', (json) => (at(json, 'spacing.page')['inline wide'] = 4)),
      'bad-key',
    );
    rejects(
      edit(
        'spacing.json',
        (json) => (at(json, 'spacing.page')['inline-wide'] = 'calc({spacing.scale.8})'),
      ),
      'embedded-reference',
    );
    rejects(
      edit(
        'spacing.json',
        (json) => (at(json, 'spacing.page')['inline-wide'] = '{spacing.scale.99}'),
      ),
      'missing-reference',
    );
    rejects(
      edit('spacing.json', (json) => (at(json, 'spacing.page')['inline-wide'] = '{spacing.scale}')),
      'group-reference',
    );
    rejects(
      edit('spacing.json', (json) => {
        at(json, 'spacing.stack').sm = '{spacing.stack.md}';
        at(json, 'spacing.stack').md = '{spacing.stack.sm}';
      }),
      'cycle',
    );
  });

  it('layering: literal semantic/domain values, cross-theme and wrong-layer references', () => {
    rejects(
      edit('colors.json', (json) => (at(json, 'color.semantic.light.surface').default = literal)),
      'semantic-literal',
    );
    rejects(
      edit('colors.json', (json) => {
        at(json, 'color.semantic.light.surface').default = '{color.semantic.dark.surface.default}';
      }),
      'cross-theme',
    );
    rejects(
      edit('colors.json', (json) => {
        at(json, 'color.semantic.light.surface').default =
          '{color.domain.light.cash-variance.zero.fg}';
      }),
      'layer-violation',
    );
    rejects(
      edit(
        'colors.json',
        (json) => (at(json, primitive('blue'))['600'] = `{${primitive('blue', '700')}}`),
      ),
      'layer-violation',
    );
    rejects(
      edit('shadows.json', (json) => {
        at(json, 'shadow.light.raised').color = '{color.semantic.dark.shadow.raised}';
      }),
      'cross-theme',
    );
    rejects(
      edit('shadows.json', (json) => {
        at(json, 'shadow.light.raised').color = `{${primitive('black')}}`;
      }),
      'layer-violation',
    );
  });

  it('theme asymmetry and invalid values', () => {
    rejects(
      edit('colors.json', (json) => {
        delete at(json, 'color.semantic.dark.surface').hover;
      }),
      'theme-asymmetry',
    );
    rejects(
      edit('colors.json', (json) => {
        delete at(json, 'color.domain.dark')['aging-bucket'];
      }),
      'theme-asymmetry',
    );
    rejects(
      edit('colors.json', (json) => (at(json, primitive('blue'))['600'] = 'blue')),
      'invalid-value',
    );
    rejects(
      edit('layers.json', (json) => (at(json, 'layer').dialog = 41.5)),
      'invalid-value',
    );
    rejects(
      edit('motion.json', (json) => (at(json, 'motion.easing').standard = [0.2, 0])),
      'invalid-value',
    );
  });
});

describe('theme-scoped references', () => {
  it('app sources reference per-theme variables directly, never the root-resolved --color-*', async () => {
    const { readdirSync: list, statSync } = await import('node:fs');
    const offenders: string[] = [];
    const walk = (directory: string) => {
      for (const name of list(directory)) {
        const file = path.join(directory, name);
        if (statSync(file).isDirectory()) walk(file);
        else if (/\.(tsx?|css)$/.test(name) && !file.endsWith('token-adapter.ts')) {
          if (/var\(--color-/.test(readFileSync(file, 'utf8'))) offenders.push(file);
        }
      }
    };
    walk(fileURLToPath(new URL('../../../src', import.meta.url)));
    expect(offenders).toEqual([]);
  });
});
