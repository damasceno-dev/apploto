// Mobile M7 (Manager queue) seed on Mobile's owned disposable stack only. Uses the ordinary
// session/handover APIs as the synthetic people, so the Manager queue has real work:
//   prepare      two extra cashboxes (third: discrepancy, fourth: single approval)
//   batch        two Submitted sessions on ONE cashbox/day (handover Member A -> Member B)
//   batch2       a second batch pair on a fifth cashbox (the stale all-or-nothing journey)
//   fix          Member A corrects and resubmits the rejected first session on the third cashbox
//   discrepancy  a handover whose acknowledged opening differs (opening discrepancy to triage)
//   duties       a session counted and submitted by the Manager (the duties-separation refusal)
//   single       one Member-submitted session (single independent approval)
//   ids          list session ids/status per scenario box
// No decision is written here; the journeys do that. Credentials/IDs stay in the private state.
import { readFile, writeFile } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';

const [action, os, statePath] = process.argv.slice(2);
const actions = [
  'sixth',
  'fixb',
  'fix',
  'batch',
  'batch2',
  'discrepancy',
  'duties',
  'single',
  'ids',
  'prepare',
];
if (!actions.includes(action) || !['ios', 'android'].includes(os) || !statePath)
  throw new Error(`Use ${actions.join('|')} ios|android <owned-state>`);
const s = JSON.parse(await readFile(statePath, 'utf8'));
const o = s.day?.os?.[os];
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
const must = (label, r) => {
  if (r.status >= 300) throw new Error(`${label} refused (${r.status}): ${JSON.stringify(r.body)}`);
  return r;
};
const key = () => ({ 'Idempotency-Key': randomUUID() });
async function branchToken(user) {
  const identity = (await request('POST', '/user/login', user)).body.responseToken.token;
  return (await request('POST', '/branch/session', { branchId: s.branches.alfa }, identity)).body
    .token;
}
const person = (k) => s.users[o.people[k].key];
const tokens = new Map();
const tokenOf = async (k) => {
  if (!tokens.has(k)) tokens.set(k, await branchToken(person(k)));
  return tokens.get(k);
};
const admin = async () => {
  if (!tokens.has('ana')) tokens.set('ana', await branchToken(s.users.ana));
  return tokens.get('ana');
};
const items = (value) =>
  s.day.products.filter((p) => !/Diferença/i.test(p.name)).map((p) => ({ productId: p.id, value }));

async function proposal(token, account) {
  return must(
    'proposal',
    await request('GET', `/session/opening-proposal?accountId=${account}`, undefined, token),
  ).body;
}
async function startSession(who, account, operatorId) {
  const token = await tokenOf(who);
  const p = await proposal(token, account);
  if (p.activeSessionId) return p.activeSessionId;
  const opening = p.isMissing
    ? { explicitZero: true, reason: 'Abertura sintética do caixa', custodianOperatorId: operatorId }
    : {
        custodianOperatorId: operatorId,
        sourceKind: p.sourceKind,
        sourceId: p.sourceId,
        sourceRevision: p.sourceRevision,
      };
  const started = must(
    'start',
    await request('POST', '/session/start', { accountId: account, opening }, token, key()),
  ).body;
  return started.id ?? started.session?.id;
}
const read = async (token, id) =>
  must('read', await request('GET', `/session/${id}`, undefined, token));
async function count(who, id, value) {
  const token = await tokenOf(who);
  let session = await read(token, id);
  must(
    'items',
    await request(
      'PUT',
      `/session/${id}/items`,
      { items: items(value), notes: 'Contagem sintética para a fila do gestor.' },
      token,
      { 'If-Match': session.etag },
    ),
  );
  session = await read(token, id);
  const c = session.body.count;
  must(
    'conferir',
    await request(
      'POST',
      `/session/${id}/conferir`,
      {
        confirmationToken: c.confirmationToken,
        countRevision: c.countRevision,
        ledgerRevision: c.ledgerRevision,
        openingRevision: c.openingRevision,
      },
      token,
      { ...key(), 'If-Match': session.etag },
    ),
  );
}
async function submit(who, id) {
  const token = await tokenOf(who);
  const session = await read(token, id);
  const c = session.body.count;
  must(
    'submit',
    await request(
      'POST',
      `/session/${id}/submit`,
      {
        attemptId: c.confirmedAttempt.id,
        countRevision: c.countRevision,
        ledgerRevision: c.ledgerRevision,
        openingRevision: c.openingRevision,
      },
      token,
      { 'If-Match': session.etag },
    ),
  );
}
async function sessionsOf(account) {
  const token = await admin();
  return must(
    'list',
    await request('GET', `/session?AccountId=${account}&PageSize=50`, undefined, token),
  )
    .body.items.map((r) => ({
      id: r.id,
      status: r.status,
      custodian: r.custody.custodianOperatorName,
      sequence: r.custody.sequence,
    }))
    .sort((a, b) => (a.sequence ?? 0) - (b.sequence ?? 0));
}
/** Member A counts and hands the box to Member B (optionally acknowledging a different opening). */
async function handover(account, ackValue) {
  const token = await tokenOf('memberA');
  const a = await startSession('memberA', account, o.operators.memberA);
  const first = await read(token, a);
  // Resumable: a re-run continues from the state a previous partial run reached.
  if (first.body.status === 'Draft') {
    await count('memberA', a, 100);
    await submit('memberA', a);
  }
  const p = await proposal(token, account);
  let handoverId = p.pendingHandoverId;
  let handoverVersion = null;
  if (!handoverId) {
    const readA = await read(token, a);
    const offeredRes = must(
      'offer',
      await request(
        'POST',
        `/session/${a}/handover`,
        {
          incomingOperatorId: o.operators.memberB,
          countRevision: readA.body.count.countRevision,
        },
        token,
        { ...key(), 'If-Match': readA.etag },
      ),
    );
    handoverId = offeredRes.body.id ?? offeredRes.body.handover?.id;
    handoverVersion = offeredRes.body.version ?? offeredRes.body.handover?.version;
  } else {
    const history = must(
      'history',
      await request('GET', `/session/${a}/history`, undefined, token),
    ).body;
    handoverVersion = history.handovers.find((h) => h.id === handoverId).version;
  }
  const bToken = await tokenOf('memberB');
  const accept =
    ackValue === undefined
      ? {}
      : { items: items(ackValue), reason: 'Contagem de recebimento diferente' };
  const accepted = must(
    'accept',
    await request('POST', `/session/handover/${handoverId}/accept`, accept, bToken, {
      ...key(),
      'If-Match': `"${handoverVersion}"`,
    }),
  ).body;
  const b =
    accepted.id ?? accepted.session?.id ?? (await proposal(bToken, account)).activeSessionId;
  await count('memberB', b, 100);
  await submit('memberB', b);
  return { a, b };
}

