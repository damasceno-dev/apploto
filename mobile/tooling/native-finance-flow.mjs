// Writes native-touch journeys for Accounts, Account assignment, Categories & Types and Branch
// members outside Git. The API is the owned local stack (one read-only membership lookup here);
// no token injection or app JS execution. Each journey is a separate file.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { apiCall } from './native-stack.mjs';
const journeys = [
  'admin-accounts',
  'admin-accounts-race-open',
  'admin-accounts-race-confirm',
  'admin-assignment',
  'admin-types',
  'admin-types-create',
  'member-denial',
  'admin-members',
  'manager-members',
  'admin-mail-failure',
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
const f = s.finance?.os?.[os];
if (!s.owned || !f || new URL(s.apiUrl).hostname !== '127.0.0.1')
  throw new Error('Owned local finance fixtures required.');
const label = os === 'ios' ? 'iOS' : 'Android';
const call = apiCall(s.apiUrl);
const identity = (await call('POST', '/user/login', s.users.ana)).responseToken.token;
const token = (await call('POST', '/branch/session', { branchId: s.branches.alfa }, identity))
  .token;
const memberships = (await call('GET', '/branch/users', undefined, token)).branchUsers;
const membership = (userId) => memberships.find((m) => m.userId === userId)?.id;
const esc = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const tap = (id) => ({ tapOn: { id } });
const text = (value) => ({ tapOn: { text: esc(value) } });
const wait = (id) => ({ extendedWaitUntil: { visible: { id, enabled: true }, timeout: 60000 } });
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
/** Bounded search downward, then upward, then lifts the target off the bottom edge. */
const find = (target, budget = 25) => ({
  runFlow: {
    commands: [
      { waitForAnimationToEnd: { timeout: 3000 } },
      swipes(target, 'DOWN', budget),
      swipes(target, 'UP', budget + 15),
    ],
  },
});
const scroll = (id, budget) => find({ id }, budget);
/** Page triggers: no keyboard is open, so the target is centred by Maestro instead of dragged. */
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
/** Pickers and buttons: found, then lifted off the bottom edge (text fields are left in place). */
const reach = (id, budget) => ({
  // Bounds under a pinned footer still count as visible, so centre the target after finding it.
  // Pinned footer buttons live outside the scroll content and cannot be centred: best effort.
  runFlow: { commands: [find({ id }, budget), center(id, true)] },
});
const scrollTo = (value) => find('.*' + esc(value) + '.*');
const optional = (visible, commands) => ({ runFlow: { when: { visible }, commands } });
const close = os === 'android' ? { pressKey: 'Enter' } : { hideKeyboard: {} };
const settle =
  os === 'ios'
    ? [
        { swipe: { start: '50%, 30%', end: '50%, 92%', duration: 700 } },
        { waitForAnimationToEnd: { timeout: 3000 } },
      ]
    : [];
const type = (id, value) => [
  scroll(id),
  tap(id),
  { waitForAnimationToEnd: { timeout: 3000 } },
  { eraseText: 60 },
  { inputText: value },
  // iOS dialogs keep the keyboard; `press` drags it away interactively before the next tap.
  ...(os === 'android' ? [close] : []),
];
const press = (id) => [
  ...settle,
  reach(id),
  tap(id),
  { waitForAnimationToEnd: { timeout: 3000 } },
  // A write refused by an expired token is never replayed by the app; the person presses again.
  optional('.*Token expirado.*', [tap(id)]),
];
/**
 * Opens a dialog from its trigger. A tap during the previous sheet's closing animation can be
 * swallowed, so the trigger is tapped again while the dialog is absent (opening sends nothing).
 */
const openDialog = (trigger, dialog, top = false) => [
  // Bidirectional search first (the target may be above). List rows are then centred; a trigger
  // at the top of the page is left in place, since centring could tuck it under the sticky header.
  scroll(trigger),
  ...(top
    ? [
        // Found by an upward search, it can sit behind the sticky top bar: nudge content down.
        { swipe: { start: '50%, 40%', end: '50%, 55%', duration: 600 } },
        { waitForAnimationToEnd: { timeout: 3000 } },
      ]
    : [center(trigger)]),
  tap(trigger),
  { waitForAnimationToEnd: { timeout: 3000 } },
  {
    repeat: {
      while: { notVisible: { id: dialog } },
      times: 3,
      commands: [{ waitForAnimationToEnd: { timeout: 3000 } }, scroll(trigger), tap(trigger)],
    },
  },
  wait(dialog),
];
/** Opens one category group of the types list by its exact header label. */
const group = (name) => [
  // The header is one accessible element: "{categoria}, {n} tipos · {direção}".
  find(esc(name) + ', \\d+ tipos? · .*'),
  { tapOn: { text: esc(name) + ', \\d+ tipos? · .*' } },
  { waitForAnimationToEnd: { timeout: 3000 } },
];
const pressText = (value) => [
  scrollTo(value),
  text(value),
  { waitForAnimationToEnd: { timeout: 3000 } },
  optional('.*Token expirado.*', [text(value)]),
];
// Inside a picker sheet a downward drag dismisses it, so options are awaited and searched upward
// in the content only (swipe up), never by dragging down.
const choose = (select, value, long = false) => [
  // An iOS keyboard left by the previous field shrinks the dialog; drag it away first.
  ...settle,
  reach(select),
  // On iOS a tap during scroll momentum only stops the scroll, so wait for the page to settle.
  { waitForAnimationToEnd: { timeout: 3000 } },
  tap(select),
  // The sheet slides in; tapping the field again would hit its backdrop and close it.
  { extendedWaitUntil: { visible: { id: `${select}-sheet` }, timeout: 10000 } },
  { waitForAnimationToEnd: { timeout: 3000 } },
  // Long lists fill the screen, so a clipped row is centred inside the sheet. A short sheet shows
  // every option, and a swipe there would land on its backdrop and close it.
  ...(long ? [center(`${select}-option-${value}`)] : []),
  { waitForAnimationToEnd: { timeout: 3000 } },
  tap(`${select}-option-${value}`),
  { waitForAnimationToEnd: { timeout: 3000 } },
];
const shot = (name) => ({ takeScreenshot: `${os}-m10f-${name}` });
const link = (url) => ({
  openLink: {
    link: 'lotero://' + url + (url.includes('?') ? '&' : '?') + 'branchId=' + s.branches.alfa,
  },
});
const login = (user) => [
  { launchApp: { stopApp: true } },
  { extendedWaitUntil: { visible: { id: 'login-email|session-menu' }, timeout: 60000 } },
  // Session restoration can pass through a transient screen; decide only once it has settled.
  { waitForAnimationToEnd: { timeout: 5000 } },
  { extendedWaitUntil: { visible: { id: 'login-email|session-menu' }, timeout: 60000 } },
  optional({ id: 'session-menu' }, [
    wait('navigation-menu'),
    { waitForAnimationToEnd: { timeout: 5000 } },
    tap('session-menu'),
    { waitForAnimationToEnd: { timeout: 5000 } },
    { runFlow: { when: { notVisible: { id: 'logout' } }, commands: [tap('session-menu')] } },
    scroll('logout'),
    tap('logout'),
    optional('Sair da conta\\?', [text('Sair e manter os dados')]),
  ]),
  { extendedWaitUntil: { visible: { id: 'login-email|session-menu' }, timeout: 60000 } },
  // A late restore still lands on the signed-in shell: sign out once more before typing.
  optional({ id: 'session-menu' }, [
    tap('session-menu'),
    { waitForAnimationToEnd: { timeout: 5000 } },
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
  // The return key can submit the form and raise the iOS password sheet first; never save.
  optional('Save Password\\?', [text('Not Now')]),
  optional({ id: 'login-submit' }, [tap('login-submit')]),
  optional('Save Password\\?', [text('Not Now')]),
  wait('navigation-menu'),
];
const back = (title) => (os === 'android' ? { pressKey: 'Back' } : text(`Voltar para ${title}`));
const commands = [];
const admin = s.users[f.admin.key];
// FIN_SUFFIX keeps names unique when a journey is rerun on the same owned database.
const suffix = process.env.FIN_SUFFIX ?? '';
const tag = suffix.replace(/\W/g, '');
const caixaNova = `Caixa nova F ${label}${suffix}`;
const doador = `Doador F ${label}`;
if (journey === 'admin-accounts') {
  commands.push(
    ...login(admin),
    link('gestao/contas'),
    wait('account-new-Terminal'),
    shot('accounts-list'),
    ...openDialog('account-new-Terminal', 'account-editor', true),
    see('Criar conta fiado nova'),
    shot('accounts-create-caixa'),
    ...type('account-name', caixaNova),
    ...press('account-save'),
    { extendedWaitUntil: { notVisible: { id: 'account-editor' }, timeout: 30000 } },
    scrollTo(`Pareado com ${caixaNova}`),
    shot('accounts-default-paired'),
    ...openDialog('account-new-Safe', 'account-editor', true),
    see('O cofre não tem terminal CAIXA'),
    ...type('account-name', `Cofre F ${label}${suffix}`),
    ...press('account-save'),
    { extendedWaitUntil: { notVisible: { id: 'account-editor' }, timeout: 30000 } },
    scrollTo(`Cofre F ${label}${suffix}`),
    ...openDialog(`account-pair-${f.bare}`, 'account-command-pair'),
    see('Parear não dá acesso a ninguém'),
    ...choose('account-pair-choice', f.freeTab),
    shot('accounts-pair-dialog'),
    ...pressText('Parear'),
    scrollTo(`Pareado com ${doador}`),
    ...openDialog(`account-unpair-${f.bare}`, 'account-command-unpair'),
    see('continua ativa, com o saldo e o histórico'),
    ...pressText('Desfazer pareamento'),
    scroll(`account-pair-${f.bare}`),
    shot('accounts-unpaired'),
  );
}
if (journey === 'admin-accounts-race-open') {
  commands.push(
    // Each journey signs in as its own user; a session left by another journey is never reused.
    ...login(admin),
    link('gestao/contas'),
    wait('account-new-Terminal'),
    ...openDialog(`account-pair-${f.racer}`, 'account-command-pair'),
    ...choose('account-pair-choice', f.freeTab),
    see('Parear não dá acesso a ninguém'),
  );
}
if (journey === 'admin-accounts-race-confirm') {
  // The runner paired the same Tab through the API between the two files.
  commands.push(
    ...pressText('Parear'),
    see('Esta conta fiado já está associada a um Terminal ativo'),
    shot('accounts-pair-conflict'),
    ...pressText('Reler contas e conferir'),
    { extendedWaitUntil: { notVisible: '.*Reler contas e conferir.*', timeout: 30000 } },
    see('Parear'),
    shot('accounts-pair-reread'),
  );
}
if (journey === 'admin-assignment') {
  commands.push(
    // Each journey signs in as its own user; a session left by another journey is never reused.
    ...login(admin),
    link('gestao/operadores'),
    scrollTo(`Contas de Operador Admin F ${label}`),
    text(`Contas de Operador Admin F ${label}`),
    wait('assignment-add'),
    see('A conta fiado é uma autorização separada'),
    shot('assignment-empty'),
    ...openDialog('assignment-add', 'assignment-command-add', true),
    ...choose('assignment-account-choice', f.donor),
    see('não é atribuída junto'),
    ...pressText('Atribuir conta'),
    wait('assignment-self-receipt'),
    scrollTo(`Conta fiado pareada: ${doador} · não atribuída a este operador`),
    shot('assignment-self-receipt'),
    ...openDialog(`assignment-add-tab-${f.donor}`, 'assignment-command-add'),
    ...pressText('Atribuir conta'),
    scrollTo(`Conta fiado pareada: ${doador} · também atribuída a este operador`),
    ...openDialog(`assignment-primary-${f.freeTab}`, 'assignment-command-primary'),
    ...pressText('Definir como principal'),
    scrollTo('Principal'),
    ...press(`assignment-account-${f.donor}`),
    scrollTo(`Detalhes: ${doador}`),
    shot('assignment-account-detail'),
    back('Contas dos operadores'),
    wait('assignment-add'),
    see(`Contas de Operador Admin F ${label}`),
    shot('assignment-returned'),
  );
}
if (journey === 'admin-types') {
  commands.push(
    // Each journey signs in as its own user; a session left by another journey is never reused.
    ...login(admin),
    link('gestao/categorias'),
    wait('category-new'),
    ...openDialog('category-new', 'category-editor', true),
    ...type('category-name', `Despesas F ${label}${suffix}`),
    see('Não pode ser mudada depois'),
    ...press('category-direction-Out'),
    ...press('category-save'),
    { extendedWaitUntil: { notVisible: { id: 'category-editor' }, timeout: 30000 } },
    scrollTo(`Despesas F ${label}${suffix}`),
    link('gestao/categorias?tab=tipos'),
    // Same screen, new tab: the page keeps its scroll position, so search for the trigger.
    scroll('type-new'),
    wait('type-new'),
    ...group(`Receitas F ${label}`),
    ...openDialog(`type-edit-${f.type}`, 'type-editor'),
    ...choose('type-monthly', 'None'),
    ...press('type-save'),
    see('Mudar a classificação deste tipo?'),
    see('inclusive em meses bloqueados'),
    shot('types-reclassify'),
    ...pressText('Confirmar e salvar'),
    scrollTo('Tipo de transação salvo no servidor.'),
    ...pressText('Cancelar'),
  );
}
if (journey === 'admin-types-create') {
  commands.push(
    ...login(admin),
    link('gestao/categorias?tab=tipos'),
    wait('type-new'),
    ...openDialog('type-new', 'type-editor', true),
    ...choose('type-category', f.category, true),
    ...type('type-name', `Tarifa F ${label}`),
    ...choose('type-settlement', 'SameDay'),
    ...press('type-save'),
    scrollTo('Já existe um tipo de transação ativo com este nome nesta categoria'),
    shot('types-conflict'),
    ...type('type-name', `Recebimento F ${label}${suffix}`),
    ...press('type-requires-tab'),
    scrollTo('Diga se este tipo é uma forma de recebimento de fiado.'),
    ...press('type-fiado-NonCash'),
    ...press('type-save'),
    { extendedWaitUntil: { notVisible: { id: 'type-editor' }, timeout: 30000 } },
    // System types (here the terminal report legs) sit in the seeded Saídas group.
    ...group('Saídas'),
    scrollTo('Automática'),
    scrollTo('Tipos automáticos não são criados, editados nem desativados manualmente.'),
    shot('types-system'),
  );
}
if (journey === 'member-denial') {
  commands.push(
    ...login(s.users[f.member.key]),
    link('gestao/contas'),
    see('Seu perfil não tem acesso a este destino.'),
    shot('member-accounts-denied'),
    link('gestao/membros'),
    see('Seu perfil não tem acesso a este destino.'),
  );
}
const target = membership(f.target.userId);
if (journey === 'admin-members') {
  commands.push(
    ...login(admin),
    link('gestao/membros'),
    wait('invitation-new'),
    scrollTo('Você não pode alterar o próprio cargo'),
    shot('members-own-row'),
    ...openDialog(`member-role-change-${target}`, 'member-role-dialog'),
    ...press('member-role-Manager'),
    ...pressText('Salvar função'),
    see('Função: Gerente'),
    link('gestao/membros?section=convites'),
    // Same screen, new section: the page keeps its scroll position, so search for the trigger.
    scroll('invitation-new'),
    wait('invitation-new'),
    scrollTo(f.pendingEmail),
    scrollTo('Não pode ser cancelado; vale até'),
    { assertNotVisible: '.*Cancelar convite.*' },
    shot('invitations-pending'),
    ...openDialog('invitation-new', 'invitation-editor', true),
    ...type('invitation-email', `novo-f-${os}${tag}@example.invalid`),
    ...press('invitation-role-Member'),
    ...choose('invitation-operator', f.freeOperators[0]),
    ...press('invitation-send'),
    see(`Convite enviado para novo-f-${os}${tag}@example.invalid.`),
    see('isso não confirma a chegada na caixa de entrada'),
    shot('invitation-sent'),
    ...openDialog(`invitation-reissue-${f.invitation}`, 'invitation-reissue-dialog'),
    see('O link anterior deixa de valer'),
    ...choose('invitation-reissue-operator', 'none'),
    ...pressText('Reenviar convite'),
    scrollTo(`Convite reenviado para ${f.pendingEmail}.`),
    link('gestao/membros'),
    scroll('invitation-new'),
    wait('invitation-new'),
    ...press(`member-remove-${target}`),
    see('Esta função estará disponível em uma próxima etapa.'),
    shot('members-offboard-availability'),
  );
}
if (journey === 'manager-members') {
  commands.push(
    ...login(s.users[f.manager.key]),
    link('gestao/membros'),
    wait('invitation-new'),
    scrollTo('Somente um administrador pode alterar ou remover o acesso de um administrador.'),
    shot('manager-admin-row'),
    ...openDialog('invitation-new', 'invitation-editor', true),
    see('Gerentes convidam somente Gerente ou Membro.'),
    { assertNotVisible: { id: 'invitation-role-Admin' } },
    shot('manager-invite-roles'),
    ...pressText('Cancelar'),
  );
}
if (journey === 'admin-mail-failure') {
  commands.push(
    ...login(admin),
    link('gestao/membros?section=convites'),
    wait('invitation-new'),
    ...openDialog('invitation-new', 'invitation-editor', true),
    ...type('invitation-email', `falha-f-${os}${tag}@example.invalid`),
    ...press('invitation-role-Manager'),
    ...press('invitation-send'),
    see(
      `O convite para falha-f-${os}${tag}@example.invalid foi criado, mas o envio do e-mail falhou.`,
    ),
    { assertNotVisible: '.*Convite enviado.*' },
    scrollTo('Falha no envio'),
    shot('invitation-failed-delivery'),
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
console.log(`M10 finance ${os}/${journey} journey written outside Git.`);
