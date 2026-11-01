// Supported API rereads proving M10 native journeys persisted, written outside Git. No writes.
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { apiCall } from './native-stack.mjs';
const [statePath, output] = process.argv.slice(2);
const root = fileURLToPath(new URL('../../', import.meta.url));
if (!path.isAbsolute(output ?? '') || path.resolve(output).startsWith(root))
  throw new Error('Use <owned-state> <outside-checkout.json>.');
const s = JSON.parse(await readFile(statePath, 'utf8'));
if (!s.owned || !s.admin || new URL(s.apiUrl).hostname !== '127.0.0.1')
  throw new Error('Owned local M10 fixtures required.');
const call = apiCall(s.apiUrl);
const identity = (await call('POST', '/user/login', s.users.ana)).responseToken.token;
const token = (await call('POST', '/branch/session', { branchId: s.branches.alfa }, identity))
  .token;
const get = (route) => call('GET', route, undefined, token);
const [operators, members, clients, products, setting] = await Promise.all([
  get('/operator'),
  get('/branch/users'),
  get('/client'),
  get('/product'),
  get('/setting'),
]);
const year = Number(s.admin.today.slice(0, 4));
const holidays = {};
for (const y of [year, year + 1])
  holidays[y] = (await get(`/holiday?Year=${y}&Page=1&PageSize=100`)).items.map((h) => ({
    date: h.date.slice(0, 10),
    description: h.description ?? null,
  }));
const balance = await get(
  '/report/timeentry-balance?' +
    new URLSearchParams({
      OperatorId: s.admin.attendance,
      DateFrom: s.admin.yesterday,
      DateTo: s.admin.today,
    }),
);
const days = balance.operators[0]?.days ?? [];
const day = (list, date) => list.find((d) => d.date.slice(0, 10) === date);
const per = {};
for (const [os, f] of Object.entries(s.admin.os)) {
  const byId = (id) => operators.operators.find((o) => o.id === id) ?? null;
  per[os] = {
    ownOperatorLinkedToSelf: byId(f.admin.operatorId)?.userId === f.admin.userId,
    unlinkedOperatorNowLinkedToSpare: byId(f.unlinked)?.userId === f.spare.userId,
    spareStillActiveMember: members.branchUsers.some(
      (m) => m.userId === f.spare.userId && m.active,
    ),
    removableClientDeactivated: !clients.clients.some((c) => c.id === f.removableClient),
    maestroClients: clients.clients
      .filter((c) => c.name.startsWith(`Cliente Maestro ${os === 'ios' ? 'iOS' : 'Android'}`))
      .map((c) => ({ name: c.name, phone: c.phone, cpf: c.cpf })),
    duplicateCpfClients: clients.clients.filter((c) => c.cpf === '52998224725').length,
    fixtureProduct: products.items.find((p) => p.id === f.product) ?? null,
  };
}
const evidence = {
  at: new Date().toISOString(),
  apiUrl: s.apiUrl,
  per,
  products: products.items.map((p) => `${p.displayOrder}:${p.name}`),
  systemProductActive: products.items.some((p) => p.name === 'Diferença Caixa'),
  holidays,
  setting: {
    before: {
      version: s.admin.before.setting.version,
      dailyTargetHours: s.admin.before.setting.dailyTargetHours,
      lunchDeductionOver6H: s.admin.before.setting.lunchDeductionOver6H,
      lunchDeductionOver4H: s.admin.before.setting.lunchDeductionOver4H,
    },
    after: {
      version: setting.version,
      lockDate: setting.lockDate,
      dailyTargetHours: setting.dailyTargetHours,
      lunchDeductionOver6H: setting.lunchDeductionOver6H,
      lunchDeductionOver4H: setting.lunchDeductionOver4H,
    },
  },
  policy: {
    yesterdayBefore: day(s.admin.before.days, s.admin.yesterday) ?? null,
    yesterdayAfter: day(days, s.admin.yesterday) ?? null,
    todayBefore: day(s.admin.before.days, s.admin.today) ?? null,
    todayAfter: day(days, s.admin.today) ?? null,
  },
};
evidence.policy.yesterdayUnchanged =
  JSON.stringify(evidence.policy.yesterdayBefore) ===
  JSON.stringify(evidence.policy.yesterdayAfter);
await writeFile(output, JSON.stringify(evidence, null, 2), { mode: 0o600 });
console.log(JSON.stringify(evidence, null, 2));
