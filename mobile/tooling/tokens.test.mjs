import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile } from 'node:fs/promises';
import { adapterPath, renderAdapter, tokenDirectory, unknownColorClasses } from './tokens.mjs';
import {
  colorVariableNames,
  colorVariables,
  loadTokens,
  nativeTokens,
  readTokenDirectory,
} from './token-resolver.mjs';

const sources = readTokenDirectory(tokenDirectory);
const json = (name) => JSON.parse(sources[name]);
const withFile = (name, value) => ({ ...sources, [name]: JSON.stringify(value) });
function rejects(changed, code) {
  assert.throws(
    () => loadTokens(changed),
    (error) => error.code === code,
    code,
  );
}

test('the committed tokens resolve and the generated adapter is current and primitive-free', async () => {
  const values = loadTokens(sources);
  assert.equal(await readFile(adapterPath, 'utf8'), await renderAdapter(values));
  const tokens = nativeTokens(values);
  // Light/dark parity of every app-facing color, and every value a literal color.
  const light = colorVariables(values, 'light');
  const dark = colorVariables(values, 'dark');
  assert.deepEqual(Object.keys(light).sort(), Object.keys(dark).sort());
  for (const value of [...Object.values(light), ...Object.values(dark)])
    assert.match(value, /^#[0-9a-f]{6}([0-9a-f]{2})?$/);
  assert.equal(colorVariableNames(values).length, Object.keys(light).length);
  // No primitive is reachable by name.
  const adapter = await readFile(adapterPath, 'utf8');
  assert.doesNotMatch(adapter, /primitive[.:'"]|\bnight\b|\bnavy\b/);
  assert.ok(!('primitive' in tokens.themes.light));
  // Units: letterSpacing em → px of its own size; tabular figures; shadows split alpha for iOS.
  assert.equal(tokens.typography['table-header'].letterSpacing, 0.65);
  assert.deepEqual(tokens.typography.money.fontVariant, ['tabular-nums', 'lining-nums']);
  assert.equal(tokens.themes.light.shadow.floating.shadowColor.length, 7);
  assert.ok(
    tokens.themes.light.shadow.floating.shadowOpacity > 0 &&
      tokens.themes.light.shadow.floating.shadowOpacity < 1,
  );
  assert.equal(tokens.themes.dark.backdropOpacity, 0.6);
  assert.equal(tokens.motion.reduced.toast.duration, 0);
});

test('malformed sources fail with named errors', () => {
  const { tokenFiles } = { tokenFiles: Object.keys(sources) };
  assert.equal(tokenFiles.length, 8);
  const missing = { ...sources };
  delete missing['radii.json'];
  rejects(missing, 'missing-file');
  rejects({ ...sources, 'extra.json': '{}' }, 'unexpected-file');
  rejects({ ...sources, 'motion.json': '{ nope' }, 'invalid-json');
  rejects(withFile('radii.json', { radius: {}, spacing: {} }), 'bad-group');
  const radii = json('radii.json');
  rejects(withFile('radii.json', { radius: { ...radii.radius, 'bad key': 2 } }), 'bad-key');
  rejects(
    withFile('radii.json', { radius: { ...radii.radius, control: '{radius.md} px' } }),
    'embedded-reference',
  );
  rejects(
    withFile('radii.json', { radius: { ...radii.radius, control: '{radius.huge}' } }),
    'missing-reference',
  );
  rejects(
    withFile('radii.json', { radius: { ...radii.radius, control: '{spacing.scale}' } }),
    'group-reference',
  );
  rejects(
    withFile('radii.json', {
      radius: { ...radii.radius, sm: '{radius.control}', control: '{radius.sm}' },
    }),
    'cycle',
  );
});

test('layering, literals, cross-theme references, parity and values are enforced', () => {
  const colors = json('colors.json');
  const mutate = (change) => {
    const copy = structuredClone(colors);
    change(copy.color);
    return withFile('colors.json', copy);
  };
  rejects(
    mutate((color) => (color.semantic.light.text.primary = '#000000')),
    'semantic-literal',
  );
  rejects(
    mutate((color) => (color.semantic.light.text.primary = '{color.semantic.dark.text.primary}')),
    'cross-theme',
  );
  rejects(
    mutate((color) => (color.semantic.light.text.primary = '{color.domain.light.direction.In.fg}')),
    'layer-violation',
  );
  rejects(
    mutate((color) => (color.primitive.blue['600'] = '{color.primitive.blue.700}')),
    'layer-violation',
  );
  rejects(
    mutate((color) => delete color.semantic.dark.text.muted),
    'theme-asymmetry',
  );
  rejects(
    mutate(
      (color) => (color.domain.light.direction.In.extra = '{color.semantic.light.text.primary}'),
    ),
    'theme-asymmetry',
  );
  rejects(
    mutate((color) => delete color.domain.dark),
    'theme-asymmetry',
  );
  rejects(
    mutate((color) => (color.primitive.blue['600'] = 'blue')),
    'invalid-value',
  );
  const shadows = json('shadows.json');
  shadows.shadow.light.raised.color = '{color.primitive.black}';
  rejects(withFile('shadows.json', shadows), 'layer-violation');
  const motion = json('motion.json');
  motion.motion.easing.standard = [0.2, 0, 1];
  rejects(withFile('motion.json', motion), 'invalid-value');
  const layers = json('layers.json');
  layers.layer.dialog = 410.5;
  rejects(withFile('layers.json', layers), 'invalid-value');
});

test('class names may consume only existing semantic/domain colors', () => {
  const names = colorVariableNames(loadTokens(sources));
  assert.deepEqual(
    unknownColorClasses(
      '<View className="bg-sem-surface-default text-dom-cash-variance-shortage-fg" />',
      names,
    ),
    [],
  );
  assert.deepEqual(
    unknownColorClasses(
      '<View className="bg-sem-surface-nope border-dom-direction-Sideways-fg text-dom-cash-variance-shortage" />',
      names,
    ),
    ['sem-surface-nope', 'dom-direction-Sideways-fg', 'dom-cash-variance-shortage'],
  );
  assert.deepEqual(
    unknownColorClasses('<Text className="active:text-sem-text-primary/50" />', names),
    [],
  );
});
