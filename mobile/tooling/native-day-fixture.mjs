// Mobile M5 (Operator Day) seed on Mobile's owned disposable stack only. Synthetic people come from
// local-Mailpit invitations; terminals, cashboxes, grants, a client and a cofre are created through
// the ordinary admin APIs. Credentials, IDs and evidence stay in the private state file.
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
if (state.day) throw new Error('Operator-day fixture already seeded; reuse its private state.');
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
    email: `${key.toLowerCase()}-day@example.invalid`,
    password: randomBytes(12).toString('base64url'),
  };
  await call('POST', '/invitation', { email: user.email, role }, admin);
  const registered = await call('POST', '/invitation/register', {
    token: await capability(user.email),
    name,
    password: user.password,
  });
  state.users[key] = user;
  return { key, userId: registered.userId, name };
}
const products = (await call('GET', '/product', undefined, admin)).items;
const types = (await call('GET', '/transaction-type', undefined, admin)).items;
const pick = (predicate, what) => {
  const found = types.find(predicate);
  if (!found) throw new Error(`Seeded type missing: ${what}`);
  return found.id;
};
const day = {
  products: products.map((p) => ({ id: p.id, name: p.name, order: p.displayOrder })),
  types: {
    // Non-cash tender recorded on the cashbox (card, PIX), an expense, the fiado sale on the Tab
    // and both repayment methods; system types are never chosen.
    debit: pick((t) => t.name === 'Cartão de Débito' && !t.systemRole, 'Cartão de Débito'),
    pix: pick((t) => t.name === 'PIX' && t.categoryName === 'Saídas', 'PIX (saída)'),
    expense: pick((t) => t.name === 'Copa e Banheiro', 'Copa e Banheiro'),
    // A fiado sale is the Tab saída; Tab entradas are repayments (Receber pagamento only).
    fiadoSale: pick((t) => t.name === 'Cliente' && t.categoryName === 'Saídas', 'Cliente (saída)'),
    fiadoCash: pick((t) => t.fiadoPaymentKind === 'CountedCash', 'fiado em dinheiro'),
    fiadoPix: pick((t) => t.fiadoPaymentKind === 'NonCash', 'fiado em PIX'),
  },
  os: {},
};
const safe = await call('POST', '/account/safe', { name: 'Cofre D E2E' }, admin);
const client = await call(
  'POST',
  '/client',
  { name: 'Cliente D E2E', phone: '11999990000' },
  admin,
);
day.safe = safe.id;
day.client = client.id;
for (const os of ['ios', 'android']) {
  const label = os === 'ios' ? 'iOS' : 'Android';
  const people = {
    memberA: await invite(`dayMemberA${label}`, 'Member', `Membro A ${label}`),
    memberB: await invite(`dayMemberB${label}`, 'Member', `Membro B ${label}`),
    manager: await invite(`dayManager${label}`, 'Manager', `Gerente D ${label}`),
    unlinked: await invite(`dayGestor${label}`, 'Manager', `Gestor D ${label}`),
  };
  const terminal = await call('POST', '/terminal', { name: `T-${label}` }, admin);
  const cashbox = await call(
    'POST',
    '/account/terminal',
    { name: `Caixa D ${label}`, createTabAccount: true, terminalId: terminal.id },
    admin,
  );
  const second = await call(
    'POST',
    '/account/terminal',
    { name: `Caixa D2 ${label}`, createTabAccount: true, terminalId: terminal.id },
    admin,
  );
  const operators = {};
  for (const key of ['memberA', 'memberB', 'manager']) {
    const person = people[key];
    const operator = await call(
      'POST',
      '/operator',
      { name: person.name, userId: person.userId },
      admin,
    );
    operators[key] = operator.id;
    await call('POST', `/operator/${operator.id}/accounts`, { accountId: cashbox.id }, admin);
    await call(
      'POST',
      `/operator/${operator.id}/accounts`,
      { accountId: cashbox.tabAccountId },
      admin,
    );
    // Depósito Dinheiro needs the cofre grant too (a Member acts only on granted accounts).
    await call('POST', `/operator/${operator.id}/accounts`, { accountId: safe.id }, admin);
  }
  // Member A also holds the second cashbox on the same physical terminal (separate custody).
  await call('POST', `/operator/${operators.memberA}/accounts`, { accountId: second.id }, admin);
  day.os[os] = {
    people: Object.fromEntries(
      Object.entries(people).map(([k, v]) => [k, { key: v.key, userId: v.userId, name: v.name }]),
    ),
    operators,
    terminal: terminal.id,
    cashbox: cashbox.id,
    tab: cashbox.tabAccountId,
    second: second.id,
  };
}
state.day = day;
await writeFile(statePath, JSON.stringify(state), { mode: 0o600 });
console.log('Operator-day fixtures seeded; credentials and IDs remain in private state.');
