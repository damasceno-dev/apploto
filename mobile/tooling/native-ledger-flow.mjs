// Writes native-touch Maestro journeys for Mobile M6 (Transactions ledger, fast entry, inline
// opening, Draft blocker round trip, fiado sale and repayments, Depósito Dinheiro, cheque plan and
// cheque lifecycle) outside Git. Ids come from read-only lookups on the owned local API; no token
// injection or app JS execution. Helpers mirror native-day-flow.mjs.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { apiCall } from './native-stack.mjs';
const journeys = [
  'ledger-open',
  'ledger-draft',
  'ledger-blocker',
  'ledger-fiado',
  'ledger-pay',
  'ledger-safe',
  'ledger-cheque',
  'ledger-convert',
  'ledger-cheque-ops',
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
const d = s.day;
const o = d?.os?.[os];
if (!s.owned || !o || !s.ledger || new URL(s.apiUrl).hostname !== '127.0.0.1')
  throw new Error('Owned local M5 + M6 fixtures required.');
const call = apiCall(s.apiUrl);
const identity = (await call('POST', '/user/login', s.users.ana)).responseToken.token;
const token = (await call('POST', '/branch/session', { branchId: s.branches.alfa }, identity))
  .token;
const today = (await call('GET', '/branch/current', undefined, token)).branchLocalDate;
const sessions = (
  await call(
    'GET',
    `/session?AccountId=${o.cashbox}&DateFrom=${today}&DateTo=${today}&PageSize=50`,
    undefined,
    token,
  )
).items;
const activeSession = sessions.find((row) => row.custody.custodyState === 'Active')?.id;
const cash = d.products.find((p) => p.name === 'Dinheiro').id;
const label = os === 'ios' ? 'iOS' : 'Android';
const due = (months) => {
  const [y, m] = today.split('-').map(Number);
  const total = y * 12 + (m - 1) + months;
  return `10/${String((total % 12) + 1).padStart(2, '0')}/${Math.floor(total / 12)}`;
};
const esc = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const tap = (id) => ({ tapOn: { id } });
const text = (value) => ({ tapOn: { text: esc(value) } });
const wait = (id, timeout = 60000) => ({
  extendedWaitUntil: { visible: { id, enabled: true }, timeout },
});
const see = (value, timeout = 30000) => ({
  extendedWaitUntil: { visible: '.*' + esc(value) + '.*', timeout },
});
const swipes = (target, direction, times) => ({
  repeat: {
    while: { notVisible: target },
    times,
    commands: [
      {
        swipe: {
          start: direction === 'DOWN' ? '50%, 65%' : '50%, 35%',
          end: direction === 'DOWN' ? '50%, 40%' : '50%, 60%',
          duration: 400,
        },
      },
    ],
  },
});
const find = (target, budget = 25) => ({
  runFlow: {
    commands: [
      { waitForAnimationToEnd: { timeout: 3000 } },
      swipes(target, 'DOWN', budget),
      swipes(target, 'UP', budget + 15),
    ],
  },
});
const center = (id, optional = false) => ({
  scrollUntilVisible: {
    element: { id },
    direction: 'DOWN',
    timeout: optional ? 8000 : 60000,
    centerElement: true,
    visibilityPercentage: 100,
    ...(optional ? { optional: true } : {}),
  },
});
const centerUp = (id) => ({
  scrollUntilVisible: {
    element: { id },
    direction: 'UP',
    timeout: 6000,
    centerElement: true,
    visibilityPercentage: 100,
    optional: true,
  },
});
/** Found, then centred both ways, so a target clipped under the top bar is never tapped blind. */
const reach = (id, budget) => ({
  // Android's sticky header does not overlap the scroll area, and centring scrolls can lose the
  // target there; the bidirectional search alone is enough.
  runFlow: {
    commands:
      os === 'android'
        ? [
            // UiAutomator lists off-screen React Native nodes, so search by on-screen visibility.
            {
              scrollUntilVisible: {
                element: { id },
                direction: 'DOWN',
                timeout: 90000,
                speed: 70,
                visibilityPercentage: 100,
                optional: true,
              },
            },
            {
              scrollUntilVisible: {
                element: { id },
                direction: 'UP',
                timeout: 120000,
                speed: 10,
                visibilityPercentage: 100,
                optional: true,
              },
            },
          ]
        : [find({ id }, budget), centerUp(id), center(id, true)],
  },
});
/** Brings text into view and asserts it (Android searches by real on-screen visibility). */
const scrollTo = (value) => {
  const text = '.*' + esc(value) + '.*';
  return {
    runFlow: {
      commands:
        os === 'android'
          ? [
              {
                scrollUntilVisible: {
                  element: { text },
                  direction: 'DOWN',
                  timeout: 90000,
                  speed: 70,
                  optional: true,
                },
              },
              {
                scrollUntilVisible: {
                  element: { text },
                  direction: 'UP',
                  timeout: 120000,
                  speed: 10,
                },
              },
            ]
          : [find(text), { extendedWaitUntil: { visible: text, timeout: 10000 } }],
    },
  };
};
const optional = (visible, commands) => ({ runFlow: { when: { visible }, commands } });
// iOS number/decimal pads have no return key: drag the content to dismiss (keyboardDismissMode).
const hide =
  os === 'android'
    ? {
        // keyboardDismissMode on-drag: a slow short drag closes the keypad without navigating.
        runFlow: {
          commands: [
            { swipe: { start: '50%, 40%', end: '50%, 46%', duration: 900 } },
            { waitForAnimationToEnd: { timeout: 3000 } },
          ],
        },
      }
    : {
        runFlow: {
          commands: [
            { swipe: { start: '50%, 30%', end: '50%, 92%', duration: 700 } },
            { waitForAnimationToEnd: { timeout: 3000 } },
          ],
        },
      };
const type = (id, value) => [
  reach(id),
  tap(id),
  { waitForAnimationToEnd: { timeout: 3000 } },
  { eraseText: 30 },
  { inputText: value },
];
// Unlike the M5 helper, no keypad-dismiss drag here: fill() already closed the keypad, and that drag
// would toggle a just-centred checkbox (Android) or scroll the target away again (iOS).
const press = (id) => [
  { waitForAnimationToEnd: { timeout: 5000 } },
  reach(id),
  // Let a scroll fling stop first: a tap on a moving list only stops it (Android).
  { waitForAnimationToEnd: { timeout: 3000 } },
  { waitForAnimationToEnd: { timeout: 3000 } },
  tap(id),
  { waitForAnimationToEnd: { timeout: 3000 } },
];
const choose = (select, value) => [
  reach(select),
  { waitForAnimationToEnd: { timeout: 3000 } },
  tap(select),
  // The option itself, not the sheet wrapper (which a platform may flatten away).
  { extendedWaitUntil: { visible: { id: `${select}-option-${value}` }, timeout: 10000 } },
  { waitForAnimationToEnd: { timeout: 3000 } },
  tap(`${select}-option-${value}`),
  { waitForAnimationToEnd: { timeout: 3000 } },
];
const shot = (name) => ({ takeScreenshot: `${os}-m6-${name}` });

const link = (url) => ({
  openLink: {
    link: 'lotero://' + url + (url.includes('?') ? '&' : '?') + 'branchId=' + s.branches.alfa,
  },
});
const login = (user, home = 'cockpit') => [
  { launchApp: { stopApp: true } },
  { extendedWaitUntil: { visible: { id: 'login-email|session-menu' }, timeout: 60000 } },
  { waitForAnimationToEnd: { timeout: 5000 } },
  { extendedWaitUntil: { visible: { id: 'login-email|session-menu' }, timeout: 60000 } },
  optional({ id: 'session-menu' }, [
    wait('navigation-menu'),
    { waitForAnimationToEnd: { timeout: 5000 } },
    tap('session-menu'),
    { waitForAnimationToEnd: { timeout: 5000 } },
    { runFlow: { when: { notVisible: { id: 'logout' } }, commands: [tap('session-menu')] } },
    find({ id: 'logout' }),
    tap('logout'),
    optional('Sair da conta\\?', [text('Sair e manter os dados')]),
  ]),
  { extendedWaitUntil: { visible: { id: 'login-email|session-menu' }, timeout: 60000 } },
  optional({ id: 'session-menu' }, [
    tap('session-menu'),
    { waitForAnimationToEnd: { timeout: 5000 } },
    find({ id: 'logout' }),
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
  os === 'android' ? { pressKey: 'Enter' } : { hideKeyboard: {} },
  optional('Save Password\\?', [text('Not Now')]),
  optional({ id: 'login-submit' }, [tap('login-submit')]),
  optional('Save Password\\?', [text('Not Now')]),
  wait('navigation-menu'),
  ...(home === 'cockpit'
    ? [
        // A validated previous destination may be restored after login; open Hoje explicitly.
        link('turno/hoje'),
        // Leaf ids: wrapper views can be flattened out of the accessibility tree.
        {
          extendedWaitUntil: {
            visible: { id: 'cockpit-context|cockpit-choose-.*' },
            timeout: 90000,
          },
        },
        // Several cashboxes: the per-device, per-branch-day choice (made once per day).
        optional({ id: 'cockpit-choose' }, [
          shot('cockpit-choose'),
          tap(`cockpit-choose-${o.cashbox}`),
        ]),
        wait('cockpit-context', 60000),
      ]
    : []),
];
/**
 * iOS (Maestro 2.11 on this simulator) reports off-screen React Native nodes as visible, so its
 * scroll searches skip and taps land off-screen. Bottom-of-form controls are reached there by
 * scrolling to the end with fixed swipes and tapping the visible label; Android keeps real bounds.
 */
const toBottom = {
  repeat: {
    times: 8,
    commands: [{ swipe: { start: '50%, 85%', end: '50%, 15%', duration: 900 } }],
  },
};
const atBottom = (id, label) => {
  const tapIt =
    os === 'ios'
      ? [
          toBottom,
          // Late content (the server preview) can grow the form after the first pass.
          { waitForAnimationToEnd: { timeout: 4000 } },
          toBottom,
          { waitForAnimationToEnd: { timeout: 4000 } },
          { tapOn: { text: label } },
          { waitForAnimationToEnd: { timeout: 3000 } },
        ]
      : press(id);
  // A write tapped while the session re-verifies is paused and NOT sent; tapping again is safe.
  // On Android a missed tap leaves the same enabled button on screen: tap it once more (a busy
  // button ignores taps and the durable intent never sends a second operation).
  const again =
    os === 'android'
      ? [
          { waitForAnimationToEnd: { timeout: 3000 } },
          { waitForAnimationToEnd: { timeout: 3000 } },
          reach(id),
          { waitForAnimationToEnd: { timeout: 3000 } },
          { runFlow: { when: { visible: { id, enabled: true } }, commands: [tap(id)] } },
        ]
      : [];
  return [...tapIt, ...again, optional('.*Envios pausados.*', tapIt)];
};
const check = (id, label) =>
  os === 'ios'
    ? [
        toBottom,
        { waitForAnimationToEnd: { timeout: 4000 } },
        { tapOn: { text: esc(label) + '.*' } },
        { waitForAnimationToEnd: { timeout: 3000 } },
      ]
    : press(id);
/** Searchable selects: narrow the sheet with its search box so the option is on screen. */
const search = (select, query, value) => [
  reach(select),
  { waitForAnimationToEnd: { timeout: 3000 } },
  tap(select),
  wait(`${select}-search`, 15000),
  tap(`${select}-search`),
  { inputText: query },
  { extendedWaitUntil: { visible: { id: `${select}-option-${value}` }, timeout: 15000 } },
  tap(`${select}-option-${value}`),
  { waitForAnimationToEnd: { timeout: 3000 } },
];
/** Below-the-fold text checks run where visibility is real (Android); iOS relies on shots + API. */
const below = (...steps) => (os === 'android' ? steps : []);
/** Inside a bottom sheet: no search drags (a downward drag dismisses the sheet). */
const inSheet = (id) => [
  { waitForAnimationToEnd: { timeout: 4000 } },
  tap(id),
  { waitForAnimationToEnd: { timeout: 4000 } },
];
const sheetChoose = (select, value) => [
  ...inSheet(select),
  { extendedWaitUntil: { visible: { id: `${select}-option-${value}` }, timeout: 10000 } },
  tap(`${select}-option-${value}`),
  { waitForAnimationToEnd: { timeout: 3000 } },
];
const commands = [];
if (journey === 'ledger-cheque-ops') {
  // One concrete cheque still in the cashbox: custody → deposit → return → re-present →
  // conversion, each from its exact id and current version (action buttons end the page).
  const rows = (
    await call('GET', `/transaction?AccountId=${s.ledger.bank}&PageSize=100`, undefined, token)
  ).items.filter((r) => r.transactionTypeId === s.ledger.chequeType && r.status === 'Active');
  let target = null;
  let state = null;
  for (const r of rows) {
    const rec = await call('GET', `/receivable/${r.id}`, undefined, token);
    if (!target && ['Received', 'Deposited', 'Returned'].includes(rec.chequeState)) {
      target = r.id;
      state = rec.chequeState;
    }
  }
  if (!target) throw new Error('No cheque in the cashbox or deposited.');
  const fromStart = state === 'Received';
  const beforeReturn = state !== 'Returned';
  const act = (key, label) => atBottom(`cheque-action-${key}`, label);
  commands.push(
    ...login(s.users[o.people.manager.key], 'queue'),
    link(`transacoes/${target}`),
    see('CH PRE', 60000),
    ...(fromStart
      ? [
          ...act('custody', 'Levar ao cofre'),
          ...sheetChoose('cheque-sheet-account', d.safe),
          ...inSheet('cheque-sheet-confirm'),
          { waitForAnimationToEnd: { timeout: 5000 } },
          shot('ops-in-safe'),
          ...act('deposit', 'Depositar'),
          ...sheetChoose('cheque-sheet-account', s.ledger.bank),
          ...inSheet('cheque-sheet-confirm'),
          { waitForAnimationToEnd: { timeout: 5000 } },
          shot('ops-deposited'),
        ]
      : []),
    ...(beforeReturn
      ? [
          ...act('return', 'Registrar devolução'),
          ...search('cheque-sheet-client', 'E2E', d.client),
          ...type('cheque-sheet-next', due(2)),
          // Commit the date by leaving the field (the optional alínea stays under the iOS keyboard).
          { pressKey: 'Enter' },
          ...(os === 'android' ? type('cheque-sheet-reason', '11') : []),
          ...inSheet('cheque-sheet-confirm'),
          { waitForAnimationToEnd: { timeout: 5000 } },
          shot('ops-returned'),
        ]
      : []),
    ...act('represent', 'Reapresentar'),
    ...inSheet('cheque-sheet-confirm'),
    { waitForAnimationToEnd: { timeout: 5000 } },
    shot('ops-represented'),
    ...act('convert', 'Converter em fiado'),
    ...sheetChoose('cheque-sheet-account', o.tab),
    ...inSheet('cheque-sheet-confirm'),
    { waitForAnimationToEnd: { timeout: 5000 } },
    shot('ops-converted'),
  );
}
if (journey === 'ledger-convert') {
  // Conversion of the exact re-presented cheque (client already identified by the return): the
  // cheque claim ends and one dedicated fiado debt is created, with no cashbox leg.
  const rows = (
    await call('GET', `/transaction?AccountId=${s.ledger.bank}&PageSize=100`, undefined, token)
  ).items.filter((r) => r.transactionTypeId === s.ledger.chequeType && r.status === 'Active');
  let target = null;
  for (const r of rows) {
    const rec = await call('GET', `/receivable/${r.id}`, undefined, token);
    if (rec.chequeState === 'AwaitingRedeposit') target = r.id;
  }
  if (!target) throw new Error('Run ledger-cheque first.');
  commands.push(
    ...login(s.users[o.people.manager.key], 'queue'),
    link(`transacoes/${target}`),
    wait('cheque-card', 60000),
    see('Reapresentado'),
    ...press('cheque-action-convert'),
    ...choose('cheque-sheet-account', o.tab),
    ...press('cheque-sheet-confirm'),
    see('Convertido em fiado', 60000),
    shot('cheque-converted'),
  );
}
const member = (key) => s.users[o.people[key].key];
const fill = (id, value) => [...type(id, value), hide];
if (journey === 'ledger-open') {
  // No turno today: the M5 opening (explicit zero) travels on the same create; then a Draft that
  // is finalized explicitly from its receipt; then the list from Hoje.
  commands.push(
    ...login(member('memberA')),
    link(`transacoes/nova?accountId=${o.cashbox}`),
    wait('create-type'),
    ...search('create-type', 'Copa', d.types.expense),
    ...fill('create-value', '1250'),
    reach('create-opening'),
    see(`Abrir o dia de Caixa D ${label} com esta operação`),
    shot('create-inline-opening'),
    ...check('create-opening-zero', 'Confirmo que o caixa começa'),
    ...check('create-opening-confirm', 'Conferi a abertura acima'),
    // The preview appears once the opening is confirmed; save after the layout settles.
    reach('create-preview'),
    ...atBottom('create-save', 'Salvar'),
    wait('ledger-receipt-started', 60000),
    shot('create-receipt-started'),
  );
}
if (journey === 'ledger-draft') {
  // With the turno active: a Draft, finalized explicitly from its receipt; then the list.
  commands.push(
    ...login(member('memberA')),
    link(`transacoes/nova?accountId=${o.cashbox}`),
    wait('create-type'),
    ...search('create-type', 'Copa', d.types.expense),
    ...fill('create-value', '300'),
    ...atBottom('create-save-draft', 'Salvar como rascunho'),
    see('Rascunho registrado no servidor', 60000),
    ...press('ledger-receipt-finalize'),
    wait('ledger-receipt-finalized', 60000),
    shot('create-draft-finalized'),
    link(`transacoes?accountId=${o.cashbox}&from=${today}&to=${today}`),
    see('transações', 60000),
    shot('list-today'),
  );
}
if (journey === 'ledger-blocker') {
  // M5 integration: a Draft blocks the close; the typed count survives the trip to the ledger,
  // the Draft is finalized there, the list re-reads empty and the close shows no blocker.
  commands.push(
    ...login(member('memberA')),
    link(`transacoes/nova?accountId=${o.cashbox}`),
    wait('create-type'),
    ...search('create-type', 'Copa', d.types.expense),
    ...fill('create-value', '400'),
    ...below(see(`Entra no turno ativo de Caixa D ${label}.`)),
    ...atBottom('create-save-draft', 'Salvar como rascunho'),
    see('Rascunho registrado no servidor', 60000),
    ...(os === 'android'
      ? [
          link(`fechamentos/${activeSession}`),
          wait('close-context', 60000),
          ...fill(`close-input-${cash}`, '7770'),
          reach('close-draft-count'),
          see('em rascunho'),
          shot('close-blocked'),
          scrollTo('Finalizar movimentações'),
          text('Finalizar movimentações'),
        ]
      : [
          // iOS: the same Draft-blocker address the close screen opens (one cashbox, today, Draft).
          link(`transacoes?accountId=${o.cashbox}&from=${today}&to=${today}&status=Draft`),
        ]),
    wait('list-blocker', 60000),
    see('Rascunhos que bloqueiam o fechamento'),
    shot('list-blocker'),
    scrollTo('Abrir transação: Copa e Banheiro'),
    { tapOn: { text: 'Abrir transação: Copa e Banheiro.*' } },
    ...atBottom('detail-finalize', 'Finalizar rascunho'),
    wait('detail-receipt', 60000),
    shot('detail-finalized'),
    ...(os === 'android'
      ? [
          scrollTo('Voltar para Transações'),
          text('Voltar para Transações'),
          see('Nenhum rascunho pendente em', 60000),
          shot('list-blocker-clear'),
          ...press('list-blocker-back'),
          wait('close-context', 60000),
          reach(`close-input-${cash}`),
          see('77,70'),
          see('Nenhuma movimentação em rascunho neste caixa e dia.'),
          shot('close-retained-unblocked'),
        ]
      : [
          link(`transacoes?accountId=${o.cashbox}&from=${today}&to=${today}&status=Draft`),
          see('Nenhum rascunho pendente em', 60000),
          shot('list-blocker-clear'),
        ]),
  );
}
if (journey === 'ledger-fiado') {
  // Inline client create → fiado sale (server cashbox leg) → statement → PIX then Dinheiro.
  // Unique per run: an earlier run's client with the same name would trigger the duplicate check.
  const name = `Cliente M6 ${label} ${Date.now()}`;
  commands.push(
    ...login(member('memberA')),
    link('transacoes/nova'),
    wait('create-type'),
    ...search('create-type', 'Cliente', d.types.fiadoSale),
    ...choose('create-account', o.tab),
    ...below(see('Venda fiado no balcão')),
    ...fill('create-value', '4000'),
    ...press('create-client-new'),
    wait('client-name', 60000),
    // Inside the editor dialog: no dismiss drag (a downward drag closes the sheet).
    ...type('client-name', name),
    ...type('client-phone', '11988887777'),
    ...press('client-save'),
    ...press('client-return'),
    wait('create-type', 60000),
    ...below(see(name)),
    shot('create-fiado-client-returned'),
    see('40,00'),
    ...atBottom('create-save', 'Salvar'),
    wait('ledger-receipt-generated', 60000),
    shot('create-fiado-receipt'),
  );
}
if (journey === 'ledger-pay') {
  const clients = (await call('GET', '/client', undefined, token)).clients;
  const client = clients
    .filter(
      (c) =>
        /^\d{13}$/.test(c.name.slice(`Cliente M6 ${label} `.length)) &&
        c.name.startsWith(`Cliente M6 ${label} `),
    )
    .sort((a, b) => a.name.localeCompare(b.name))
    .at(-1);
  if (!client) throw new Error('Run ledger-fiado first.');
  commands.push(
    ...login(member('memberA')),
    link(`clientes/${client.id}/extrato`),
    wait('statement-outstanding', 60000),
    see('40,00 · em aberto'),
    shot('statement-debt'),
    ...press('statement-receive'),
    wait('payment-balance', 60000),
    ...choose('payment-account', o.tab),
    ...fill('payment-value', '1000'),
    ...atBottom('payment-confirm', 'Confirmar pagamento'),
    ...below(see('Escolha a forma de pagamento.')),
    shot('payment-no-default'),
    ...press(`payment-method-${d.types.fiadoPix}`),
    ...below(see('Não entra no caixa')),
    ...atBottom('payment-confirm', 'Confirmar pagamento'),
    wait('payment-after-balance', 60000),
    see('30,00 · em aberto'),
    shot('payment-pix'),
    scrollTo('Ver extrato'),
    text('Ver extrato'),
    wait('statement-outstanding', 60000),
    see('30,00 · em aberto'),
    ...press('statement-receive'),
    wait('payment-balance', 60000),
    ...choose('payment-account', o.tab),
    ...fill('payment-value', '500'),
    ...press(`payment-method-${d.types.fiadoCash}`),
    ...below(see('Dinheiro entra no caixa')),
    ...atBottom('payment-confirm', 'Confirmar pagamento'),
    wait('payment-after-balance', 60000),
    see('25,00 · em aberto'),
    shot('payment-cash'),
  );
}
if (journey === 'ledger-safe') {
  // The member's second cashbox has no turno today: the deposit opens it atomically (explicit
  // zero) and moves the cash to the cofre in the same protected two-leg command.
  commands.push(
    ...login(member('memberA')),
    link(`transacoes/deposito-cofre?accountId=${o.second}`),
    wait('safe-destination'),
    ...choose('safe-destination', d.safe),
    ...fill('safe-value', '2000'),
    ...below(see(`Abrir o dia de Caixa D2 ${label} com esta operação`)),
    ...check('safe-opening-zero', 'Confirmo que o caixa começa'),
    ...check('safe-opening-confirm', 'Conferi a abertura acima'),
    ...atBottom('safe-confirm', 'Registrar depósito no cofre'),
    wait('safe-receipt', 60000),
    see('na mesma operação'),
    shot('safe-receipt'),
  );
}
if (journey === 'ledger-cheque') {
  const manager = s.users[o.people.manager.key];
  commands.push(
    ...login(manager, 'queue'),
    link('transacoes/cheque-pre-datado/novo'),
    wait('plan-type'),
    ...search('plan-type', 'Cheque pr', s.ledger.chequeType),
    ...choose('plan-account', s.ledger.bank),
    ...fill('plan-value', '30000'),
    reach('plan-mode'),
    text('Gerar mensais'),
    ...fill('plan-first-due', due(1)),
    ...fill('plan-count', '3'),
    wait('plan-accept', 60000),
    ...below(see('CH PRE (3/3)')),
    shot('plan-schedule'),
    ...check('plan-accept', 'Conferi as datas e valores'),
    ...atBottom('plan-save-draft', 'Salvar plano como rascunho'),
    wait('plan-receipt', 60000),
    ...press('plan-receipt-open'),
    see('Plano de cheques · Rascunho', 60000),
    ...press('plan-finalize'),
    ...press('plan-finalize-dialog-confirm'),
    see('Plano de cheques · Ativo', 60000),
    shot('plan-finalized'),
    scrollTo('Abrir parcela'),
    text('Abrir parcela'),
    wait('cheque-card', 60000),
    see('No caixa'),
    ...press('cheque-action-custody'),
    ...choose('cheque-sheet-account', d.safe),
    ...press('cheque-sheet-confirm'),
    see('No cofre', 60000),
    shot('cheque-in-safe'),
    ...press('cheque-action-deposit'),
    ...choose('cheque-sheet-account', s.ledger.bank),
    ...press('cheque-sheet-confirm'),
    see('pendente de confirmação', 60000),
    shot('cheque-deposited'),
    ...press('cheque-action-return'),
    ...search('cheque-sheet-client', 'E2E', d.client),
    ...fill('cheque-sheet-next', due(2)),
    ...fill('cheque-sheet-reason', '11'),
    ...press('cheque-sheet-confirm'),
    see('Devolvido', 60000),
    shot('cheque-returned'),
    ...press('cheque-action-represent'),
    ...press('cheque-sheet-confirm'),
    see('Reapresentado (aguardando depósito)', 60000),
    shot('cheque-represented'),
    scrollTo('Voltar para Plano de cheque pré-datado'),
    text('Voltar para Plano de cheque pré-datado'),
    see('Plano de cheques · Ativo', 60000),
    scrollTo('2. '),
    { tapOn: { text: 'Abrir parcela', index: 1 } },
    wait('cheque-card', 60000),
    ...press('cheque-action-convert'),
    ...search('cheque-sheet-client', 'E2E', d.client),
    ...choose('cheque-sheet-account', o.tab),
    ...press('cheque-sheet-confirm'),
    see('Convertido em fiado', 60000),
    shot('cheque-converted'),
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
console.log(`M6 ${os}/${journey} journey written outside Git.`);
