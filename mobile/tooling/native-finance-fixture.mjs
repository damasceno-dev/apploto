// M10 Accounts / Assignment / Categories & Types / Members seed on Mobile's owned disposable stack
// only. Synthetic users come from local-Mailpit invitations; every record is created through the
// ordinary admin APIs. Credentials, IDs and evidence stay in the private state file.
import { readFile, writeFile } from 'node:fs/promises';
import { randomBytes } from 'node:crypto';
import { apiCall } from './native-stack.mjs';
const [statePath] = process.argv.slice(2);
const state = JSON.parse(await readFile(statePath, 'utf8'));
if (
  !state.owned ||
  !state.users?.ana?.email.endsWith('@example.invalid') ||
  new URL(state.apiUrl).hostname !== '127.0.0.1' ||
  new URL(state.mailUrl).hostname !== '127.0.0.1'
)
  throw new Error('Owned loopback fixture required.');
if (state.finance) throw new Error('Finance fixture already seeded; reuse its private state.');
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
/** Reads the newest capability link for an address from the owned local Mailpit only. */
async function capability(email) {
  for (let attempt = 0; attempt < 40; attempt++) {
    const search = await (
      await fetch(`${state.mailUrl}/api/v1/search?query=${encodeURIComponent(`to:"${email}"`)}`)
    ).json();
    for (const message of search.messages.filter((m) => !seen.has(m.ID))) {
      const mail = await (await fetch(`${state.mailUrl}/api/v1/message/${message.ID}`)).json();
      seen.add(message.ID);
      const token = /#token=([A-Za-z0-9%._~-]+)/.exec(mail.Text + '\n' + mail.HTML)?.[1];
      if (token) return decodeURIComponent(token);
    }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error('Local fixture invitation missing.');
}
async function invite(key, role, name) {
  const user = {
    email: `${key}-fin@example.invalid`,
    password: randomBytes(12).toString('base64url'),
  };
  await call('POST', '/invitation', { email: user.email, role }, admin);
  const registered = await call('POST', '/invitation/register', {
    token: await capability(user.email),
    name,
    password: user.password,
  });
  state.users[key] = user;
  return { ...user, userId: registered.userId, name };
}
const finance = { os: {} };
for (const os of ['ios', 'android']) {
  const label = os === 'ios' ? 'iOS' : 'Android';
  const adminUser = await invite(`finAdmin${label}`, 'Admin', `Admin F ${label}`);
  const managerUser = await invite(`finManager${label}`, 'Manager', `Gerente F ${label}`);
  const memberUser = await invite(`finMember${label}`, 'Member', `Membro F ${label}`);
  const target = await invite(`finTarget${label}`, 'Member', `Alvo F ${label}`);
  const selfOperator = await call(
    'POST',
    '/operator',
    { name: `Operador Admin F ${label}`, userId: adminUser.userId },
    admin,
  );
  const free = [];
  for (const n of [1, 2])
    free.push(await call('POST', '/operator', { name: `Sem login F${n} ${label}` }, admin));
  // A caixa without Tab, a donor whose default Tab is released (a free Tab), and a paired caixa.
  const bare = await call(
    'POST',
    '/account/terminal',
    { name: `Caixa sem fiado F ${label}`, createTabAccount: false },
    admin,
  );
  const donor = await call('POST', '/account/terminal', { name: `Doador F ${label}` }, admin);
  await call('DELETE', `/account/terminal/${donor.id}/tab`, undefined, admin);
  const racer = await call(
    'POST',
    '/account/terminal',
    { name: `Caixa corrida F ${label}`, createTabAccount: false },
    admin,
  );
  const category = await call(
    'POST',
    '/category',
    { name: `Receitas F ${label}`, defaultDirection: 'In' },
    admin,
  );
  const type = await call(
    'POST',
    '/transaction-type',
    {
      categoryId: category.id,
      name: `Tarifa F ${label}`,
      settlementRule: 'SameDay',
      requiresTabAccountAndClient: false,
      fiadoPaymentKind: null,
      settlementInstrument: 'None',
      monthlyFinancialRole: 'BolaoServiceFee',
    },
    admin,
  );
  const pending = `pendente-f-${os}@example.invalid`;
  const invitation = await call(
    'POST',
    '/invitation',
    { email: pending, role: 'Member', operatorId: free[1].id },
    admin,
  );
  finance.os[os] = {
    admin: { key: `finAdmin${label}`, userId: adminUser.userId, operatorId: selfOperator.id },
    manager: { key: `finManager${label}`, userId: managerUser.userId },
    member: { key: `finMember${label}`, userId: memberUser.userId },
    target: { key: `finTarget${label}`, userId: target.userId },
    freeOperators: free.map((o) => o.id),
    bare: bare.id,
    donor: donor.id,
    freeTab: donor.tabAccountId,
    racer: racer.id,
    category: category.id,
    type: type.id,
    invitation: invitation.id,
    pendingEmail: pending,
  };
}
state.finance = finance;
await writeFile(statePath, JSON.stringify(state), { mode: 0o600 });
console.log('Finance admin fixtures seeded; credentials and IDs remain in private state.');
