// M9 supported-flow seed on Mobile's owned disposable stack only. State/evidence stay outside Git.
import { readFile, writeFile } from 'node:fs/promises';
import { randomBytes, randomUUID } from 'node:crypto';
import { apiCall } from './native-stack.mjs';
const [statePath, mode = 'seed'] = process.argv.slice(2);
const state = JSON.parse(await readFile(statePath, 'utf8'));
if (
  !state.owned ||
  !state.users?.ana?.email.endsWith('@example.invalid') ||
  new URL(state.apiUrl).hostname !== '127.0.0.1'
)
  throw new Error('Owned loopback fixture required.');

const call = apiCall(state.apiUrl);
const login = async (user) => (await call('POST', '/user/login', user)).responseToken.token;
const admin = (
  await call(
    'POST',
    '/branch/session',
    { branchId: state.branches.alfa },
    await login(state.users.ana),
  )
).token;
if (mode === 'prepare-ios' || mode === 'prepare-android') {
  if (!state.time) throw new Error('Seed the M9 state first.');
  const operatorId = mode === 'prepare-ios' ? state.time.member : state.time.memberAndroid;
  const rows = (
    await call(
      'GET',
      `/timeentry?OperatorId=${operatorId}&DateFrom=${state.time.yesterday}&DateTo=${state.time.today}`,
      undefined,
      admin,
    )
  ).items;
  const clock = await call('GET', '/branch/current', undefined, admin);
  for (const row of rows) {
    const entry = await call('GET', '/timeentry/' + row.id, undefined, admin);
    await call(
      'PUT',
      '/timeentry',
      {
        operatorId,
        date: entry.date,
        status: 'Present',
        segments: entry.segments
          .filter((s) => s.active)
          .map((s) => ({
            id: s.id,
            clockIn: s.clockIn,
            clockOut:
              row.date.slice(0, 10) === state.time.yesterday
                ? null
                : (s.clockOut ?? clock.branchLocalDateTime),
          })),
      },
      admin,
    );
  }
  console.log(
    'Prepared the same synthetic segment IDs through supported snapshot writes; no database reset.',
  );
  process.exit(0);
}
if (state.time) throw new Error('Time fixture already seeded; use its existing evidence.');
async function linkedUser(key, role) {
  const user = {
    email: `${key}-m9@example.invalid`,
    password: randomBytes(12).toString('base64url'),
  };
  await call('POST', '/invitation', { email: user.email, role }, admin);
  const search = await (
    await fetch(`${state.mailUrl}/api/v1/search?query=${encodeURIComponent(`to:"${user.email}"`)}`)
  ).json();
  const mail = await (
    await fetch(`${state.mailUrl}/api/v1/message/${search.messages[0].ID}`)
  ).json();
  const capability = /#token=([A-Za-z0-9%._~-]+)/.exec(mail.Text + '\n' + mail.HTML)?.[1];
  if (!capability) throw new Error('Local fixture invitation missing.');
  const registered = await call('POST', '/invitation/register', {
    token: decodeURIComponent(capability),
    name: `Pessoa ${key} M9`,
    password: user.password,
  });
  const operator = await call(
    'POST',
    '/operator',
    { name: `Pessoa ${key} M9`, userId: registered.userId },
    admin,
  );
  state.users[key] = user;
  await writeFile(statePath, JSON.stringify(state), { mode: 0o600 });
  return operator;
}
const androidMember = await linkedUser('memberAndroid', 'Member');
const androidManager = await linkedUser('managerAndroid', 'Manager');
const zero = await call('POST', '/operator', { name: 'Operador sem registros M9' }, admin);
const inactive = await call('POST', '/operator', { name: 'Operador histórico M9' }, admin);
const operators = (await call('GET', '/operator', undefined, admin)).operators;
const member = operators.find((o) => o.name === 'Pessoa memberLinked E2E');
const manager = operators.find((o) => o.name === 'Pessoa managerLinked E2E');
const terminal = (await call('GET', '/account', undefined, admin)).accounts?.find(
  (a) => a.name === 'Caixa shell E2E' && a.type === 'Terminal',
);
// Grant the same disposable terminal only to the additional OS user; sessions remain distinct.
if (terminal)
  await call('POST', `/operator/${androidMember.id}/accounts`, { accountId: terminal.id }, admin);
const clock = await call('GET', '/branch/current', undefined, admin);
const today = clock.branchLocalDate.slice(0, 10);
const shift = (date, delta) => {
  const d = new Date(date + 'T12:00:00Z');
  d.setUTCDate(d.getUTCDate() + delta);
  return d.toISOString().slice(0, 10);
};
const yesterday = shift(today, -1);
const write = (operatorId, date, status, segments = []) =>
  call('PUT', '/timeentry', { operatorId, date, status, segments }, admin);
const closed = (date) => [
  { id: null, clockIn: date + 'T08:00:00', clockOut: date + 'T12:00:00' },
  { id: null, clockIn: date + 'T13:00:00', clockOut: date + 'T17:00:00' },
];
const entries = {};
for (const [key, operator] of [
  ['ios', member],
  ['android', androidMember],
]) {
  entries[key] = await write(operator.id, yesterday, 'Present', [
    { id: null, clockIn: yesterday + 'T23:00:00', clockOut: null },
  ]);
  for (const [offset, status] of [
    [-2, 'DayOff'],
    [-3, 'Vacation'],
    [-4, 'UnjustifiedAbsence'],
    [-5, 'JustifiedAbsence'],
  ])
    await write(operator.id, shift(today, offset), status);
  await write(operator.id, shift(today, -6), 'Present', closed(shift(today, -6)));
}
entries.manager = await write(manager.id, yesterday, 'Present', closed(yesterday));
entries.managerAndroid = await write(androidManager.id, yesterday, 'Present', closed(yesterday));
await write(inactive.id, yesterday, 'Present', closed(yesterday));
await call('DELETE', '/operator/' + inactive.id, undefined, admin);
const setting = await call('GET', '/setting', undefined, admin);
await call('PUT', '/setting', { dailyTargetHours: 6 }, admin, {
  'If-Match': `"${setting.version}"`,
});
// Zero-entry remains active, inactive history remains returned. A previous-month lock has no active sessions.
const monthStart = today.slice(0, 8) + '01';
const prior = shift(monthStart, -1);
entries.locked = await write(manager.id, prior, 'Present', closed(prior));
// A historical bank movement establishes the operational floor without terminal close activity.
await call(
  'POST',
  '/transaction',
  {
    date: prior,
    value: 1,
    transactionTypeId: state.transaction.transactionTypeId,
    accountId: state.transaction.accountId,
    recordedByOperatorId: manager.id,
    description: 'Histórico sintético M9',
    historicalReason: 'Fixture M9 de mês bloqueado',
  },
  admin,
  { 'Idempotency-Key': randomUUID() },
);
const beforeLock = await call('GET', '/setting', undefined, admin);
await call(
  'POST',
  '/setting/lock-month',
  { year: Number(prior.slice(0, 4)), month: Number(prior.slice(5, 7)) },
  admin,
  { 'If-Match': `"${beforeLock.version}"` },
);
state.time = {
  today,
  yesterday,
  member: member.id,
  memberAndroid: androidMember.id,
  manager: manager.id,
  managerAndroid: androidManager.id,
  zero: zero.id,
  inactive: inactive.id,
  entries,
};
await writeFile(statePath, JSON.stringify(state), { mode: 0o600 });
console.log('M9 synthetic time fixtures seeded; credentials and IDs remain in private state.');
