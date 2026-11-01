// Writes native-touch M10 journeys outside Git. The API is the owned local stack; no token
// injection or JS execution. Each journey is a separate file so one surface fails in isolation.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const journeys = [
  'member-clients',
  'admin-operators',
  'admin-clients',
  'admin-products',
  'admin-holidays',
  'admin-settings',
  'admin-settings-stale',
];
const [os, statePath, output, journey] = process.argv.slice(2);
const root = fileURLToPath(new URL('../../', import.meta.url));
if (
  !['ios', 'android'].includes(os) ||
  !journeys.includes(journey) ||
  !path.isAbsolute(output ?? '') ||
  path.resolve(output).startsWith(root)
)
  throw new Error(`Use ios|android <owned-state> <outside-checkout.yaml> ${journeys.join('|')}`);
const s = JSON.parse(await readFile(statePath, 'utf8'));
const f = s.admin?.os?.[os];
if (!s.owned || !f || new URL(s.apiUrl).hostname !== '127.0.0.1')
  throw new Error('Owned local M10 fixtures required.');
const label = os === 'ios' ? 'iOS' : 'Android';
const esc = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const tap = (id) => ({ tapOn: { id } });
const text = (value) => ({ tapOn: { text: esc(value) } });
const wait = (id) => ({ extendedWaitUntil: { visible: { id, enabled: true }, timeout: 60000 } });
const see = (value, timeout = 30000) => ({
  extendedWaitUntil: { visible: '.*' + esc(value) + '.*', timeout },
});
const gone = (value) => ({
  extendedWaitUntil: { notVisible: '.*' + esc(value) + '.*', timeout: 30000 },
});
// Bounded swipes until the element is on screen: scrollUntilVisible can miss an element that is
// already fully visible at the end of a long list.
const swipes = (id, direction, times) => ({
  repeat: {
    while: { notVisible: { id } },
    times,
    commands: [
      {
        swipe: {
          start: direction === 'DOWN' ? '50%, 60%' : '50%, 40%',
          end: direction === 'DOWN' ? '50%, 40%' : '50%, 60%',
          duration: 400,
        },
      },
    ],
  },
});
// Searches in the given direction first, then the other way, so list growth cannot strand it.
const scroll = (id, direction = 'DOWN') => ({
  runFlow: {
    commands: [
      // Let an opening dialog finish its animation before deciding whether to swipe.
      { waitForAnimationToEnd: { timeout: 3000 } },
      swipes(id, direction, 15),
      swipes(id, direction === 'DOWN' ? 'UP' : 'DOWN', 30),
    ],
  },
});
// Page rows: return to the top (the header context line is visible), then search downward only,
// so a row always enters from the bottom instead of stopping under the top bar.
const row = (id) => ({
  runFlow: {
    commands: [
      { waitForAnimationToEnd: { timeout: 3000 } },
      {
        repeat: {
          while: { notVisible: '.*Lotérica Alfa E2E · .*' },
          times: 40,
          commands: [{ swipe: { start: '50%, 30%', end: '50%, 80%', duration: 300 } }],
        },
      },
      swipes(id, 'DOWN', 40),
      // Detected rows can still sit on the bottom edge; lift them into the middle band.
      { swipe: { start: '50%, 70%', end: '50%, 45%', duration: 500 } },
      { waitForAnimationToEnd: { timeout: 3000 } },
    ],
  },
});
const lift = [
  { swipe: { start: '50%, 70%', end: '50%, 45%', duration: 500 } },
  { waitForAnimationToEnd: { timeout: 3000 } },
];
const scrollText = (value, direction = 'DOWN') => ({
  scrollUntilVisible: {
    element: { text: esc(value) },
    direction,
    timeout: 30000,
    visibilityPercentage: 60,
  },
});
const optional = (visible, commands) => ({ runFlow: { when: { visible }, commands } });
const close = os === 'android' ? { pressKey: 'Enter' } : { hideKeyboard: {} };
// iOS dialogs keep the keyboard while the next field or the pinned footer is tapped; Android
// closes it only through the runner's observed-IME check.
const type = (id, value, erase = true) => [
  scroll(id),
  tap(id),
  { waitForAnimationToEnd: { timeout: 3000 } },
  ...(erase ? [{ eraseText: 60 }] : []),
  { inputText: value },
  ...(os === 'android' ? [close] : []),
];
// Masked identifier fields receive one digit per command, like a person typing on the keypad.
const digits = (id, value) => [
  scroll(id),
  tap(id),
  { waitForAnimationToEnd: { timeout: 3000 } },
  ...[...value].map((digit) => ({ inputText: digit })),
  ...(os === 'android' ? [close] : []),
];
// A write refused by an expired token is never replayed by the app; the person presses Save again.
// iOS: Maestro reports footer bounds from before the keyboard appeared, so a footer tap could
// land on a key. A short downward drag inside the dialog's scroll view (keyboardDismissMode
// interactive) dismisses it first; pages use the same interactive dismissal.
const settle = (where) =>
  os === 'ios' && where
    ? // Number pads have no return key, so hideKeyboard cannot close them on a page either.
      [
        { swipe: { start: '50%, 30%', end: '50%, 92%', duration: 700 } },
        { waitForAnimationToEnd: { timeout: 3000 } },
      ]
    : [];