const out = {};
if (action === 'prepare') {
  const token = await admin();
  const made = [];
  for (const name of ['Caixa D3', 'Caixa D4']) {
    const box = must(
      'account',
      await request(
        'POST',
        '/account/terminal',
        { name: `${name} ${os}`, createTabAccount: true, terminalId: o.terminal },
        token,
      ),
    ).body;
    for (const k of ['memberA', 'memberB', 'manager']) {
      must(
        'grant',
        await request('POST', `/operator/${o.operators[k]}/accounts`, { accountId: box.id }, token),
      );
    }
    made.push(box.id);
  }
  s.day.os[os].third = made[0];
  s.day.os[os].fourth = made[1];
  await writeFile(statePath, JSON.stringify(s), { mode: 0o600 });
  out.prepared = made.length;
} else if (action === 'sixth') {
  // A fresh cashbox with one Member-submitted session; the box key is argv[5] (default sixth), so a
  // journey that consumes its session can be repeated on a new box (reject -> correct -> review).
  const key = process.argv[5] ?? 'sixth';
  if (!o[key]) {
    const token = await admin();
    const box = must(
      'account',
      await request(
        'POST',
        '/account/terminal',
        { name: `Caixa D-${key} ${os}`, createTabAccount: true, terminalId: o.terminal },
        token,
      ),
    ).body;
    for (const k of ['memberA', 'memberB', 'manager'])
      must(
        'grant',
        await request('POST', `/operator/${o.operators[k]}/accounts`, { accountId: box.id }, token),
      );
    s.day.os[os][key] = box.id;
    await writeFile(statePath, JSON.stringify(s), { mode: 0o600 });
    o[key] = box.id;
  }
  const id = await startSession('memberA', o[key], o.operators.memberA);
  await count('memberA', id, 100);
  await submit('memberA', id);
  out[key] = id;
} else if (action === 'fixb') {
  // Member B corrects and resubmits the rejected second session of the fifth cashbox.
  const second = (await sessionsOf(o.fifth))[1];
  await count('memberB', second.id, 110);
  await submit('memberB', second.id);
  out.fixb = second.id;
} else if (action === 'fix') {
  const [first] = await sessionsOf(o[process.argv[5] ?? 'third']);
  await count('memberA', first.id, 110);
  await submit('memberA', first.id);
  out.fix = first.id;
} else if (action === 'batch2') {
  if (!o.fifth) {
    const token = await admin();
    const box = must(
      'account',
      await request(
        'POST',
        '/account/terminal',
        { name: `Caixa D5 ${os}`, createTabAccount: true, terminalId: o.terminal },
        token,
      ),
    ).body;
    for (const k of ['memberA', 'memberB', 'manager'])
      must(
        'grant',
        await request('POST', `/operator/${o.operators[k]}/accounts`, { accountId: box.id }, token),
      );
    s.day.os[os].fifth = box.id;
    await writeFile(statePath, JSON.stringify(s), { mode: 0o600 });
    o.fifth = box.id;
  }
  out.batch2 = await handover(o.fifth, 100);
} else if (action === 'batch') out.batch = await handover(o.cashbox, 100);
else if (action === 'discrepancy') out.discrepancy = await handover(o.third, 90);
else if (action === 'duties') {
  const id = await startSession('manager', o.second, o.operators.manager);
  await count('manager', id, 100);
  await submit('manager', id);
  out.duties = id;
} else if (action === 'single') {
  const id = await startSession('memberA', o.fourth, o.operators.memberA);
  await count('memberA', id, 100);
  await submit('memberA', id);
  out.single = id;
} else {
  for (const k of ['cashbox', 'second', 'third', 'fourth', 'fifth', 'sixth', 'seventh'])
    if (o[k]) out[k] = await sessionsOf(o[k]);
}
console.log(JSON.stringify(out));
