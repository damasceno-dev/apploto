// Synthetic generated-share setup via supported financial APIs; never imported by the app.
import { readFile, writeFile } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
import { apiCall } from './native-stack.mjs';
const [statePath] = process.argv.slice(2);
const s = JSON.parse(await readFile(statePath, 'utf8'));
if (!s.owned || !s.time || new URL(s.apiUrl).hostname !== '127.0.0.1')
  throw new Error('Owned M9 state required.');
const c = apiCall(s.apiUrl);
const token = (
  await c(
    'POST',
    '/branch/session',
    { branchId: s.branches.alfa },
    (await c('POST', '/user/login', s.users.ana)).responseToken.token,
  )
).token;
const accounts = (await c('GET', '/account', undefined, token)).accounts;
const terminal = accounts.find((a) => a.type === 'Terminal');
const tab = accounts.find((a) => a.id === terminal.tabAccountId);
const categories = (await c('GET', '/category', undefined, token)).items;
const sale = (await c('GET', '/transaction-type', undefined, token)).items.find(
  (t) =>
    t.requiresTabAccountAndClient &&
    categories.find((category) => category.id === t.categoryId)?.defaultDirection === 'Out',
);
for (const operatorId of [s.time.member, s.time.memberAndroid]) {
  const assigned = (await c('GET', `/operator/${operatorId}/accounts`, undefined, token)).accounts;
  for (const account of [terminal, tab])
    if (!assigned?.some((a) => a.accountId === account.id)) {
      const response = await fetch(s.apiUrl + `/operator/${operatorId}/accounts`, {
        method: 'POST',
        headers: { authorization: 'Bearer ' + token, 'content-type': 'application/json' },
        body: JSON.stringify({ accountId: account.id }),
      });
      if (!response.ok && response.status !== 409)
        throw new Error('Synthetic account assignment failed: ' + response.status);
    }
}
const activeSessions = await c(
  'GET',
  '/session?CustodyState=Active&PageSize=100',
  undefined,
  token,
);
const active = activeSessions.items?.find((row) => row.accountId === terminal.id);
const started = active
  ? { session: active }
  : await c(
      'POST',
      '/session/start',
      {
        accountId: terminal.id,
        opening: {
          custodianOperatorId: s.time.member,
          explicitZero: true,
          items: [],
          reason: 'Abertura sintética para verificação M9',
        },
      },
      token,
      { 'Idempotency-Key': randomUUID() },
    );
const client = await c(
  'POST',
  '/client',
  { name: 'Cliente sintético M9', phone: '11999990000' },
  token,
);
for (const operatorId of [s.time.member, s.time.memberAndroid])
  await c(
    'POST',
    '/transaction',
    {
      date: s.time.today,
      accountId: tab.id,
      clientId: client.id,
      dueDate: s.time.today,
      transactionTypeId: sale.id,
      value: 5,
      recordedByOperatorId: operatorId,
      handsOutCountedStock: true,
      sessionId: started.session.id,
      description: 'Fixture sintética de parcela automática M9',
      historicalReason: 'Lançamento sintético para resumo M9',
    },
    token,
    { 'Idempotency-Key': randomUUID() },
  );
s.time.generatedShare = {};
for (const operatorId of [s.time.member, s.time.memberAndroid])
  s.time.generatedShare[operatorId] = await c(
    'GET',
    `/report/operator-summary?OperatorId=${operatorId}&DateFrom=${s.time.today}&DateTo=${s.time.today}`,
    undefined,
    token,
  );
await writeFile(statePath, JSON.stringify(s), { mode: 0o600 });
console.log('Real automatic counterparts and summary subsets captured in private M9 state.');
