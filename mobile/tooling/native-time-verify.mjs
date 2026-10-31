// Supported API rereads plus exact local-source/stack identity. All evidence is private synthetic data.
import { readFile, writeFile, readdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { apiCall } from './native-stack.mjs';
const [statePath, output, stage = 'snapshot', baselinePath] = process.argv.slice(2);
const root = fileURLToPath(new URL('../../', import.meta.url));
if (!path.isAbsolute(output ?? '') || path.resolve(output).startsWith(root))
  throw new Error('Evidence must stay outside Git.');
const state = JSON.parse(await readFile(statePath, 'utf8'));
if (
  !state.owned ||
  !state.time ||
  new URL(state.apiUrl).hostname !== '127.0.0.1' ||
  !state.users.ana.email.endsWith('@example.invalid')
)
  throw new Error('Owned synthetic M9 stack required.');
const call = apiCall(state.apiUrl);
const token = (
  await call(
    'POST',
    '/branch/session',
    { branchId: state.branches.alfa },
    (await call('POST', '/user/login', state.users.ana)).responseToken.token,
  )
).token;
const evidence = {
  stage,
  branch: state.branches.alfa,
  observedAt: new Date().toISOString(),
  head: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(),
  operators: {},
  checks: [],
};
for (const name of ['member', 'memberAndroid', 'manager', 'managerAndroid']) {
  const operatorId = state.time[name];
  const list = await call(
    'GET',
    '/timeentry?' +
      new URLSearchParams({
        OperatorId: operatorId,
        DateFrom: name.startsWith('manager')
          ? state.time.today.slice(0, 8) + '01'
          : state.time.yesterday,
        DateTo: state.time.today,
      }),
    undefined,
    token,
  );
  const entries = [];
  for (const row of list.items)
    entries.push(await call('GET', '/timeentry/' + row.id, undefined, token));
  const summary = await call(
    'GET',
    '/report/operator-summary?' +
      new URLSearchParams({
        OperatorId: operatorId,
        DateFrom: state.time.today,
        DateTo: state.time.today,
      }),
    undefined,
    token,
  );
  const balance = await call(
    'GET',
    '/report/timeentry-balance?' +
      new URLSearchParams({
        OperatorId: operatorId,
        DateFrom: name.startsWith('manager')
          ? state.time.today.slice(0, 8) + '01'
          : state.time.yesterday,
        DateTo: state.time.today,
      }),
    undefined,
    token,
  );
  evidence.operators[name] = { operatorId, entries, summary, balance };
}
const team = await call(
  'GET',
  '/report/timeentry-balance?' +
    new URLSearchParams({
      DateFrom: state.time.today.slice(0, 8) + '01',
      DateTo: state.time.today,
    }),
  undefined,
  token,
);
evidence.team = team;
const zero = team.operators.find((operator) => operator.operatorId === state.time.zero);
const inactive = team.operators.find((operator) => operator.operatorId === state.time.inactive);
if (
  !zero?.operatorActive ||
  zero.days.length !== 0 ||
  inactive?.operatorActive !== false ||
  !inactive.days.length
)
  throw new Error('Real zero-entry/inactive-history fixture assertions failed.');
evidence.checks.push({ activeZeroEntryReturned: true, inactiveHistoryReturned: true });
const memberDays = evidence.operators.member.balance.operators[0]?.days ?? [];
evidence.checks.push({
  perDayTargets: memberDays.map((day) => ({
    date: day.date,
    target: day.totalHours - day.balanceHours,
  })),
});
if (stage === 'locked-refusal') {
  const locked = await call('GET', '/timeentry/' + state.time.entries.locked.id, undefined, token);
  const body = {
    operatorId: locked.operatorId,
    date: locked.date,
    status: locked.status,
    segments: locked.segments.map((segment) => ({
      id: segment.id,
      clockIn: segment.clockIn,
      clockOut: segment.clockOut,
    })),
  };
  const refusal = await fetch(state.apiUrl + '/timeentry', {
    method: 'PUT',
    headers: { authorization: 'Bearer ' + token, 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
  const result = await refusal.json();
  if (refusal.status !== 409 || !result.errorMessages?.length)
    throw new Error('Expected the real locked-day refusal.');
  const after = await call('GET', '/timeentry/' + locked.id, undefined, token);
  if (JSON.stringify(locked) !== JSON.stringify(after))
    throw new Error('Refused day unexpectedly changed.');
  evidence.checks.push({
    lockedStatus: refusal.status,
    errorMessages: result.errorMessages,
    unchangedAfterRefusal: true,
  });
}
if (stage === 'context-matrix') {
  const window = { DateFrom: state.time.today, DateTo: state.time.today };
  async function scopedUser(key) {
    return (
      await call(
        'POST',
        '/branch/session',
        { branchId: state.branches.alfa },
        (await call('POST', '/user/login', state.users[key])).responseToken.token,
      )
    ).token;
  }
  async function expectReport(userToken, route, context, status) {
    const response = await fetch(
      state.apiUrl + '/report/' + route + '?' + new URLSearchParams({ ...window, ...context }),
      {
        headers: { authorization: 'Bearer ' + userToken },
      },
    );
    const body = await response.json();
    if (response.status !== status)
      throw new Error('Unexpected report context status: ' + route + '/' + response.status);
    evidence.checks.push({
      route,
      context,
      status,
      ...(status !== 200 ? { errorMessages: body.errorMessages } : {}),
    });
    return body;
  }
  const unlinkedMember = await scopedUser('carla');
  const empty = await expectReport(unlinkedMember, 'operator-summary', { Mine: 'true' }, 200);
  const noOperators = await expectReport(
    unlinkedMember,
    'timeentry-balance',
    { Mine: 'true' },
    200,
  );
  if (empty.totalTransactionCount !== 0 || noOperators.operators.length !== 0)
    throw new Error('Unlinked Member short-circuit changed.');
  const unlinkedManager = await scopedUser('manager');
  await expectReport(unlinkedManager, 'operator-summary', { Mine: 'true' }, 404);
  await expectReport(unlinkedManager, 'timeentry-balance', { Mine: 'true' }, 404);
  await expectReport(unlinkedManager, 'operator-summary', {}, 400);
  await expectReport(unlinkedManager, 'timeentry-balance', {}, 200);
  const linkedAdmin = await scopedUser('adminLinked');
  await expectReport(linkedAdmin, 'operator-summary', { Mine: 'true' }, 200);
  await expectReport(linkedAdmin, 'timeentry-balance', { Mine: 'true' }, 200);
  const inactive = await expectReport(
    linkedAdmin,
    'operator-summary',
    { OperatorId: state.time.inactive },
    200,
  );
  if (inactive.operatorActive !== false) throw new Error('Inactive history marker changed.');
  await expectReport(
    linkedAdmin,
    'operator-summary',
    { Mine: 'true', OperatorId: state.time.member },
    400,
  );
  await expectReport(
    linkedAdmin,
    'timeentry-balance',
    { DateFrom: '2025-01-01', DateTo: '2026-01-03' },
    400,
  );
  const linkedMember = await scopedUser('memberLinked');
  await expectReport(linkedMember, 'operator-summary', { OperatorId: state.time.manager }, 403);
  await expectReport(linkedMember, 'timeentry-balance', { OperatorId: state.time.manager }, 403);
}
if (stage === 'overnight-open') {
  const entries = evidence.operators.memberAndroid.entries;
  for (const date of [state.time.yesterday, state.time.today])
    if (
      !entries.some(
        (entry) =>
          entry.date.slice(0, 10) === date &&
          entry.segments.some((segment) => segment.active && !segment.clockOut),
      )
    )
      throw new Error('The explicit today-open choice must leave both days open.');
  evidence.checks.push({ yesterdayAndTodayOpenIndependently: true });
}
if (stage === 'deleted-days') {
  if (!path.isAbsolute(baselinePath ?? '') || path.resolve(baselinePath).startsWith(root))
    throw new Error('Deletion proof requires the outside-checkout pre-delete API evidence.');
  const baseline = JSON.parse(await readFile(baselinePath, 'utf8'));
  if (baseline.branch !== state.branches.alfa)
    throw new Error('Foreign deletion baseline refused.');
  for (const [name, os] of [
    ['manager', 'ios'],
    ['managerAndroid', 'android'],
  ]) {
    const date = state.time.journeyDates[os];
    const prior = baseline.operators[name].entries.find(
      (entry) => entry.date.slice(0, 10) === date,
    );
    if (!prior) throw new Error('Native-created baseline missing.');
    const response = await fetch(state.apiUrl + '/timeentry/' + prior.id, {
      headers: { authorization: 'Bearer ' + token },
    });
    const current = evidence.operators[name];
    if (
      response.status !== 404 ||
      current.entries.some((entry) => entry.id === prior.id) ||
      current.balance.operators.some((operator) =>
        operator.days.some((day) => day.date.slice(0, 10) === date),
      )
    )
      throw new Error('Native deletion did not remove the active resource/day trail.');
    evidence.checks.push({
      nativeDeletedId: prior.id,
      date,
      detailStatus: response.status,
      absentFromListAndBalance: true,
    });
  }
}
const files = [];
async function walk(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const file = path.join(directory, entry.name);
    if (entry.isDirectory()) await walk(file);
    else files.push(file);
  }
}
await walk(path.join(root, 'mobile/src'));
const hash = createHash('sha256');
for (const file of files.sort()) {
  hash.update(path.relative(root, file));
  hash.update(await readFile(file));
}
evidence.runtimeSourceSha256 = hash.digest('hex');
evidence.serverDllSha256 = createHash('sha256')
  .update(await readFile(path.join(root, 'server/server.API/bin/Debug/net10.0/server.API.dll')))
  .digest('hex');
evidence.openApiSha256 = createHash('sha256')
  .update(await readFile(path.join(root, 'shared/api/openapi/server-api.openapi.json')))
  .digest('hex');
const metro = await fetch('http://127.0.0.1:8083/status');
evidence.metroProjectRoot = metro.headers.get('x-react-native-project-root');
if (evidence.metroProjectRoot !== path.join(root, 'mobile'))
  throw new Error('Foreign Metro; evidence refused.');
await writeFile(output, JSON.stringify(evidence, null, 2), { mode: 0o600 });
console.log('Supported API rereads and runtime identity captured outside Git: ' + stage);
