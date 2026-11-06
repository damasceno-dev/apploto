// Writes native-touch Maestro journeys for Mobile M5 (Hoje, Abrir dia, Fechar dia, Conferir,
// Passar o caixa, Corrigir e reenviar) outside Git. Session ids come from one read-only lookup on
// the owned local API; no token injection or app JS execution.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { apiCall } from './native-stack.mjs';
const journeys = [
  'manager-f1',
  'member-open-prev',
  'member-open',
  'member-count',
  'member-reconfer',
  'manager-offer',
  'member-receive',
  'member-recount',
  'member-fix',
  'member-fix-restore',
  'gestao-unlinked',
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
if (!s.owned || !o || new URL(s.apiUrl).hostname !== '127.0.0.1')
  throw new Error('Owned local M5 fixtures required.');
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
const sessionOf = (operatorId) =>
  sessions
    .filter((row) => row.custody.custodianOperatorId === operatorId)
    .sort((a, b) => (b.custody.sequence ?? 0) - (a.custody.sequence ?? 0))[0]?.id;
const product = (name) => d.products.find((p) => p.name === name).id;
const cash = product('Dinheiro');
const raspa = product('Raspadinha');
const esc = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const tap = (id) => ({ tapOn: { id } });
const text = (value) => ({ tapOn: { text: esc(value) } });
const wait = (id, timeout = 60000) => ({
  extendedWaitUntil: { visible: { id, enabled: true }, timeout },
});
const see = (value, timeout = 30000) => ({
  extendedWaitUntil: { visible: '.*' + esc(value) + '.*', timeout },
});
const absent = (value) => ({ assertNotVisible: '.*' + esc(value) + '.*' });
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
const scroll = (id, budget) => reach(id, budget);
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
const press = (id) => [
  hide,
  // Dismissing the keypad shifts the layout; tap only after it settles and the target is re-found.
  { waitForAnimationToEnd: { timeout: 5000 } },
  reach(id),
  { waitForAnimationToEnd: { timeout: 3000 } },
  tap(id),
  { waitForAnimationToEnd: { timeout: 3000 } },
  optional('.*Token expirado.*', [tap(id)]),
];
/** Calcular: buffer, operator keys from the app row, the visible result, then explicit Usar. */
const calculate = (field, parts, result) => [
  ...press(`${field}-calc`),
  wait(`${field}-expression`),
  tap(`${field}-expression`),
  // The operator row sits above the buffer, so it stays reachable with the keypad open.
  ...parts.flatMap((part) =>
    part.key ? [tap(`${field}-key-${part.key}`)] : [{ inputText: part.text }],
  ),
  see(result),
  ...press(`${field}-accept`),
  // Accepting closes the buffer and the keypad; let the layout settle before the next target.
  { waitForAnimationToEnd: { timeout: 5000 } },
  { extendedWaitUntil: { notVisible: { id: `${field}-expression` }, timeout: 10000 } },
];
const choose = (select, value) => [
  hide,
  reach(select),
  { waitForAnimationToEnd: { timeout: 3000 } },
  tap(select),
  // The option itself, not the sheet wrapper (which a platform may flatten away).
  { extendedWaitUntil: { visible: { id: `${select}-option-${value}` }, timeout: 10000 } },
  { waitForAnimationToEnd: { timeout: 3000 } },
  tap(`${select}-option-${value}`),
  { waitForAnimationToEnd: { timeout: 3000 } },
];
const shot = (name) => ({ takeScreenshot: `${os}-m5-${name}` });

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
const commands = [];
const member = (key) => s.users[o.people[key].key];
if (journey === 'member-open') {
  commands.push(
    ...login(member('memberA')),
    see('Dia ainda não aberto', 60000),
    shot('cockpit-not-open'),
    ...press('cockpit-next-cta'),
    wait('open-start'),
    see('Não há abertura registrada para este caixa.'),
    absent('Dia ainda não aberto'),
    ...press('open-explicit-zero'),
    shot('open-explicit-zero'),
    ...press('open-submit'),
    see('Turno iniciado. A abertura foi registrada.'),
    ...press('open-go-close'),
    wait('close-context'),
    see('Conte o caixa, salve e confira.'),
    absent('Diferença calculada pelo servidor'),
    shot('close-blind'),
  );
}
if (journey === 'manager-f1') {
  // F1: a turno left active past the branch midnight is finished by Gestão with a reason; it stays
  // review work. The exception is recorded as the manager's, never as the operator's.
  commands.push(
    ...login(s.users[o.people.manager.key], 'queue'),
    link(`fechamentos/abrir?accountId=${o.cashbox}`),
    see('Este caixa já tem um turno em andamento.', 60000),
    see('ainda ativo'),
    shot('f1-active'),
    ...press('custody-exception-finish'),
    ...type('custody-exception-reason', 'Turno ficou aberto depois da meia-noite'),
    hide,
    ...press('custody-exception-submit'),
    see('Exceção registrada no servidor.', 30000),
    shot('f1-finished'),
  );
}
if (journey === 'member-open-prev') {
  commands.push(
    ...login(member('memberA')),
    see('Dia ainda não aberto', 60000),
    ...press('cockpit-next-cta'),
    scrollTo('Abertura proposta: Turno anterior'),
    shot('open-previous-session'),
    ...press('open-submit'),
    see('Turno iniciado. A abertura foi registrada.', 30000),
    ...press('open-go-close'),
    wait('close-context'),
    scrollTo('Conte o caixa, salve e confira.'),
    absent('Diferença calculada pelo servidor'),
  );
}
if (journey === 'member-count') {
  commands.push(
    ...login(member('memberA')),
    see('Dia aberto', 60000),
    ...press('cockpit-next-cta'),
    wait('close-context'),
    absent('Diferença calculada pelo servidor'),
    ...calculate(
      `close-input-${cash}`,
      [{ text: '100' }, { key: 'mais' }, { text: '50' }, { key: 'mais' }, { text: '20,5' }],
      '170,50',
    ),
    ...calculate(
      'close-field-TerminalReport',
      [{ text: '10' }, { key: 'menos' }, { text: '25' }],
      '15,00',
    ),
    ...type(`close-input-${raspa}`, '1000'),
    hide,
    { waitForAnimationToEnd: { timeout: 5000 } },
    scrollTo('Salvo às'),
    absent('Diferença calculada pelo servidor'),
    shot('close-saved'),
    ...press('close-conferir-button'),
    scrollTo('Resultado da conferência'),
    scrollTo('Diferença calculada pelo servidor'),
    shot('close-current'),
  );
}
if (journey === 'member-reconfer') {
  commands.push(
    ...login(member('memberA')),
    link(`fechamentos/${sessionOf(o.operators.memberA)}`),
    wait('close-context'),
    scrollTo('Recalculado — não confirmado'),
    shot('close-unconfirmed'),
    ...press('close-still-observed'),
    ...press('close-conferir-button'),
    scrollTo('Resultado da conferência'),
    ...press('close-submit-button'),
    scroll('close-custody'),
    see('Passar o caixa pelo aplicativo'),
    shot('close-submitted-member'),
  );
}
if (journey === 'manager-offer') {
  commands.push(
    ...login(s.users[o.people.manager.key], 'queue'),
    link(`fechamentos/${sessionOf(o.operators.memberA)}`),
    wait('close-context'),
    scroll('close-custody'),
    ...type('close-custody-reason', 'Troca de turno'),
    hide,
    ...press('close-offer-start'),
    ...choose('close-offer-incoming', o.operators.memberB),
    ...press('close-offer-button'),
    scroll('close-pending-offer'),
    see(`Passagem para ${o.people.memberB.name}`),
    shot('close-offered'),
  );
}
if (journey === 'member-receive') {
  commands.push(
    ...login(member('memberB')),
    see('Receber o caixa', 60000),
    shot('cockpit-receive'),
    ...press('cockpit-next-cta'),
    wait('handover-different'),
    ...press('handover-different'),
    ...type(`open-observed-${cash}`, '16000'),
    hide,
    ...press('handover-accept-submit'),
    see('Caixa recebido. Seu turno começou.', 30000),
    see('sem atribuição'),
    shot('handover-received'),
    ...press('open-go-close'),
    wait('close-context'),
  );
}
if (journey === 'member-recount') {
  commands.push(
    ...login(member('memberB')),
    see('Dia aberto', 60000),
    ...press('cockpit-next-cta'),
    wait('close-context'),
    ...type(`close-input-${cash}`, '16000'),
    hide,
    hide,
    { waitForAnimationToEnd: { timeout: 5000 } },
    scrollTo('Salvo às'),
    ...press('close-conferir-button'),
    scrollTo('Resultado da conferência'),
    ...press('close-recount-button'),
    scrollTo('Recontagem em andamento'),
    absent('Diferença calculada pelo servidor'),
    shot('close-recounting'),
    ...press('close-conferir-button'),
    scrollTo('Resultado da conferência'),
    ...press('close-submit-button'),
    scroll('close-custody'),
    ...press('close-finish-button'),
    { extendedWaitUntil: { notVisible: { id: 'close-finish-button' }, timeout: 30000 } },
    shot('close-finished'),
  );
}
if (journey === 'member-fix') {
  commands.push(
    ...login(member('memberA')),
    see('precisa de correção', 60000),
    shot('cockpit-correction'),
    ...press('cockpit-next-cta'),
    wait('close-causes'),
    see('Observação da gerência: Conferir Raspadinha com a gerência'),
    shot('fix-causes'),
    ...type(`close-input-${raspa}`, '1200'),
    hide,
    scrollTo('Alterações não salvas no servidor'),
    { waitForAnimationToEnd: { timeout: 3000 } },
    // Process death with the correction unsaved on the server.
    { stopApp: {} },
  );
}
if (journey === 'member-fix-restore') {
  commands.push(
    { launchApp: { stopApp: false } },
    wait('navigation-menu', 90000),
    link(`fechamentos/${sessionOf(o.operators.memberA)}/correcao`),
    wait('close-context'),
    see('Há texto guardado neste dispositivo.', 30000),
    shot('fix-retained'),
    scrollTo('Restaurar para revisão'),
    text('Restaurar para revisão'),
    { waitForAnimationToEnd: { timeout: 3000 } },
    ...press('close-save'),
    hide,
    { waitForAnimationToEnd: { timeout: 5000 } },
    scrollTo('Salvo às'),
    ...press('close-conferir-button'),
    scrollTo('Resultado da conferência'),
    ...press('close-submit-button'),
    { extendedWaitUntil: { notVisible: { id: 'close-submit-button' }, timeout: 30000 } },
    shot('fix-resubmitted'),
  );
}
if (journey === 'gestao-unlinked') {
  commands.push(
    ...login(s.users[o.people.unlinked.key], 'queue'),
    link(`fechamentos/${sessionOf(o.operators.memberB)}`),
    wait('close-context'),
    see('Gestão'),
    absent('Seu login ainda não está vinculado a um operador.'),
    shot('gestao-unlinked-close'),
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
console.log(`M5 ${os}/${journey} journey written outside Git.`);
