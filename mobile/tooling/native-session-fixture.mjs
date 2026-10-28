// Disposable API fixtures only; no database access, production credentials or token logging.
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const [mode, statePath, platform, fixturePath, evidencePath, phase = `${platform}-final`] =
  process.argv.slice(2);
if (
  !['prepare', 'verify'].includes(mode) ||
  !['ios', 'android'].includes(platform) ||
  !statePath ||
  !fixturePath
)
  throw new Error(
    'Usage: node tooling/native-session-fixture.mjs prepare|verify <owned-state> ios|android <outside-checkout-fixture.json> [http-evidence.jsonl] [phase]',
  );
const root = fileURLToPath(new URL('../../', import.meta.url));
if (!path.isAbsolute(fixturePath) || path.resolve(fixturePath).startsWith(root))
  throw new Error('Fixtures must stay outside the checkout.');
const state = JSON.parse(await readFile(statePath, 'utf8'));
const api = new URL(state.apiUrl);
if (
  api.protocol !== 'http:' ||
  !['127.0.0.1', 'localhost'].includes(api.hostname) ||
  !state.users?.ana?.email?.endsWith('@example.invalid') ||
  !state.owned
)
  throw new Error('Only owned loopback disposable stacks with fake identities are supported.');
const call = async (route, body, token) => {
  const response = await fetch(state.apiUrl + route, {
    method: body ? 'POST' : 'GET',
    headers: {
      ...(body ? { 'content-type': 'application/json' } : {}),
      ...(token ? { authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!response.ok)
    throw new Error(`Disposable fixture ${route.split('?')[0]} returned ${response.status}`);
  return response.json();
};
// M8 keeps one active refresh token per person: finish the previous OS before this login.
const identity = await call('/user/login', state.users.ana);
const branch = await call(
  '/branch/session',
  { branchId: state.branches.alfa },
  identity.responseToken.token,
);
const clock = await call('/branch/current', undefined, branch.token);
const listing = await call(
  `/transaction?AccountId=${state.transaction.accountId}&DateFrom=${clock.branchLocalDate}&DateTo=${clock.branchLocalDate}&PageSize=100`,
  undefined,
  branch.token,
);
if (mode === 'prepare') {
  const fixture = {
    platform,
    clock,
    listing,
    body: {
      date: clock.branchLocalDate,
      value: 12.34,
      description: `native-${platform}-m2-${randomUUID()}`,
      accountId: state.transaction.accountId,
      transactionTypeId: state.transaction.transactionTypeId,
      recordedByOperatorId: state.transaction.operatorId,
    },
  };
  await mkdir(path.dirname(fixturePath), { recursive: true });
  await writeFile(fixturePath, JSON.stringify(fixture, null, 2), { mode: 0o600, flag: 'wx' });
  console.log(
    JSON.stringify({
      platform,
      prepared: true,
      branchDate: clock.branchLocalDate,
      before: listing.totalCount,
    }),
  );
} else {
  const before = JSON.parse(await readFile(fixturePath, 'utf8'));
  assert.equal(
    clock.branchLocalDate,
    before.clock.branchLocalDate,
    'Business day changed: reconcile the fixture explicitly.',
  );
  assert.equal(listing.totalCount, before.listing.totalCount + 1);
  assert.equal(
    listing.items.filter((item) => item.description === before.body.description).length,
    1,
  );
  const summary = {
    platform,
    before: before.listing.totalCount,
    after: listing.totalCount,
    durableResults: 1,
  };
  if (evidencePath) {
    const entries = (await readFile(evidencePath, 'utf8'))
      .trim()
      .split('\n')
      .map((line) => JSON.parse(line));
    const sends = entries.filter(
      (entry) => entry.phase === phase && entry.method === 'POST' && entry.path === '/transaction',
    );
    assert.equal(sends.length, 2);
    assert(sends.every((entry) => entry.status === 201));
    assert.equal(new Set(sends.map((entry) => entry.key)).size, 1);
    assert.equal(new Set(sends.map((entry) => entry.bodySha256)).size, 1);
    assert.equal(new Set(sends.map((entry) => entry.etag)).size, 1);
    Object.assign(summary, {
      dispatches: 2,
      uniqueKeys: 1,
      uniqueBodyHashes: 1,
      matchingETag: true,
    });
  }
  console.log(JSON.stringify(summary));
}
