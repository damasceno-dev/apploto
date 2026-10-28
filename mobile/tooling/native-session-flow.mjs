// Native button flow. Input data arrives from the owned DEV fixture server; auth/storage stay real.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { nativeAppId } from './native-app.mjs';
const [platform, statePath, fixturePath, output] = process.argv.slice(2);
if (!['ios', 'android'].includes(platform) || !statePath || !fixturePath || !output)
  throw new Error(
    'Usage: node tooling/native-session-flow.mjs ios|android <owned-state> <fixture-json> <outside-checkout-flow.yaml>',
  );
const root = fileURLToPath(new URL('../../', import.meta.url));
if (!path.isAbsolute(output) || path.resolve(output).startsWith(root))
  throw new Error('Keep native artifacts outside the checkout.');
const state = JSON.parse(await readFile(statePath, 'utf8'));
const fixture = JSON.parse(await readFile(fixturePath, 'utf8'));
if (
  !['127.0.0.1', 'localhost'].includes(new URL(state.apiUrl).hostname) ||
  !state.owned ||
  !state.users?.ana?.email?.endsWith('@example.invalid') ||
  !fixture.body
)
  throw new Error('Use owned disposable fixtures only.');
const scroll = (id, direction = 'DOWN') => ({ scrollUntilVisible: { element: { id }, direction } });
const tap = (id) => ({ tapOn: { id } });
const press = (id, direction = 'DOWN') => [scroll(id, direction), tap(id)];
const wait = (id, text) => ({ extendedWaitUntil: { visible: { id, text }, timeout: 60000 } });
const load = () => [
  ...press('session-load-fixture', 'UP'),
  wait('session-fixture-state', 'fixture-ready'),
];
const commands = [
  ...(platform === 'android'
    ? [{ runFlow: { when: { visible: 'Try out your stylus' }, commands: [{ tapOn: 'Cancel' }] } }]
    : []),
  'stopApp',
  { launchApp: { stopApp: false } },
  ...['Open', 'Continue'].map((text) => ({
    runFlow: { when: { visible: text }, commands: [{ tapOn: text }] },
  })),
  {
    runFlow: {
      when: { visible: 'Reload' },
      commands: platform === 'ios' ? [{ tapOn: { point: '90%,48%' } }] : [{ pressKey: 'Back' }],
    },
  },
  wait('session-status', 'anonymous|identity|branch'),
  {
    runFlow: {
      when: { visible: { id: 'session-status', text: 'identity|branch' } },
      commands: [...press('session-logout'), wait('session-status', 'anonymous')],
    },
  },
  ...load(),
  ...press('session-login'),
  wait('session-status', 'identity'),
  { assertVisible: 'sem filial' },
  ...load(),
  ...press('session-select-branch'),
  wait('session-status', 'branch'),
  { assertVisible: 'contexto verificado' },
  ...load(),
  ...press('session-save-note'),
  scroll('session-note-state'),
  wait('session-note-state', 'Nota guardada neste dispositivo'),
  ...press('session-hold-intent'),
  ...press('session-resume-intent'),
  scroll('session-resumed-intent'),
  wait('session-resumed-intent', '/transacoes'),
  ...press('session-lose-response'),
  ...press('session-create-fixture'),
  scroll('session-operation-kind'),
  wait('session-operation-kind', 'uncertain'),
  'stopApp',
  { launchApp: { stopApp: false } },
  wait('session-status', 'branch'),
  ...press('session-replay-fixture'),
  scroll('session-operation-kind'),
  wait('session-operation-kind', 'confirmed'),
  ...press('session-restore-note', 'UP'),
  scroll('session-note-state'),
  wait('session-note-state', 'found'),
  { assertVisible: { id: 'session-note', text: `${platform} retained input final` } },
  ...press('session-grants', 'UP'),
  scroll('session-grants-result'),
  { assertVisible: { id: 'session-grants-result', text: '.*availableAccounts.*' } },
];
await mkdir(path.dirname(output), { recursive: true });
await writeFile(
  output,
  JSON.stringify({ appId: nativeAppId() }) +
    '\n---\n' +
    commands.map((command) => '- ' + JSON.stringify(command)).join('\n') +
    '\n',
  { mode: 0o600 },
);
console.log(
  'Native button flow generated outside the checkout. Enable the DEV fixture prefill and run OSs sequentially.',
);
