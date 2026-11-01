// M10 supported-flow seed on Mobile's owned disposable stack only. Synthetic users/records come
// from invitations and ordinary admin APIs; credentials, IDs and evidence stay in private state.
import { readFile, writeFile } from 'node:fs/promises';
import { randomBytes } from 'node:crypto';
import { apiCall } from './native-stack.mjs';
const [statePath] = process.argv.slice(2);
const state = JSON.parse(await readFile(statePath, 'utf8'));
if (
  !state.owned ||
  !state.users?.ana?.email.endsWith('@example.invalid') ||
  new URL(state.apiUrl).hostname !== '127.0.0.1'
)
  throw new Error('Owned loopback fixture required.');
if (state.admin) throw new Error('M10 fixture already seeded; reuse its private state.');
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
const seen = new Set();
async function invite(key, role) {
  const user = {
    email: `${key}-m10@example.invalid`,
    password: randomBytes(12).toString('base64url'),
  };
  await call('POST', '/invitation', { email: user.email, role }, admin);
  let capability;
  for (let attempt = 0; attempt < 40 && !capability; attempt++) {
    const search = await (
      await fetch(
        `${state.mailUrl}/api/v1/search?query=${encodeURIComponent(`to:"${user.email}"`)}`,
      )
    ).json();
    for (const message of search.messages.filter((m) => !seen.has(m.ID))) {
      const mail = await (await fetch(`${state.mailUrl}/api/v1/message/${message.ID}`)).json();
      capability = /#token=([A-Za-z0-9%._~-]+)/.exec(mail.Text + '\n' + mail.HTML)?.[1];
      seen.add(message.ID);
    }
    if (!capability) await new Promise((resolve) => setTimeout(resolve, 250));
  }
  if (!capability) throw new Error('Local fixture invitation missing.');
  const registered = await call('POST', '/invitation/register', {
    token: decodeURIComponent(capability),
    name: `Pessoa ${key} M10`,
    password: user.password,
  });
  state.users[key] = user;
  return { ...user, userId: registered.userId, name: `Pessoa ${key} M10` };
}
const terminal = (await call('GET', '/account', undefined, admin)).accounts?.find(
  (a) => a.name === 'Caixa shell E2E' && a.type === 'Terminal',
);
const fixture = { os: {} };
for (const os of ['ios', 'android']) {
  const label = os === 'ios' ? 'iOS' : 'Android';
  const adminUser = await invite(`admin${label}`, 'Admin');
  const member = await invite(`member${label}`, 'Member');
  const spare = await invite(`spare${label}`, 'Member');
  const adminOperator = await call(
    'POST',
    '/operator',
    { name: `Admin M10 ${label}`, userId: adminUser.userId },
    admin,
  );
  const memberOperator = await call(
    'POST',
    '/operator',
    { name: `Membro M10 ${label}`, userId: member.userId },
    admin,
  );
  if (terminal)
    for (const operator of [adminOperator, memberOperator])
      await call('POST', `/operator/${operator.id}/accounts`, { accountId: terminal.id }, admin);
  const unlinked = await call('POST', '/operator', { name: `Sem login M10 ${label}` }, admin);
  const removable = await call(
    'POST',
    '/client',
    { name: `Cliente desativar M10 ${label}`, phone: '6133334444' },
    admin,
  );
  const product = await call(
    'POST',
    '/product',
    { name: `Produto M10 ${label}`, displayOrder: os === 'ios' ? 500 : 600 },
    admin,
  );
  fixture.os[os] = {
    admin: { key: `admin${label}`, userId: adminUser.userId, operatorId: adminOperator.id },
    member: { key: `member${label}`, userId: member.userId, operatorId: memberOperator.id },
    spare: { key: `spare${label}`, userId: spare.userId, name: spare.name, email: spare.email },
    unlinked: unlinked.id,
    removableClient: removable.id,
    product: product.id,
  };
}
// One active client per CPF per branch: both OS journeys meet the same verbatim conflict.
const duplicate = await call(
  'POST',
  '/client',
  { name: 'Cliente CPF M10', phone: '61912345678', cpf: '52998224725' },
  admin,
);
await call('POST', '/product', { name: 'Raspadinha M10', displayOrder: 400 }, admin);
const clock = await call('GET', '/branch/current', undefined, admin);
const today = clock.branchLocalDate.slice(0, 10);
const shift = (date, delta) => {
  const d = new Date(date + 'T12:00:00Z');
  d.setUTCDate(d.getUTCDate() + delta);
  return d.toISOString().slice(0, 10);
};
await call(
  'POST',
  '/holiday',
  { holidays: [{ date: `${today.slice(0, 4)}-12-08`, description: 'Feriado municipal M10' }] },
  admin,
);
// Policy window: closed 8h days yesterday and today for one operator, read through M9 reports.
const attendance = await call('POST', '/operator', { name: 'Ponto M10' }, admin);
const closed = (date) => [
  { id: null, clockIn: date + 'T08:00:00', clockOut: date + 'T12:00:00' },
  { id: null, clockIn: date + 'T13:00:00', clockOut: date + 'T17:00:00' },
];
for (const date of [shift(today, -1), today])
  await call(
    'PUT',
    '/timeentry',
    { operatorId: attendance.id, date, status: 'Present', segments: closed(date) },
    admin,
  );
const balance = await call(
  'GET',
  '/report/timeentry-balance?' +
    new URLSearchParams({ OperatorId: attendance.id, DateFrom: shift(today, -1), DateTo: today }),
  undefined,
  admin,
);
const setting = await call('GET', '/setting', undefined, admin);
state.admin = {
  ...fixture,
  today,
  yesterday: shift(today, -1),
  duplicateClient: duplicate.id,
  attendance: attendance.id,
  before: { setting, days: balance.operators[0]?.days ?? [] },
};
await writeFile(statePath, JSON.stringify(state), { mode: 0o600 });
console.log('M10 synthetic admin fixtures seeded; credentials and IDs remain in private state.');
