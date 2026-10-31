// Writes native-touch M9 journeys outside Git. The API remains real; no token injection or JS execution.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { apiCall } from './native-stack.mjs';
const [os, statePath, output, role = 'member', entry = 'auto'] = process.argv.slice(2);
const root = fileURLToPath(new URL('../../', import.meta.url));
if (
  !['ios', 'android'].includes(os) ||
  !['member', 'member-fresh', 'manager', 'locked'].includes(role) ||
  !path.isAbsolute(output ?? '') ||
  path.resolve(output).startsWith(root)
)
  throw new Error(
    'Use ios|android <owned-state> <outside-checkout.yaml> member|member-fresh|manager|locked',
  );
const s = JSON.parse(await readFile(statePath, 'utf8'));
const key = role.startsWith('member')
  ? os === 'ios'
    ? 'memberLinked'
    : 'memberAndroid'
  : os === 'ios'
    ? 'managerLinked'
    : 'managerAndroid';
const user = s.users[key];
if (
  !s.owned ||
  !s.time ||
  !user.email.endsWith('@example.invalid') ||
  new URL(s.apiUrl).hostname !== '127.0.0.1'
)
  throw new Error('Owned local M9 fixtures required.');
let journeyDate = s.time.today;
if (role === 'manager') {
  const call = apiCall(s.apiUrl);
  const token = (
    await call(
      'POST',
      '/branch/session',
      { branchId: s.branches.alfa },
      (await call('POST', '/user/login', s.users.ana)).responseToken.token,
    )
  ).token;
  const operatorId = os === 'ios' ? s.time.manager : s.time.managerAndroid;
  const entries = (
    await call(
      'GET',
      '/timeentry?' +
        new URLSearchParams({
          OperatorId: operatorId,
          DateFrom: s.time.today.slice(0, 8) + '01',
          DateTo: s.time.today,
          PageSize: '100',
        }),
      undefined,
      token,
    )
  ).items;
  for (let offset = 0; offset < 28; offset++) {
    const candidate = new Date(s.time.today + 'T12:00:00Z');
    candidate.setUTCDate(candidate.getUTCDate() - offset);
    const date = candidate.toISOString().slice(0, 10);
    if (date.slice(0, 7) !== s.time.today.slice(0, 7)) break;
    if (!entries.some((entry) => entry.date.slice(0, 10) === date)) {
      journeyDate = date;
      break;
    }
  }
  s.time.journeyDates ??= {};
  s.time.journeyDates[os] = journeyDate;
  await writeFile(statePath, JSON.stringify(s), { mode: 0o600 });
}
const tap = (id) => ({ tapOn: { id } });
const text = (value) => ({ tapOn: { text: value } });
const wait = (id) => ({ extendedWaitUntil: { visible: { id, enabled: true }, timeout: 60000 } });
const scroll = (id) => ({
  scrollUntilVisible: { element: { id }, direction: 'DOWN', timeout: 30000 },
});
const optional = (visible, commands) => ({ runFlow: { when: { visible }, commands } });
const link = (url) => ({
  openLink: {
    link: 'lotero://' + url + (url.includes('?') ? '&' : '?') + 'branchId=' + s.branches.alfa,
  },
});
const login = [
  wait('login-email'),
  tap('login-email'),
  ...(os === 'android'
    ? [{ longPressOn: { id: 'login-email' } }, { eraseText: 1 }]
    : [{ eraseText: 100 }]),
  { inputText: user.email },
  tap('login-password'),
  { inputText: user.password },
  os === 'android' ? { pressKey: 'Enter' } : { hideKeyboard: {} },
  optional({ id: 'login-submit' }, [tap('login-submit')]),
  optional('Save Password\\?', [text('Not Now')]),
  wait('navigation-menu'),
];
const logout = [
  wait('navigation-menu'),
  tap('session-menu'),
  scroll('logout'),
  tap('logout'),
  optional('Sair da conta\\?', [text('Sair e manter os dados')]),
  wait('login-email'),
];
const commands = [
  { launchApp: { stopApp: true } },
  { extendedWaitUntil: { visible: { id: 'login-email|session-menu' }, timeout: 60000 } },
  ...(entry === 'authenticated'
    ? [wait('navigation-menu'), ...logout]
    : [optional({ id: 'session-menu' }, logout)]),
  // Cold/warm protected route is held by the real M4 gate until login and branch selection.
  link(
    role.startsWith('member')
      ? 'turno/ponto'
      : role === 'locked'
        ? `gestao/ponto/${s.time.entries.locked.id}`
        : 'gestao/ponto',
  ),
  ...login,
];
if (role === 'member')
  commands.push(
    wait('time-close-yesterday'),
    { takeScreenshot: os + '-time-overnight' },
    tap('time-close-yesterday'),
    wait('time-tap'),
    tap('time-tap'),
    { extendedWaitUntil: { visible: 'Em andamento desde.*', timeout: 30000 } },
    { takeScreenshot: os + '-time-running' },
    tap('time-tap'),
    { extendedWaitUntil: { visible: 'Dia encerrado', timeout: 30000 } },
    tap('navigation-menu'),
    text('^Minhas transações$'),
    { extendedWaitUntil: { visible: 'Volumes de movimentações registradas', timeout: 30000 } },
    { takeScreenshot: os + '-summary' },
    { scrollUntilVisible: { element: { text: '^Meu banco de horas$' }, direction: 'DOWN' } },
    text('^Meu banco de horas$'),
    {
      extendedWaitUntil: { visible: 'O saldo corresponde ao período consultado.', timeout: 30000 },
    },
    { takeScreenshot: os + '-balance' },
    ...(os === 'android' ? [{ pressKey: 'Back' }] : [text('Voltar para Minhas transações')]),
    { assertVisible: 'Volumes de movimentações registradas' },
  );
