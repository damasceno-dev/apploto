// Writes native-touch journeys for Mobile M7 (Manager queue, review and decisions) outside Git, in the
// same command format the Android UIAutomator runner (native-day-android.mjs) executes. Session ids
// come from one read-only lookup on the owned local API; no token injection or app JS execution.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { apiCall } from './native-stack.mjs';
const journeys = [
  'queue-approve',
  'duties',
  'batch',
  'stale-open',
  'stale-confirm',
  'discrepancy',
  'reject',
  'review-resubmitted',
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
const o = s.day?.os?.[os];
if (!s.owned || !o?.third || !o?.fifth || new URL(s.apiUrl).hostname !== '127.0.0.1')
  throw new Error('Owned local M7 fixtures required (run native-manager-seed.mjs first).');
const call = apiCall(s.apiUrl);
const identity = (await call('POST', '/user/login', s.users.ana)).responseToken.token;
const token = (await call('POST', '/branch/session', { branchId: s.branches.alfa }, identity))
  .token;
const sessions = async (account) =>
  (await call('GET', `/session?AccountId=${account}&PageSize=50`, undefined, token)).items.sort(
    (a, b) => (a.custody.sequence ?? 0) - (b.custody.sequence ?? 0),
  );
const manager = s.users[o.people.manager.key];
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
const settle = { waitForAnimationToEnd: { timeout: 3000 } };
const shot = (name) => ({ takeScreenshot: `${os}-m7-${name}` });
const optional = (visible, commands) => ({ runFlow: { when: { visible }, commands } });
const link = (url) => ({
  openLink: {
    link: 'lotero://' + url + (url.includes('?') ? '&' : '?') + 'branchId=' + s.branches.alfa,
  },
});
/** The shell holds writes after an access change until the user acknowledges it (real app rule). */
const ack = optional('.*Entendi a mudança de acesso.*', [text('Entendi a mudança de acesso')]);
const open = (url) => [link(url), settle, ack];
/**
 * M7_RESUME=1 continues the already signed-in app (secure-store session) instead of a full login:
 * the dev client reloads JS about three minutes after launch, which long journeys would cross.
 */
const resume = [
  { launchApp: { stopApp: false } },
  wait('navigation-menu', 90000),
  link('gestao/fila'),
  wait('queue-day-stepper', 90000),
  ack,
];
const login = (user) => (process.env.M7_RESUME === '1' ? resume : fullLogin(user));
const fullLogin = (user) => [
  { launchApp: { stopApp: true } },
  { extendedWaitUntil: { visible: { id: 'login-email|session-menu' }, timeout: 60000 } },
  { waitForAnimationToEnd: { timeout: 5000 } },
  optional({ id: 'session-menu' }, [
    tap('session-menu'),
    { waitForAnimationToEnd: { timeout: 5000 } },
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
  wait('navigation-menu'),
  // The manager home is the real queue.
  link('gestao/fila'),
  wait('queue-day-stepper', 90000),
  ack,
];
const commands = [];
if (journey === 'queue-approve') {
  const [single] = await sessions(o.fourth);
  commands.push(
    ...login(manager),
    shot('queue'),
    ...open(`gestao/conferencia/${single.id}`),
    wait('review-approve', 90000),
    shot('review'),
    tap('review-approve'),
    wait('approve-dialog-confirm'),
    settle,
    tap('approve-dialog-confirm'),
    see('Fechamento aprovado no servidor.', 60000),
    shot('approved'),
  );
}
if (journey === 'duties') {
  const [mine] = await sessions(o.second);
  commands.push(
    ...login(manager),
    ...open(`gestao/conferencia/${mine.id}`),
    see('Quem registrou a contagem deste turno não pode aprová-lo.', 90000),
    shot('duties-refused'),
  );
}
if (journey === 'batch') {
  commands.push(
    ...login(manager),
    wait(`batch-open-${o.cashbox}`, 90000),
    tap(`batch-open-${o.cashbox}`),
    wait('batch-dialog-confirm'),
    settle,
    shot('batch-dialog'),
    tap('batch-dialog-confirm'),
    see('2 sessões aprovadas no servidor.', 60000),
    shot('batch-approved'),
  );
}
if (journey === 'stale-open') {
  commands.push(
    ...login(manager),
    wait(`batch-open-${o.fifth}`, 90000),
    tap(`batch-open-${o.fifth}`),
    wait('batch-dialog-confirm'),
    settle,
    shot('stale-dialog'),
  );
}
if (journey === 'stale-confirm') {
  commands.push(
    tap('batch-dialog-confirm'),
    see('não pode ser aprovado', 60000),
    shot('stale-refused'),
  );
}
if (journey === 'discrepancy') {
  const [, incoming] = await sessions(o.third);
  commands.push(
    ...login(manager),
    ...open(`gestao/conferencia/${incoming.id}`),
    // Leaf ids: wrapper views can be flattened out of the Android accessibility tree.
    wait('discrepancy-target-Incoming', 90000),
    shot('discrepancy'),
    wait('discrepancy-target-Incoming'),
    tap('discrepancy-target-Incoming'),
    tap('discrepancy-reason'),
    { inputText: 'Contado na entrada do turno' },
    ...(os === 'android' ? [] : [{ hideKeyboard: {} }]),
    settle,
    wait('discrepancy-submit'),
    shot('before-submit'),
    tap('discrepancy-submit'),
    settle,
    shot('after-submit'),
    see('Responsabilidade atribuída no servidor.', 60000),
    shot('attributed'),
  );
}
if (journey === 'reject') {
  const [first] = await sessions(o.seventh);
  commands.push(
    ...login(manager),
    ...open(`gestao/conferencia/${first.id}`),
    wait('review-reject', 90000),
    tap('review-reject'),
    wait('reject-reason'),
    settle,
    tap('reject-reason'),
    { inputText: 'Conferir Raspadinha com a gerencia' },
    ...(os === 'android' ? [] : [{ hideKeyboard: {} }]),
    settle,
    shot('reject-typed'),
    wait('reject-dialog-confirm'),
    settle,
    tap('reject-dialog-confirm'),
    see('Fechamento reprovado no servidor.', 60000),
    shot('rejected'),
  );
}
if (journey === 'review-resubmitted') {
  const [first] = await sessions(o.seventh);
  commands.push(
    ...login(manager),
    ...open(`gestao/conferencia/${first.id}`),
    wait('review-approve', 90000),
    absent('Motivo da reprovação'),
    shot('resubmitted-review'),
    tap('review-approve'),
    wait('approve-dialog-confirm'),
    settle,
    tap('approve-dialog-confirm'),
    see('Fechamento aprovado no servidor.', 60000),
    shot('resubmitted-approved'),
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
console.log(`M7 ${os}/${journey} journey written outside Git.`);
