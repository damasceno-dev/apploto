// Native continuation checkpoints permit supported API rereads between edit, remove and day deletion.
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { apiCall } from './native-stack.mjs';
const [os, statePath, output, phase] = process.argv.slice(2);
const root = fileURLToPath(new URL('../../', import.meta.url));
if (
  !['ios', 'android'].includes(os) ||
  ![
    'add',
    'remove',
    'status',
    'delete',
    'reports',
    'own',
    'selected-summary',
    'locked',
    'core',
  ].includes(phase) ||
  !path.isAbsolute(output ?? '') ||
  path.resolve(output).startsWith(root)
)
  throw new Error(
    'Use ios|android <owned-state> <outside-checkout.yaml> remove|delete|reports|locked|core',
  );
const s = JSON.parse(await readFile(statePath, 'utf8'));
if (!s.owned || !s.time || new URL(s.apiUrl).hostname !== '127.0.0.1')
  throw new Error('Owned M9 state required.');
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
const date = s.time.journeyDates[os];
const row = (
  await call(
    'GET',
    '/timeentry?' + new URLSearchParams({ OperatorId: operatorId, DateFrom: date, DateTo: date }),
    undefined,
    token,
  )
).items[0];
if (!row && !['core', 'locked'].includes(phase))
  throw new Error('A native-created M9 day is required.');
const tap = (id) => ({ tapOn: { id } });
const text = (text) => ({ tapOn: { text } });
const wait = (id) => ({ extendedWaitUntil: { visible: { id, enabled: true }, timeout: 60000 } });
const scroll = (value, direction = 'DOWN') => ({
  scrollUntilVisible: { element: { text: value }, direction, centerElement: true, timeout: 30000 },
});
const link = (value) => ({
  openLink: 'lotero://' + value + (value.includes('?') ? '&' : '?') + 'branchId=' + s.branches.alfa,
});
const back = (label) =>
  os === 'android'
    ? { pressKey: 'Back' }
    : {
        runFlow: { commands: [scroll('Voltar para ' + label, 'UP'), text('Voltar para ' + label)] },
      };
const saved = scroll('Registro salvo no servidor.', 'UP');
const commands = [{ launchApp: { stopApp: true } }, wait('session-menu')];
if (phase === 'add')
  commands.push(
    link('gestao/ponto/' + row.id),
    { scrollUntilVisible: { element: { id: 'time-save-day' }, direction: 'DOWN', timeout: 30000 } },
    wait('time-save-day'),
    scroll('Adicionar segmento'),
    wait('time-add'),
    tap('time-add'),
    { scrollUntilVisible: { element: { id: 'segment-2-in' }, direction: 'DOWN', timeout: 30000 } },
    tap('segment-2-in'),
    { eraseText: 100 },
    { inputText: date.split('-').reverse().join('/') + ' 08:00' },
    os === 'android' ? { pressKey: 'Enter' } : { hideKeyboard: {} },
    scroll('Salvar dia'),
    { scrollUntilVisible: { element: { id: 'time-save-day' }, direction: 'DOWN', timeout: 30000 } },
    wait('time-save-day'),
    tap('time-save-day'),
    saved,
    { takeScreenshot: os + '-snapshot-with-retained-id' },
  );
if (phase === 'remove')
  commands.push(
    link('gestao/ponto/' + row.id),
    { scrollUntilVisible: { element: { id: 'time-save-day' }, direction: 'DOWN', timeout: 30000 } },
    wait('time-save-day'),
    scroll('Remover segmento 2'),
    text('Remover segmento 2'),
    text('^Remover$'),
    saved,
    { takeScreenshot: os + '-segment-removed' },
    scroll('Remover segmento 1'),
    text('Remover segmento 1'),
    text('^Remover$'),
    saved,
    { scrollUntilVisible: { element: { id: 'time-status' }, direction: 'UP', timeout: 30000 } },
    tap('time-status'),
    tap('time-status-option-DayOff'),
    scroll('Salvar dia'),
    { scrollUntilVisible: { element: { id: 'time-save-day' }, direction: 'DOWN', timeout: 30000 } },
    wait('time-save-day'),
    tap('time-save-day'),
    saved,
    { takeScreenshot: os + '-status-day-off' },
  );
if (phase === 'status')
  commands.push(
    link('gestao/ponto/' + row.id),
    { scrollUntilVisible: { element: { id: 'time-status' }, direction: 'DOWN', timeout: 30000 } },
    wait('time-status'),
    tap('time-status'),
    tap('time-status-option-DayOff'),
    scroll('Salvar dia'),
    wait('time-save-day'),
    tap('time-save-day'),
    saved,
    { takeScreenshot: os + '-status-day-off' },
  );
