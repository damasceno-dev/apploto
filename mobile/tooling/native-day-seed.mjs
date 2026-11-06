// Between native M5 journeys: records real ledger movements through the supported financial APIs
// (as the synthetic custodian, bound by the server to the active turno), records another actor's
// ledger change after Conferir, rejects a submitted close as the manager, and rereads persisted
// sessions/attempts/handovers for verification. Owned loopback stack only; output stays private.
import { readFile, writeFile } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
const [action, os, statePath, output] = process.argv.slice(2);
if (
  !['ledger', 'change', 'reject', 'verify', 'ids'].includes(action) ||
  !['ios', 'android'].includes(os)
)
  throw new Error(
    'Use ledger|change|reject|verify|ids ios|android <owned-state> [private-output.json]',
  );
const s = JSON.parse(await readFile(statePath, 'utf8'));
const d = s.day;
const o = d?.os?.[os];
if (!s.owned || !o || new URL(s.apiUrl).hostname !== '127.0.0.1')
  throw new Error('Owned M5 state required.');
async function request(method, route, body, token, headers = {}) {
  const response = await fetch(s.apiUrl + route, {
    method,
    headers: {
      ...(body === undefined ? {} : { 'content-type': 'application/json' }),
      ...(token ? { authorization: `Bearer ${token}` } : {}),
      ...headers,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  return {
    status: response.status,
    etag: response.headers.get('etag'),
    body: text ? JSON.parse(text) : null,
  };
}
async function branchToken(user) {
  const identity = (await request('POST', '/user/login', user)).body.responseToken.token;
  return (await request('POST', '/branch/session', { branchId: s.branches.alfa }, identity)).body
    .token;
}
const person = (key) => s.users[o.people[key].key];
// DAY_DATE pins the proof's business date when a run crosses the branch midnight.
const today =
  process.env.DAY_DATE ??
  (await request('GET', '/branch/current', undefined, await branchToken(s.users.ana))).body
    .branchLocalDate;
async function sessions(token) {
  return (
    await request(
      'GET',
      `/session?AccountId=${o.cashbox}&DateFrom=${today}&DateTo=${today}&PageSize=50`,
      undefined,
      token,
    )
  ).body.items.sort((a, b) => (a.custody.sequence ?? 0) - (b.custody.sequence ?? 0));
}
const results = [];
const createOne = async (label, route, body, token) => {
  const result = await request('POST', route, body, token, { 'Idempotency-Key': randomUUID() });
  results.push({ label, status: result.status, messages: result.body?.errorMessages ?? null });
  if (result.status >= 300) {
    if (output) await writeFile(output, JSON.stringify(results, null, 2), { mode: 0o600 });
    throw new Error(`${label} refused (${result.status}); see private output.`);
  }
};
if (action === 'ledger') {
  // DAY_FROM resumes after movements a previous partial run already recorded (never duplicated).
  const from = Number(process.env.DAY_FROM ?? 0);
  let step = 0;
  const create = async (...args) => (step++ >= from ? createOne(...args) : undefined);
  const token = await branchToken(person(process.env.DAY_WHO ?? 'memberA'));
  const base = { date: today };
  await create(
    'Cartão de Débito 30',
    '/transaction',
    { ...base, value: 30, transactionTypeId: d.types.debit, accountId: o.cashbox },
    token,
  );
  await create(
    'PIX 20',
    '/transaction',
    { ...base, value: 20, transactionTypeId: d.types.pix, accountId: o.cashbox },
    token,
  );
  await create(
    'Despesa 12',
    '/transaction',
    { ...base, value: 12, transactionTypeId: d.types.expense, accountId: o.cashbox },
    token,
  );
  await create(
    'Venda fiado 15',
    '/transaction',
    {
      ...base,
      value: 15,
      transactionTypeId: d.types.fiadoSale,
      accountId: o.tab,
      clientId: d.client,
    },
    token,
  );
  await create(
    'Fiado em dinheiro 10',
    '/transaction/fiado-payment',
    {
      ...base,
      value: 10,
      transactionTypeId: d.types.fiadoCash,
      accountId: o.tab,
      clientId: d.client,
    },
    token,
  );
  await create(
    'Fiado em PIX 5',
    '/transaction/fiado-payment',
    {
      ...base,
      value: 5,
      transactionTypeId: d.types.fiadoPix,
      accountId: o.tab,
      clientId: d.client,
    },
    token,
  );
  await create(
    'Depósito Dinheiro 40',
    '/transaction/safe-deposit',
    { ...base, value: 40, accountId: o.cashbox, safeAccountId: d.safe },
    token,
  );
}
if (action === 'change') {
  // Another movement on the same turno after Conferir: a ledger change only (count unchanged).
  const token = await branchToken(person('memberA'));
  await createOne(
    'PIX 7 após conferir',
    '/transaction',
    { date: today, value: 7, transactionTypeId: d.types.pix, accountId: o.cashbox },
    token,
  );
}
if (action === 'reject') {
  const token = await branchToken(s.users.ana);
  const target = (await sessions(token)).find(
    (row) => row.custody.custodianOperatorId === o.operators.memberA,
  );
  const result = await request(
    'POST',
    `/session/${target.id}/reject`,
    { rejectionReason: 'Conferir Raspadinha com a gerência' },
    token,
  );
  results.push({
    label: 'reject',
    status: result.status,
    messages: result.body?.errorMessages ?? null,
  });
}
if (action === 'ids') {
  const token = await branchToken(s.users.ana);
  console.log(
    JSON.stringify(
      (await sessions(token)).map((row) => ({
        id: row.id,
        custodian: row.custody.custodianOperatorName,
        status: row.status,
        custody: row.custody.custodyState,
      })),
    ),
  );
}
if (action === 'verify') {
  const token = await branchToken(s.users.ana);
  const rows = await sessions(token);
  const detail = [];
  for (const row of rows) {
    const review = await request('GET', `/session/${row.id}/review`, undefined, token);
    const history = await request('GET', `/session/${row.id}/history`, undefined, token);
    detail.push({
      id: row.id,
      custodian: row.custody.custodianOperatorName,
      origin: row.custody.origin,
      custodyState: row.custody.custodyState,
      status: row.status,
      candidate: review.body.count.candidateState,
      attempts: (history.body.countAttempts ?? []).map((a) => ({
        n: a.attemptNumber,
        kind: a.kind,
        difference: a.difference,
        totalClosing: a.totalClosing,
      })),
      handovers: history.body.handovers.map((h) => ({
        state: h.state,
        to: h.intendedIncomingOperatorName,
      })),
      events: history.body.events.map((e) => e.type),
      closeFields: review.body.closeFields.map((f) => [f.field, f.value, !!f.transactionId]),
      items: review.body.items
        .filter((i) => i.closingValue != null)
        .map((i) => [i.productName, i.closingValue]),
      openingDiscrepancy: review.body.openingDiscrepancy?.currentTotalDelta ?? null,
      notes: review.body.notes ?? null,
    });
  }
  const ledger = (
    await request(
      'GET',
      `/transaction?AccountId=${o.cashbox}&DateFrom=${today}&DateTo=${today}&PageSize=100`,
      undefined,
      token,
    )
  ).body.items.map((t) => [
    t.transactionTypeName,
    t.direction,
    t.value,
    t.status,
    t.generationKind ?? null,
    t.sessionId,
  ]);
  results.push({ today, sessions: detail, ledger });
}
if (output) await writeFile(output, JSON.stringify(results, null, 2), { mode: 0o600 });
console.log(`M5 ${action} ${os}: ${results.length} private result(s).`);