if (role === 'member-fresh')
  commands.push(
    wait('time-open-today'),
    tap('time-open-today'),
    { extendedWaitUntil: { visible: 'Em andamento desde.*', timeout: 30000 } },
    wait('time-tap'),
    { takeScreenshot: os + '-today-open-yesterday-still-open' },
  );
if (role === 'locked')
  commands.push(
    { extendedWaitUntil: { visible: 'Dia bloqueado para alterações', timeout: 30000 } },
    { takeScreenshot: os + '-locked-day' },
  );
if (role === 'manager')
  commands.push(
    wait('time-new'),
    { takeScreenshot: os + '-manager-list' },
    link(
      `gestao/ponto?section=new&operatorId=${os === 'ios' ? s.time.manager : s.time.managerAndroid}&date=${journeyDate}`,
    ),
    wait('time-prepare'),
    tap('time-prepare'),
    { scrollUntilVisible: { element: { text: 'Adicionar segmento' }, direction: 'DOWN' } },
    wait('time-add'),
    tap('time-add'),
    scroll('segment-in'),
    tap('segment-in'),
    { eraseText: 100 },
    { inputText: journeyDate.split('-').reverse().join('/') + ' 06:00' },
    os === 'android' ? { pressKey: 'Enter' } : { hideKeyboard: {} },
    scroll('segment-out'),
    tap('segment-out'),
    { inputText: journeyDate.split('-').reverse().join('/') + ' 07:00' },
    { takeScreenshot: os + '-manager-keyboard' },
    os === 'android' ? { pressKey: 'Enter' } : { hideKeyboard: {} },
    scroll('time-save-day'),
    wait('time-save-day'),
    tap('time-save-day'),
    { extendedWaitUntil: { visible: 'Registro salvo no servidor.', timeout: 30000 } },
    { extendedWaitUntil: { visible: { id: 'time-resource-ready' }, timeout: 60000 } },
    { takeScreenshot: os + '-manager-created' },
    { scrollUntilVisible: { element: { text: 'Editar segmento 1' }, direction: 'DOWN' } },
    wait('time-edit-segment-1'),
    tap('time-edit-segment-1'),
    scroll('segment-edit-in'),
    tap('segment-edit-in'),
    { eraseText: 100 },
    { inputText: journeyDate.split('-').reverse().join('/') + ' 05:45' },
    os === 'android' ? { pressKey: 'Enter' } : { hideKeyboard: {} },
    { scrollUntilVisible: { element: { text: 'Salvar segmento' }, direction: 'DOWN' } },
    wait('time-save-segment'),
    tap('time-save-segment'),
    {
      scrollUntilVisible: {
        element: { text: 'Registro salvo no servidor.' },
        direction: 'UP',
        timeout: 30000,
      },
    },
    scroll('segment-in'),
    {
      assertVisible: {
        id: 'segment-in',
        text: '.*' + journeyDate.split('-').reverse().join('/') + ' 05:45.*',
      },
    },
    {
      scrollUntilVisible: {
        element: { text: 'Horas: 1h 15min.*' },
        direction: 'UP',
        centerElement: true,
        timeout: 30000,
      },
    },
    { assertVisible: 'Horas: 1h 15min.*' },
    { takeScreenshot: os + '-manager-segment-edited' },
    { scrollUntilVisible: { element: { text: 'Adicionar segmento' }, direction: 'DOWN' } },
    wait('time-add'),
    tap('time-add'),
    scroll('segment-2-in'),
    tap('segment-2-in'),
    { eraseText: 100 },
    { inputText: journeyDate.split('-').reverse().join('/') + ' 08:00' },
    os === 'android' ? { pressKey: 'Enter' } : { hideKeyboard: {} },
    scroll('time-save-day'),
    wait('time-save-day'),
    tap('time-save-day'),
    {
      scrollUntilVisible: {
        element: { text: 'Registro salvo no servidor.' },
        direction: 'UP',
        timeout: 30000,
      },
    },
    scroll('segment-in'),
    {
      assertVisible: {
        id: 'segment-in',
        text: '.*' + journeyDate.split('-').reverse().join('/') + ' 05:45.*',
      },
    },
    scroll('segment-2-in'),
    {
      assertVisible: {
        id: 'segment-2-in',
        text: '.*' + journeyDate.split('-').reverse().join('/') + ' 08:00.*',
      },
    },
    { takeScreenshot: os + '-manager-snapshot-added' },
  );
await mkdir(path.dirname(output), { recursive: true });
await writeFile(
  output,
  JSON.stringify({ appId: 'com.example.lotero.development' }) +
    '\n---\n' +
    commands.map((c) => '- ' + JSON.stringify(c)).join('\n') +
    '\n',
  { mode: 0o600 },
);
console.log(`M9 ${os}/${role} journey written outside Git.`);