if (phase === 'delete')
  commands.push(
    link('gestao/ponto/' + row.id),
    { scrollUntilVisible: { element: { id: 'time-save-day' }, direction: 'DOWN', timeout: 30000 } },
    wait('time-save-day'),
    scroll('Remover registro do dia'),
    text('Remover registro do dia'),
    { takeScreenshot: os + '-remove-day-confirmation' },
    text('^Remover$'),
    scroll('Registro removido. O dia passa a contar como sem registro.', 'UP'),
    { takeScreenshot: os + '-day-removed' },
  );
if (phase === 'reports')
  commands.push(
    link('gestao/ponto/banco-de-horas?' + new URLSearchParams({ from: date, to: date })),
    { extendedWaitUntil: { visible: 'Banco de horas da equipe', timeout: 30000 } },
    scroll('Ver detalhes de ' + row.operatorName),
    text('Ver detalhes de ' + row.operatorName),
    { extendedWaitUntil: { visible: 'Banco de horas no período', timeout: 30000 } },
    scroll('Abrir ponto de ' + date.split('-').reverse().join('/')),
    text('Abrir ponto de ' + date.split('-').reverse().join('/')),
    {
      scrollUntilVisible: {
        element: { id: 'time-open-' + row.id },
        direction: 'DOWN',
        timeout: 30000,
      },
    },
    wait('time-open-' + row.id),
    tap('time-open-' + row.id),
    { scrollUntilVisible: { element: { id: 'time-save-day' }, direction: 'DOWN', timeout: 30000 } },
    wait('time-save-day'),
    back('Gestão de ponto'),
    {
      scrollUntilVisible: {
        element: { id: 'time-open-' + row.id },
        direction: 'DOWN',
        timeout: 30000,
      },
    },
    wait('time-open-' + row.id),
    back('Banco de horas'),
    { extendedWaitUntil: { visible: 'Banco de horas no período', timeout: 30000 } },
    back('Banco de horas'),
    { extendedWaitUntil: { visible: 'Banco de horas da equipe', timeout: 30000 } },
    { takeScreenshot: os + '-nested-team-return' },
  );
if (phase === 'own')
  commands.push(
    link('turno/banco-de-horas?' + new URLSearchParams({ from: date, to: date })),
    { extendedWaitUntil: { visible: 'Banco de horas no período', timeout: 30000 } },
    scroll('Abrir ponto de ' + date.split('-').reverse().join('/')),
    text('Abrir ponto de ' + date.split('-').reverse().join('/')),
    {
      scrollUntilVisible: {
        element: { id: 'time-open-' + row.id },
        direction: 'DOWN',
        timeout: 30000,
      },
    },
    wait('time-open-' + row.id),
    back('Meu banco de horas'),
    { extendedWaitUntil: { visible: 'Banco de horas no período', timeout: 30000 } },
    { takeScreenshot: os + '-manager-own-return' },
  );
if (phase === 'selected-summary')
  commands.push(
    link(
      'gestao/operadores/' +
        operatorId +
        '/transacoes?' +
        new URLSearchParams({ from: date, to: date }),
    ),
    { extendedWaitUntil: { visible: 'Volumes de movimentações registradas', timeout: 30000 } },
    scroll('Ver transações'),
    text('Ver transações'),
    { extendedWaitUntil: { visible: 'Os dados e as ações de transações.*', timeout: 30000 } },
    back('Transações do operador'),
    { extendedWaitUntil: { visible: 'Volumes de movimentações registradas', timeout: 30000 } },
    { takeScreenshot: os + '-selected-summary-return' },
  );
if (phase === 'locked')
  commands.push(
    link('gestao/ponto/' + s.time.entries.locked.id),
    { extendedWaitUntil: { visible: 'Dia bloqueado para alterações', timeout: 30000 } },
    { takeScreenshot: os + '-locked-readonly' },
    tap('session-menu'),
    text('^Escuro$'),
    text('^Fechar$'),
    { takeScreenshot: os + '-locked-dark' },
  );
if (phase === 'core')
  commands.push(
    tap('session-menu'),
    scroll('Sair'),
    tap('logout'),
    {
      runFlow: {
        when: { visible: 'Sair da conta\\?' },
        commands: [text('Sair e manter os dados')],
      },
    },
    wait('login-email'),
    scroll('Verificação técnica \\(DEV\\)'),
    text('Verificação técnica \\(DEV\\)'),
    {
      scrollUntilVisible: {
        element: { id: 'native-core-measure' },
        direction: 'DOWN',
        timeout: 30000,
      },
    },
    wait('native-core-measure'),
    tap('native-core-measure'),
    { extendedWaitUntil: { visible: { id: 'native-core-summary' }, timeout: 30000 } },
    { takeScreenshot: os + '-core107-native' },
  );
await writeFile(
  output,
  JSON.stringify({ appId: 'com.example.lotero.development' }) +
    '\n---\n' +
    commands.map((c) => '- ' + JSON.stringify(c)).join('\n') +
    '\n',
  { mode: 0o600 },
);
console.log('M9 native continuation written: ' + os + '/' + phase);
