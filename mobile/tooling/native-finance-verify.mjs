// Supported API rereads proving the M10 Accounts / Assignment / Categories & Types / Members
// native journeys persisted. Read-only; the report is written outside Git.
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { apiCall } from './native-stack.mjs';
const [statePath, output, ...oses] = process.argv.slice(2);
const root = fileURLToPath(new URL('../../', import.meta.url));
if (
  !path.isAbsolute(output ?? '') ||
  path.resolve(output).startsWith(root) ||
  !oses.length ||
  oses.some((os) => !['ios', 'android'].includes(os))
)
  throw new Error('Use <owned-state> <outside-checkout.json> ios|android…');
const s = JSON.parse(await readFile(statePath, 'utf8'));
if (!s.owned || !s.finance || new URL(s.apiUrl).hostname !== '127.0.0.1')
  throw new Error('Owned local finance fixtures required.');
const call = apiCall(s.apiUrl);
const identity = (await call('POST', '/user/login', s.users.ana)).responseToken.token;
const token = (await call('POST', '/branch/session', { branchId: s.branches.alfa }, identity))
  .token;
const get = (route) => call('GET', route, undefined, token);
const [{ accounts }, { items: categories }, { items: types }, { branchUsers }] = await Promise.all([
  get('/account'),
  get('/category'),
  get('/transaction-type'),
  get('/branch/users'),
]);
const invitations = [];
for (let page = 1; ; page++) {
  const result = await get(`/invitation?Page=${page}&PageSize=100`);
  invitations.push(...result.items);
  if (page * result.pageSize >= result.totalCount) break;
}
// Journey reruns append " N" to names and N to e-mail local parts; any rerun's record counts.
const named = (value, base) =>
  new RegExp(`^${base.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}( \\d+)?$`).test(value);
const mailed = (value, local) => new RegExp(`^${local}\\d*@example\\.invalid$`).test(value);
const checks = [];
const check = (os, name, ok, detail) => checks.push({ os, name, ok: !!ok, detail });
for (const os of oses) {
  const f = s.finance.os[os];
  const label = os === 'ios' ? 'iOS' : 'Android';
  const byId = (id) => accounts.find((a) => a.id === id);
  const caixa = accounts.find(
    (a) => a.type === 'Terminal' && named(a.name, `Caixa nova F ${label}`),
  );
  const caixaTab = caixa && byId(caixa.tabAccountId);
  check(os, 'new caixa paired with a new Tab by default', caixaTab?.type === 'Tab', {
    tabTerminal: caixaTab?.terminalAccountId === caixa?.id,
  });
  check(
    os,
    'cofre created as Safe without pairing',
    accounts.some((a) => a.type === 'Safe' && named(a.name, `Cofre F ${label}`) && !a.tabAccountId),
  );
  check(os, 'caixa sem fiado unpaired after pair/unpair', byId(f.bare)?.tabAccountId == null);
  check(os, 'race caixa kept no Tab after the conflict', byId(f.racer)?.tabAccountId == null);
  check(
    os,
    'freed Tab paired to the donor by the concurrent change',
    byId(f.donor)?.tabAccountId === f.freeTab,
  );
  const grants = (await get(`/operator/${f.admin.operatorId}/accounts`)).operatorAccounts;
  check(
    os,
    'caixa and its Tab granted separately to the self operator',
    grants.some((g) => g.accountId === f.donor) && grants.some((g) => g.accountId === f.freeTab),
    grants.map((g) => ({ type: g.accountType, primary: g.isPrimary })),
  );
  check(
    os,
    'exactly one primary, on the Tab chosen explicitly',
    grants
      .filter((g) => g.isPrimary)
      .map((g) => g.accountId)
      .join() === f.freeTab,
  );
  check(
    os,
    'category created with the chosen direction',
    categories.some((c) => named(c.name, `Despesas F ${label}`) && c.defaultDirection === 'Out'),
  );
  const edited = types.find((t) => t.id === f.type);
  check(os, 'explicit Nenhuma cleared the monthly role', edited?.monthlyFinancialRole === 'None', {
    settlementInstrument: edited?.settlementInstrument,
  });
  check(
    os,
    'duplicate type name refused (one active)',
    types.filter((t) => t.categoryId === f.category && t.name === `Tarifa F ${label}`).length === 1,
  );
  const created = types.find((t) => named(t.name, `Recebimento F ${label}`));
  check(
    os,
    'repayment type stored with explicit NonCash kind',
    created?.categoryId === f.category &&
      created.requiresTabAccountAndClient &&
      created.fiadoPaymentKind === 'NonCash',
  );
  const target = branchUsers.find((m) => m.userId === f.target.userId);
  check(os, 'role change persisted on the exact membership', target?.role === 'Manager');
  check(os, 'offboard navigation removed nothing', target?.active === true);
  const issued = invitations.find((i) => mailed(i.email, `novo-f-${os}`));
  check(
    os,
    'Member invitation issued with the chosen unlinked operator',
    issued?.role === 'Member' && issued.operatorId === f.freeOperators[0],
    { status: issued?.status, delivery: issued?.deliveryStatus },
  );
  const reissued = invitations.find((i) => i.id === f.invitation);
  check(os, 'reissue with Sem operador cleared the preselection', reissued?.operatorId == null, {
    status: reissued?.status,
    delivery: reissued?.deliveryStatus,
  });
  const failed = invitations.find((i) => mailed(i.email, `falha-f-${os}`));
  check(os, 'unreachable SMTP recorded as Failed delivery', failed?.deliveryStatus === 'Failed', {
    role: failed?.role,
  });
  const mailpit = await (
    await fetch(`${s.mailUrl}/api/v1/search?query=${encodeURIComponent(`to:novo-f-${os}`)}`)
  ).json();
  check(os, 'local Mailpit received the issued invitation', mailpit.messages.length >= 1);
}
const report = { apiUrl: s.apiUrl, at: new Date().toISOString(), checks };
await writeFile(output, JSON.stringify(report, null, 2), { mode: 0o600 });
const failedChecks = checks.filter((c) => !c.ok);
console.log(`${checks.length - failedChecks.length}/${checks.length} finance API checks passed.`);
for (const c of failedChecks) console.log(`FAILED ${c.os}: ${c.name}`);
process.exitCode = failedChecks.length ? 1 : 0;
