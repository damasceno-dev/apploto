import assert from 'node:assert/strict';
import test from 'node:test';
import { Linter } from 'eslint';
import tseslint from 'typescript-eslint';
import { rawColorRule } from './colors.mjs';
import { checkCss } from './check-css.mjs';

const linter = new Linter();
function lint(code) {
  return linter.verify(
    code,
    [
      {
        files: ['**/*.tsx'],
        languageOptions: {
          parser: tseslint.parser,
          parserOptions: { ecmaFeatures: { jsx: true } },
        },
        plugins: { loto: { rules: { 'raw-color': rawColorRule } } },
        rules: { 'loto/raw-color': 'error' },
      },
    ],
    { filename: 'fixture.tsx' },
  );
}

test('reject actual style and arbitrary class colors', () => {
  for (const code of [
    'const x = <div style={{color: "red"}} />;',
    'const x = {backgroundColor: "#aabbcc"};',
    'const x = {border: "1px solid rgb(1 2 3)"};',
    'const x = <div className="hover:bg-[#abc] text-[hsl(0_0%_0%)]"/>;',
    'const x = {shadowColor: `rebeccapurple`};',
    'const x = <svg fill="blue"/>;',
    "export const danger = '#d00000';",
    "const tone = '  #abc  ';",
    "const tone = '  rgb(10 20 30)  ';",
    "const tone = 'oklch(50% 0.2 30)';",
    'const x = {border: "1px solid #abc"};',
    'const x = <div style={{boxShadow: "0 0 2px #abc"}}/>;',
    "const palette = {primary: 'rgb(10 20 30)', accent: 'hsl(0 100% 50%)'};",
    "const palette = {tint: '#00ff00'};",
    'export const danger = `#d00000`;',
    "const danger = '#d00000'; const classes = `bg-[${danger}]`;",
    'const tone = "red"; const x = { color: tone };',
    'const tone = "#abc"; const x = <div className={"bg-[" + tone + "]"}/>;',
  ])
    assert.ok(
      lint(code).some((m) => m.ruleId === 'loto/raw-color'),
      code,
    );
});

test('ordinary copy, fragments, URLs and dimensions are not colors', () => {
  for (const code of [
    'const x = <>red</>;',
    "const label = 'Bolão #123 confirmado';",
    'const x = <button aria-label="Excluir lançamento #abc"/>;',
    'const label = `Bolão #123 confirmado`;',
    "const label = 'Exemplo rgb(10 20 30) explicado';",
    "const label = 'rgb(10 20 30) explicado';",
    'const x = <a href="#abc">red</a>;',
    'const urls = {href: "#abc", uri: "#def"};',
    'const label = "red"; const href = "https://example.test/#abc";',
    'const labels = { label: "red", colorLabel: "red", backgroundLabel: "red" };',
    'const x = <div className="text-[14px] p-8"/>;',
    'const x = {backgroundImage: "url(https://example.test/red.svg#abc)"};',
    'const x = {color: "var(--text-default)"};',
  ])
    assert.equal(lint(code).length, 0, code);
});

test('CSS declarations cover stylesheets without matching content/URLs', () => {
  for (const css of [
    'a { color: red }',
    'a { @apply bg-[#ff0000]; }',
    '@supports (color: rgb(10 20 30)) { a { display: block } }',
    'a { color: var(--text, red) }',
    'a { --bad: #aabbcc }',
    'a { box-shadow: 0 0 2px hsl(0 0% 0%) }',
  ])
    assert.ok(checkCss(css, 'test.css').length);
  assert.deepEqual(
    checkCss(
      'a { content: "red"; background: url(red.svg#abc); color: var(--text-default) }',
      'test.css',
    ),
    [],
  );
});

test('each direct style/color violation has one diagnostic', () => {
  for (const code of [
    'const x = {color: "red"};',
    'const x = <div style={{color: "#abc"}}/>;',
    'const x = <svg fill="blue"/>;',
  ]) {
    assert.equal(
      lint(code).filter((message) => message.ruleId === 'loto/raw-color').length,
      1,
      code,
    );
  }
});

test('project lint rejects imported palette definitions used by styles and interpolated classes', () => {
  const palette = "export const danger = '#d00000';";
  const consumer =
    "import { danger } from './palette'; const style = { color: danger }; const classes = `bg-[${danger}]`;";
  // No import execution/cross-file evaluation: the forbidden literal is rejected at its definition.
  const messages = [palette, consumer].flatMap((source) => lint(source));
  assert.equal(messages.filter((message) => message.ruleId === 'loto/raw-color').length, 1);
});

// Fixture classes are assembled at run time so this file itself names no primitive.
const ramp = (utility, name, step = '') => [utility, name, step].filter(Boolean).join('-');
const primitivePath = ['color', 'primitive', 'blue', '600'].join('.');

test('primitive references fail in classes, token paths and CSS variables (Web M3 1.4)', () => {
  for (const code of [
    `const x = <div className="p-4 ${ramp('bg', 'blue', '600')}"/>;`,
    `const x = cn('${ramp('hover:text', 'red', '700')}/50');`,
    `const tone = '${ramp('border-t', 'neutral', '200')}';`,
    `const x = <p className="${ramp('text', 'black')}"/>;`,
    `const ref = '{${primitivePath}}';`,
    `const x = { color: 'var(--${ramp('color', 'teal', '400')})' };`,
    `const classes = \`\${active ? '${ramp('bg', 'orange', '500')}' : ''}\`;`,
  ]) {
    assert.ok(
      lint(code).some((m) => m.ruleId === 'loto/raw-color'),
      code,
    );
  }
  for (const css of [
    `a { @apply ${ramp('bg', 'sky', '600')}; }`,
    `a { color: var(--${ramp('color', 'night', '950')}); }`,
    `@theme { --${ramp('color', 'amber', '700')}: inherit; }`,
    `.${ramp('text', 'green', '600')} { display: block }`,
  ]) {
    assert.ok(checkCss(css, 'test.css').length, css);
  }
});

test('semantic and domain token names, and copy that mentions colors, pass', () => {
  for (const code of [
    'const x = <div className="bg-surface-default text-text-primary border-border-control"/>;',
    'const x = <span className="text-feedback-neutral-fg bg-dom-aging-bucket-Days91Plus-bg"/>;',
    'const x = <nav className="bg-nav-bg text-nav-text-muted ring-focus-ring-on-nav"/>;',
    "const label = 'Saldo em vermelho no relatório';",
    "const label = 'neutral';",
  ]) {
    assert.equal(lint(code).length, 0, code);
  }
  assert.deepEqual(
    checkCss(
      'a { color: var(--color-text-primary); background: var(--loto-sem-background) }',
      't.css',
    ),
    [],
  );
});
