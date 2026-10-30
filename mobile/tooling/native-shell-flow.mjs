// Real native controls + owned API fixtures. No remote JavaScript, app-data reset or token injection.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const [platform, statePath, output, userKey = 'ana'] = process.argv.slice(2);
const root = fileURLToPath(new URL('../../', import.meta.url));
if (
  !['ios', 'android'].includes(platform) ||
  !statePath ||
  !output ||
  !['ana', 'managerLinked'].includes(userKey) ||
  !path.isAbsolute(output) ||
  path.resolve(output).startsWith(root)
)
  throw new Error(
    'Use ios|android <owned-state> <outside-checkout-flow.yaml> [ana|managerLinked].',
  );
const state = JSON.parse(await readFile(statePath, 'utf8'));
const user = state.users?.[userKey];
if (
  !state.owned ||
  !user?.email?.endsWith('@example.invalid') ||
  !['127.0.0.1', 'localhost'].includes(new URL(state.apiUrl).hostname)
)
  throw new Error('Only owned loopback fake-identity fixtures are supported.');
const tap = (id) => ({ tapOn: { id } });
const text = (value) => ({ tapOn: { text: value } });
const wait = (id) => ({ extendedWaitUntil: { visible: { id }, timeout: 60000 } });
const scroll = (id) => ({ scrollUntilVisible: { element: { id }, direction: 'DOWN' } });
const optional = (visible, commands) => ({ runFlow: { when: { visible }, commands } });
const logout = [
  tap('session-menu'),
  scroll('logout'),
  tap('logout'),
  optional('Sair da conta\\?', [text('Sair e manter os dados')]),
  wait('login-email'),
];
const login = [
  wait('login-email'),
  tap('login-email'),
  ...(platform === 'android'
    ? [{ longPressOn: { id: 'login-email' } }, { eraseText: 1 }]
    : [{ eraseText: 100 }]),
  { inputText: user.email },
  tap('login-password'),
  { inputText: user.password },
  { hideKeyboard: {} },
  optional({ id: 'login-submit' }, [tap('login-submit')]),
  optional('Save Password\\?', [text('Not Now')]),
];
const choose = (id) => [wait('branch-' + id), tap('branch-' + id), wait('navigation-menu')];
const commands = [
  optional('Save Password\\?', [text('Not Now')]),
  optional({ id: 'session-menu' }, logout),
  ...login,
  ...(userKey === 'ana' ? choose(state.branches.alfa) : [wait('navigation-menu')]),
  { assertVisible: 'Lotérica Alfa E2E.*' },
  { takeScreenshot: platform + '-shell-home' },
  tap('navigation-menu'),
  { assertVisible: 'horário da filial|.*horário da filial' },
  { takeScreenshot: platform + '-drawer' },
  text('^Nova transação$'),
  { assertVisible: 'Os dados e as ações de nova transação.*' },
  ...(platform === 'android' ? [{ pressKey: 'Back' }] : [text('Voltar para Transações')]),
  { assertVisible: 'Os dados e as ações de transações.*' },
  tap('session-menu'),
  text('^Minha conta$'),
  { assertVisible: 'Os dados e as ações de conta e acesso.*' },
  ...(platform === 'ios'
    ? [{ swipe: { direction: 'RIGHT', duration: 700 } }]
    : [{ pressKey: 'Back' }]),
  wait('navigation-menu'),
  { pressKey: 'Home' },
  { launchApp: { stopApp: false } },
  wait('navigation-menu'),
  tap('session-menu'),
  ...(userKey === 'managerLinked'
    ? [
        text('^Meu turno$'),
        wait('navigation-menu'),
        { assertVisible: 'Os dados e as ações de hoje.*' },
        tap('navigation-menu'),
        { assertNotVisible: '^Ponto$' },
        text('^Hoje$'),
        tap('session-menu'),
        text('^Gestão$'),
        wait('navigation-menu'),
        tap('session-menu'),
      ]
    : [{ assertVisible: 'Indisponível: seu login não está vinculado.*' }]),
  text('^Escuro$'),
  { takeScreenshot: platform + '-session-menu-dark' },
  ...(userKey === 'ana'
    ? [
        tap('switch-branch'),
        wait('branch-' + state.branches.beta),
        tap('branch-' + state.branches.beta),
        wait('navigation-menu'),
        { assertVisible: 'Lotérica Beta E2E.*' },
        tap('session-menu'),
      ]
    : []),
  text('^Sistema$'),
  { tapOn: { text: '^Fechar$', optional: true } },
  ...logout,
  { takeScreenshot: platform + '-logged-out' },
  'stopApp',
  { launchApp: { stopApp: false } },
  wait('login-email'),
  { openLink: 'lotero://relatorios/dashboard?branchId=' + state.branches.alfa },
  wait('login-email'),
  { assertNotVisible: 'Os dados e as ações de dashboard.*' },
  ...login,
  ...(userKey === 'ana' ? choose(state.branches.alfa) : [wait('navigation-menu')]),
  { assertVisible: 'Os dados e as ações de dashboard.*' },
  ...logout,
];
await mkdir(path.dirname(output), { recursive: true });
await writeFile(
  output,
  JSON.stringify({ appId: 'com.example.lotero.development' }) +
    '\n---\n' +
    commands
      .map((command) => '- ' + (typeof command === 'string' ? command : JSON.stringify(command)))
      .join('\n') +
    '\n',
  { mode: 0o600 },
);
console.log('Native shell flow written outside Git for ' + platform + ' (' + userKey + ').');