const save = (id, where = 'dialog') => [
  ...settle(where),
  tap(id),
  { waitForAnimationToEnd: { timeout: 3000 } },
  optional('.*Token expirado.*', [tap(id)]),
];
const shot = (name) => ({ takeScreenshot: `${os}-m10-${name}` });
const link = (url) => ({
  openLink: {
    link: 'lotero://' + url + (url.includes('?') ? '&' : '?') + 'branchId=' + s.branches.alfa,
  },
});
const login = (user) => [
  { launchApp: { stopApp: true } },
  { extendedWaitUntil: { visible: { id: 'login-email|session-menu' }, timeout: 60000 } },
  optional({ id: 'session-menu' }, [
    wait('navigation-menu'),
    { waitForAnimationToEnd: { timeout: 5000 } },
    tap('session-menu'),
    { waitForAnimationToEnd: { timeout: 5000 } },
    optional({ id: 'logout' }, []),
    { runFlow: { when: { notVisible: { id: 'logout' } }, commands: [tap('session-menu')] } },
    scroll('logout'),
    tap('logout'),
    optional('Sair da conta\\?', [text('Sair e manter os dados')]),
  ]),
  wait('login-email'),
  tap('login-email'),
  ...(os === 'android'
    ? [{ longPressOn: { id: 'login-email' } }, { eraseText: 1 }]
    : [{ eraseText: 100 }]),
  { inputText: user.email },
  tap('login-password'),
  { inputText: user.password },
  close,
  optional({ id: 'login-submit' }, [tap('login-submit')]),
  optional('Save Password\\?', [text('Not Now')]),
  wait('navigation-menu'),
];
// Continuation journeys reuse the restored secure session; a relaunch clears any stale dialog.
const relaunch = [{ launchApp: { stopApp: true } }, wait('navigation-menu')];
const back = (title) => (os === 'android' ? { pressKey: 'Back' } : text(`Voltar para ${title}`));
const commands = [];
if (journey === 'member-clients') {
  const name = `Cliente Maestro ${label}${process.env.M10_SUFFIX ?? ''}`;
  commands.push(
    ...login(s.users[f.member.key]),
    link('clientes'),
    wait('client-new'),
    see('O extrato de fiado exige uma conta fiado'),
    { assertNotVisible: 'Desativar Cliente CPF M10' },
    shot('member-clients'),
    tap('client-new'),
    ...type('client-name', name, false),
    ...digits('client-phone', '61987654321'),
    ...digits('client-cpf', os === 'ios' ? (process.env.M10_CPF ?? '11144477735') : '39053344705'),
    shot('member-client-keyboard'),
    ...save('client-save'),
    see(`Cliente ${name} salvo no servidor.`),
    wait('client-new'),
    tap('client-new'),
    ...type('client-name', `Duplicado ${label}`, false),
    ...digits('client-phone', '6133334444'),
    ...digits('client-cpf', '52998224725'),
    ...save('client-save'),
    see('Já existe um cliente ativo com este CPF nesta filial'),
    shot('member-cpf-conflict'),
    // The first tap may only dismiss the keyboard; repeat while the dialog is still open.
    ...settle('dialog'),
    text('Cancelar'),
    { waitForAnimationToEnd: { timeout: 3000 } },
    optional({ id: 'client-save' }, [text('Cancelar')]),
    scroll('client-search', 'UP'),
    ...type('client-search', 'maestro', false),
    {
      extendedWaitUntil: { visible: { id: 'client-count', text: '.*encontrad.*' }, timeout: 30000 },
    },
    shot('member-search'),
  );
}
if (journey === 'admin-operators') {
  const spare = s.users[f.spare.key];
  commands.push(
    ...login(s.users[f.admin.key]),
    link('gestao/operadores'),
    wait('operator-new'),
    scrollText('você'),
    shot('operators'),
    row(`operator-edit-${f.unlinked}`),
    tap(`operator-edit-${f.unlinked}`),
    wait('operator-login'),
    tap('operator-login'),
    text(`${f.spare.name} · ${spare.email} · Membro`),
    shot('operator-link-editor'),
    ...save('operator-save'),
    see('Operador salvo no servidor.'),
    optional({ id: 'operator-save' }, [text('Cancelar')]),
    row(`operator-login-${f.unlinked}`),
    {
      assertVisible: {
        id: `operator-login-${f.unlinked}`,
        text: esc(`Login: ${f.spare.name} · ${spare.email} · Membro`),
      },
    },
    // Unlinking the caller's own operator re-resolves the shell's self-context.
    row(`operator-edit-${f.admin.operatorId}`),
    tap(`operator-edit-${f.admin.operatorId}`),
    wait('operator-unlink'),
    tap('operator-unlink'),
    ...save('operator-save'),
    see('Remover o login deste operador?'),
    shot('operator-unlink-confirmation'),
    text('Remover login e salvar'),
    wait('operator-new'),
    scroll('operator-self-receipt', 'UP'),
    optional('Entendi a mudança de acesso', [text('Entendi a mudança de acesso')]),
    wait('operator-new'),
    row(`operator-login-${f.admin.operatorId}`),
    { assertVisible: { id: `operator-login-${f.admin.operatorId}`, text: 'Login: Sem login' } },
    { assertNotVisible: 'você' },
    shot('operator-unlinked'),
    row(`operator-edit-${f.admin.operatorId}`),
    tap(`operator-edit-${f.admin.operatorId}`),
    wait('operator-login'),
    tap('operator-login'),
    text(`Pessoa ${f.admin.key} M10 · ${s.users[f.admin.key].email} · Administrador`),
    ...save('operator-save'),
    wait('operator-new'),
    scroll('operator-self-receipt', 'UP'),
    optional('Entendi a mudança de acesso', [text('Entendi a mudança de acesso')]),
    wait('operator-new'),
    scrollText('você'),
    row(`operator-summary-${f.member.operatorId}`),
    tap(`operator-summary-${f.member.operatorId}`),
    see('Transações do operador', 60000),
    shot('operator-summary'),
    back('Operadores'),
    wait('operator-new'),
    shot('operator-returned'),
  );
}
if (journey === 'admin-clients') {
  commands.push(
    ...relaunch,
    link('clientes'),
    wait('client-new'),
    row(`client-deactivate-${f.removableClient}`),
    tap(`client-deactivate-${f.removableClient}`),
    see('O cliente sai da lista'),
    shot('client-deactivate-confirmation'),
    text('Desativar cliente'),
    gone(`Cliente desativar M10 ${label}`),
  );
}
if (journey === 'admin-products') {
  const suffix = process.env.M10_SUFFIX ?? '';
  commands.push(
    ...relaunch,
    link('gestao/produtos'),
    wait('product-new'),
    tap('product-new'),
    ...type('product-name', 'Raspadinha M10', false),
    ...save('product-save'),
    see('Já existe um produto ativo com este nome nesta filial'),
    shot('product-conflict'),
    ...type('product-name', `Produto novo ${label}${suffix}`),
    ...save('product-save'),
    {
      scrollUntilVisible: {
        element: { text: `.*\\. ${esc(`Produto novo ${label}${suffix}`)}` },
        direction: 'DOWN',
        timeout: 60000,
      },
    },
    row(`product-edit-${f.product}`),
    tap(`product-edit-${f.product}`),
    ...type('product-name', `Produto M10 ${label} editado${suffix}`),
    ...save('product-save'),
    see('Produto salvo no servidor.'),
    ...settle('dialog'),
    optional({ id: 'product-save' }, [text('Cancelar')]),
    row(`product-up-${f.product}`),
    tap(`product-up-${f.product}`),
    see('Ordem de contagem salva no servidor.'),
    shot('product-reordered'),
    scrollText('Produto do sistema', 'UP'),
    shot('products-reserved'),
  );
}
if (journey === 'admin-holidays') {
  const year = Number(s.admin.today.slice(0, 4)) + (os === 'ios' ? 0 : 1);
  commands.push(
    ...relaunch,
    link('gestao/feriados'),
    wait('holiday-import'),
    ...(os === 'android' ? [tap('holiday-year-next'), see(`Ano: ${year}`)] : []),
    tap('holiday-add'),
    ...type('holiday-date', `${process.env.M10_HOLIDAY_DAY ?? '12'}/06/${year}`, false),
    ...type('holiday-description', `Feriado manual ${label}`, false),
    ...save('holiday-batch-save'),
    see('1 feriado salvo no servidor.'),
    shot('holiday-manual'),
    text('Fechar'),
    wait('holiday-import'),
    tap('holiday-import'),
    tap('holiday-source-Composite'),
    tap('holiday-preview'),
    { extendedWaitUntil: { visible: { id: 'holiday-preview-list' }, timeout: 60000 } },
    shot('holiday-preview'),
    tap('holiday-import-confirm'),
    see('Importar feriados?'),
    text('Importar feriados'),
    { extendedWaitUntil: { visible: { id: 'holiday-import-result' }, timeout: 60000 } },
    see('importados · '),
    shot('holiday-import-result'),
    scroll('holiday-new-preview'),
    tap('holiday-new-preview'),
    scroll('holiday-source-BrasilApi', 'UP'),
    tap('holiday-source-BrasilApi'),
    tap('holiday-preview'),
    see('Não foi possível consultar a fonte externa de feriados', 60000),
    shot('holiday-outage'),
    text('Fechar'),
  );
}
if (journey === 'admin-settings') {
  commands.push(
    ...relaunch,
    link('gestao/configuracoes'),
    { extendedWaitUntil: { visible: '.*Data de bloqueio.*', timeout: 60000 } },
    scroll('setting-effect'),
    scroll('setting-save'),
    ...lift,
    { assertVisible: { id: 'setting-save', enabled: false } },
    see('Nenhuma alteração para salvar.'),
    scroll('setting-lock-date', 'UP'),
    { assertVisible: { id: 'setting-lock-date', text: '.*Nenhum mês bloqueado.*' } },
    shot('settings'),
    ...type('setting-lunchDeductionOver4H-minutes', os === 'ios' ? '18' : '21'),
    ...settle('page'),
    scroll('setting-save'),
    ...lift,
    tap('setting-save'),
    see('Confirmar alteração das regras de jornada?'),
    shot('settings-lunch-confirmation'),
    ...save('setting-confirm', null),
    see('Configurações salvas no servidor.'),
    ...type('setting-dailyTargetHours-hours', os === 'ios' ? '8' : '7'),
    ...type('setting-dailyTargetHours-minutes', os === 'ios' ? '00' : '30'),
    ...settle('page'),
    scroll('setting-save'),
    ...lift,
    tap('setting-save'),
    see('Confirmar alteração de meta?'),
    shot('settings-target-confirmation'),
    ...save('setting-confirm', null),
    see('Configurações salvas no servidor.'),
    // Leave a typed candidate for the stale-precondition continuation.
    ...type('setting-lunchDeductionOver6H-minutes', os === 'ios' ? '03' : '06'),
  );
}
if (journey === 'admin-settings-stale') {
  commands.push(
    scroll('setting-save'),
    ...settle('page'),
    scroll('setting-save'),
    ...lift,
    tap('setting-save'),
    see('Confirmar alteração das regras de jornada?'),
    ...save('setting-confirm', null),
    see('As configurações foram alteradas em outro lugar.'),
    see('As configurações foram alteradas por outra operação; recarregue antes de salvar'),
    shot('settings-stale'),
    scroll('setting-reload'),
    ...lift,
    tap('setting-reload'),
    scroll('setting-adopt'),
    ...lift,
    shot('settings-fresh'),
    tap('setting-adopt'),
    scroll('setting-save'),
    ...settle('page'),
    scroll('setting-save'),
    ...lift,
    tap('setting-save'),
    see('Confirmar alteração das regras de jornada?'),
    ...save('setting-confirm', null),
    see('Configurações salvas no servidor.'),
    shot('settings-reconciled'),
  );
}
await mkdir(path.dirname(output), { recursive: true });
await writeFile(
  output,
  JSON.stringify({ appId: 'com.example.lotero.development' }) +
    '\n---\n' +
    commands.map((c) => '- ' + JSON.stringify(c)).join('\n') +
    '\n',
  { mode: 0o600 },
);
console.log(`M10 ${os}/${journey} journey written outside Git.`);
