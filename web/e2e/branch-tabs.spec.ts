// D-M2-1 (owner decision, 2026-10-22): each browser tab keeps its own branch context through its
// `/f/{branchId}` address. Every test here drives TWO OR MORE PAGES OF ONE BROWSER CONTEXT — one
// shared cookie jar, as real tabs share it — against the real local API and PostgreSQL.
import { expect, test, type BrowserContext, type Page, type Request } from '@playwright/test';
import {
  branchNames,
  branchSlot,
  branchUrl,
  openBranch,
  putCookie,
  sessionCookies,
  signIn,
} from './helpers';
import { apiCall, readState, type StackState } from './stack';

let state: StackState;
test.beforeAll(async () => {
  state = await readState();
});

/** Forwarded calls a page makes, with the branch and scope headers they carried. */
function recordForwarded(page: Page) {
  const seen: { path: string; branch: string | undefined; scope: string | undefined }[] = [];
  page.on('request', (request: Request) => {
    const url = new URL(request.url());
    if (!url.pathname.startsWith('/bff/api/')) return;
    const headers = request.headers();
    seen.push({
      path: url.pathname,
      branch: headers['x-loto-branch'],
      scope: headers['x-loto-scope'],
    });
  });
  return seen;
}

async function readFixture(page: Page, branchId: string, query = '') {
  const response = await page.goto(branchUrl(state, branchId, `/e2e/read${query}`));
  await expect(page.getByTestId('client-clock')).toHaveText(new RegExp(`^${branchId}\\|`));
  return response;
}

const scopeOf = async (page: Page) => (await page.getByTestId('client-scope').textContent()) ?? '';

/** Ana signed in once; tab A works in Alfa, tab B in Beta — same cookie jar. */
async function anaInTwoBranches(context: BrowserContext) {
  const a = await context.newPage();
  await signIn(a, state, state.users.ana);
  const b = await context.newPage();
  return { a, b };
}

/** API calls with the branch token this browser holds for `branchId` (test harness only). */
async function apiAsBranch(context: BrowserContext, branchId: string) {
  const token = branchSlot(await sessionCookies(context, state), branchId)?.token;
  return (method: string, route: string) =>
    fetch(`${state.apiUrl}${route}`, { method, headers: { authorization: `Bearer ${token}` } });
}

test('two pages of one browser work in two branches at once, server-rendered and in the browser', async ({
  context,
}) => {
  const { a, b } = await anaInTwoBranches(context);
  const fromA = recordForwarded(a);
  const fromB = recordForwarded(b);
  // Each address opens its own branch — B arrives by deep link, with no picker in between.
  await readFixture(a, state.branches.alfa);
  await readFixture(b, state.branches.beta);
  await expect(a.getByTestId('server-clock')).toContainText(state.branches.alfa);
  await expect(b.getByTestId('server-clock')).toContainText(state.branches.beta);
  await expect(a.getByTestId('client-session')).toHaveText(
    `${state.users.ana.email}|${branchNames.alfa}`,
  );
  await expect(b.getByTestId('client-session')).toHaveText(
    `${state.users.ana.email}|${branchNames.beta}`,
  );

  // Simultaneous server renders: each tab's render reads with its own branch's session.
  await Promise.all([a.reload(), b.reload()]);
  await expect(a.getByTestId('server-clock')).toContainText(state.branches.alfa);
  await expect(b.getByTestId('server-clock')).toContainText(state.branches.beta);

  // One identity, two branch generations; every forwarded call names its own tab's branch.
  const [scopeA, scopeB] = [await scopeOf(a), await scopeOf(b)];
  expect(scopeA.split('.')[0]).toBe(scopeB.split('.')[0]);
  expect(scopeA).not.toBe(scopeB);
  await a.getByRole('button', { name: 'Atualizar' }).click();
  await b.getByRole('button', { name: 'Atualizar' }).click();
  await expect.poll(() => fromA.length).toBeGreaterThan(0);
  await expect.poll(() => fromB.length).toBeGreaterThan(0);
  expect(fromA.every((call) => call.branch === state.branches.alfa && call.scope === scopeA)).toBe(
    true,
  );
  expect(fromB.every((call) => call.branch === state.branches.beta && call.scope === scopeB)).toBe(
    true,
  );
  const cookies = await sessionCookies(context, state);
  expect(branchSlot(cookies, state.branches.alfa)?.token).not.toBe(
    branchSlot(cookies, state.branches.beta)?.token,
  );
});

test('switching branch in one tab moves only that tab; the other keeps its address and filters', async ({
  context,
}) => {
  const { a, b } = await anaInTwoBranches(context);
  await readFixture(a, state.branches.alfa);
  await readFixture(b, state.branches.alfa, '?filtro=semana');
  const scopeB = await scopeOf(b);
  await b.evaluate(() => {
    (window as unknown as { tabMarker: string }).tabMarker = 'b-intacta';
  });

  // Tab A chooses Beta (the picker's command), then navigates itself into Beta's namespace.
  await a.goto(`${state.webUrl}/e2e/session`);
  const beta = await openBranch(a, branchNames.beta);
  expect(beta.landing).toBe(`/f/${state.branches.beta}/gestao/fila`);
  await readFixture(a, state.branches.beta);
  expect(await scopeOf(a)).not.toBe(scopeB);

  // Tab B was not moved, reloaded or reset: same address, filter, page instance and scope.
  expect(b.url()).toBe(branchUrl(state, state.branches.alfa, '/e2e/read?filtro=semana'));
  expect(await b.evaluate(() => (window as unknown as { tabMarker?: string }).tabMarker)).toBe(
    'b-intacta',
  );
  await b.getByRole('button', { name: 'Atualizar' }).click();
  await expect(b.getByTestId('client-clock')).toHaveText(new RegExp(`^${state.branches.alfa}\\|`));
  expect(await scopeOf(b)).toBe(scopeB);

  // Back and forward in tab A follow its own history: Alfa, then Beta again.
  await a.goBack(); // the session fixture
  await a.goBack(); // Alfa
  await expect(a.getByTestId('client-clock')).toHaveText(new RegExp(`^${state.branches.alfa}\\|`));
  await a.goForward();
  await a.goForward();
  await expect(a.getByTestId('client-clock')).toHaveText(new RegExp(`^${state.branches.beta}\\|`));
  await expect(b.getByTestId('client-clock')).toHaveText(new RegExp(`^${state.branches.alfa}\\|`));
});

test('reload, a duplicated tab and a bookmark each open the branch their address names', async ({
  context,
}) => {
  const { a } = await anaInTwoBranches(context);
  await readFixture(a, state.branches.beta, '?filtro=hoje');
  await a.reload();
  await expect(a.getByTestId('client-clock')).toHaveText(new RegExp(`^${state.branches.beta}\\|`));
  // A duplicated tab is a new page at the same address in the same browser.
  const duplicate = await context.newPage();
  await duplicate.goto(a.url());
  await expect(duplicate.getByTestId('client-clock')).toHaveText(
    new RegExp(`^${state.branches.beta}\\|`),
  );
  // A bookmark of the other branch opens that branch, leaving both Beta tabs where they are.
  const bookmark = await context.newPage();
  await readFixture(bookmark, state.branches.alfa);
  await duplicate.getByRole('button', { name: 'Atualizar' }).click();
  await expect(duplicate.getByTestId('client-clock')).toHaveText(
    new RegExp(`^${state.branches.beta}\\|`),
  );
  await expect(a.getByTestId('client-clock')).toHaveText(new RegExp(`^${state.branches.beta}\\|`));
});

test('a branch this person cannot open is a 403 with the API message; other tabs keep working', async ({
  context,
}) => {
  // The API's own answer for a branch without membership (operator: no branch at all).
  const call = apiCall(state.apiUrl);
  const operator = await call<{ responseToken: { token: string } }>(
    'POST',
    '/user/login',
    state.users.operator,
  );
  const reference = await fetch(`${state.apiUrl}/branch/session`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      authorization: `Bearer ${operator.responseToken.token}`,
    },
    body: JSON.stringify({ branchId: state.branches.gama }),
  });
  expect(reference.status).toBe(404);
  const { errorMessages } = (await reference.json()) as { errorMessages: string[] };

  const { a, b } = await anaInTwoBranches(context);
  await readFixture(a, state.branches.alfa);
  const forbidden = await b.goto(branchUrl(state, state.branches.gama, '/e2e/read'));
  expect(forbidden?.status()).toBe(403);
  expect(forbidden?.headers()['cache-control']).toContain('no-store');
  await expect(b.getByRole('heading', { name: 'Filial indisponível' })).toBeVisible();
  for (const message of errorMessages) await expect(b.getByText(message)).toBeVisible();
  await expect(b.getByRole('link', { name: 'Escolher filial' })).toHaveAttribute(
    'href',
    '/filiais',
  );
  expect(branchSlot(await sessionCookies(context, state), state.branches.gama)).toBeNull();

  // The forwarder answers the same for a call naming that branch, without forwarding it.
  const scopeA = await scopeOf(a);
  const refused = await a.evaluate(
    async ({ scope, branch }) => {
      const response = await fetch('/bff/api/branch/current', {
        headers: { 'x-loto-bff': '1', 'x-loto-scope': scope, 'x-loto-branch': branch },
      });
      return `${response.status}:${response.headers.get('x-loto-bff-error')}`;
    },
    { scope: `${scopeA.split('.')[0]}.tab-guess`, branch: state.branches.gama },
  );
  expect(refused).toBe('403:branch-unavailable');
  await a.getByRole('button', { name: 'Atualizar' }).click();
  await expect(a.getByTestId('client-clock')).toHaveText(new RegExp(`^${state.branches.alfa}\\|`));
  expect(await scopeOf(a)).toBe(scopeA);
});

test('Sair in one tab ends every tab; a renewal answer delivered late restores nothing', async ({
  context,
}) => {
  test.setTimeout(180_000);
  const { a, b } = await anaInTwoBranches(context);
  await readFixture(a, state.branches.alfa);
  await readFixture(b, state.branches.beta);
  // Past the 30 s margin of the 60 s tokens, B's next forwarded read renews the identity. Its
  // answer (with the rotated cookies) is captured; after Sair those cookies are written back into
  // the jar, as a response delivered late would.
  await b.waitForTimeout(31_000);
  const late: { name: string; value: string }[] = [];
  await b.route('**/bff/api/branch/current', async (route) => {
    const response = await route.fetch();
    for (const header of response.headersArray()) {
      if (header.name.toLowerCase() !== 'set-cookie') continue;
      const [pair = ''] = header.value.split(';');
      const index = pair.indexOf('=');
      if (/max-age=0/i.test(header.value)) continue;
      late.push({ name: pair.slice(0, index), value: pair.slice(index + 1) });
    }
    await route.fulfill({ response });
  });
  await b.getByRole('button', { name: 'Atualizar' }).click();
  await expect.poll(() => late.some((cookie) => cookie.name === '__Host-loto-rt')).toBe(true);
  await b.unroute('**/bff/api/branch/current');

  await a.goto(`${state.webUrl}/e2e/session`);
  await a.getByRole('button', { name: 'Sair' }).click();
  await expect(a.getByTestId('outcome')).toContainText('"serverConfirmed":true');
  // Every tab of the browser learns it: B, still in Beta, reaches Login without any action.
  await expect(b).toHaveURL(/\/entrar$/);
  for (const cookie of late) await putCookie(context, state, cookie.name, cookie.value);

  // Whatever the late answer wrote into the jar, it authorizes nothing: both branches gate to
  // Login, and the rotated refresh token is refused by the API.
  const jar = await sessionCookies(context, state);
  for (const branchId of [state.branches.alfa, state.branches.beta]) {
    const page = await context.newPage();
    await page.goto(branchUrl(state, branchId, '/e2e/read'));
    expect(new URL(page.url()).pathname).toBe('/entrar');
  }
  const refresh = jar['__Host-loto-rt']?.value;
  expect(refresh).toBeDefined();
  await expect(
    apiCall(state.apiUrl)('POST', '/user/renew-token', { value: refresh }),
  ).rejects.toThrow(/401/);
});

test('an uncertain write stays in its branch: it is never verified or replayed under another', async ({
  context,
}) => {
  const { a, b } = await anaInTwoBranches(context);
  await a.goto(branchUrl(state, state.branches.alfa, '/e2e/writes'));
  await b.goto(branchUrl(state, state.branches.beta, '/e2e/writes'));
  const alfaApi = await apiAsBranch(context, state.branches.alfa);
  const betaApi = await apiAsBranch(context, state.branches.beta);
  const current = (await (await alfaApi('GET', '/branch/current')).json()) as {
    branchLocalDate: string;
  };
  const day = current.branchLocalDate;
  const count = async (api: typeof alfaApi) =>
    (
      (await (await api('GET', `/transaction?DateFrom=${day}&DateTo=${day}`)).json()) as {
        totalCount: number;
      }
    ).totalCount;
  const [alfaBefore, betaBefore] = [await count(alfaApi), await count(betaApi)];

  await expect(a.getByTestId('operation-ready')).toHaveText('pronto');
  for (const [name, value] of Object.entries({
    accountId: state.transaction.accountId,
    transactionTypeId: state.transaction.transactionTypeId,
    operatorId: state.transaction.operatorId,
    value: '7.89',
    date: day,
  })) {
    await a.getByLabel(name, { exact: true }).fill(value);
  }
  const sent: { key: string | undefined; body: string | null; branch: string | undefined }[] = [];
  await a.route('**/bff/api/transaction', async (route) => {
    const request = route.request();
    sent.push({
      key: request.headers()['idempotency-key'],
      body: request.postData(),
      branch: request.headers()['x-loto-branch'],
    });
    await route.fetch();
    await route.abort('connectionreset');
  });
  await a.getByRole('button', { name: 'Registrar' }).click();
  await expect(a.getByTestId('operation-result')).toContainText('"kind":"uncertain"');
  await a.unroute('**/bff/api/transaction');
  expect(sent[0]?.branch).toBe(state.branches.alfa);
  expect(await count(alfaApi)).toBe(alfaBefore + 1);

  // The other tab (Beta) neither sees nor can verify Alfa's unresolved operation.
  const posted: string[] = [];
  for (const page of [a, b]) {
    page.on('request', (request) => {
      if (request.method() === 'POST' && request.url().endsWith('/bff/api/transaction')) {
        posted.push(request.headers()['x-loto-branch'] ?? '-');
      }
    });
  }
  await expect(b.getByTestId('operation-ready')).toHaveText('pronto');
  await expect(b.getByTestId('operation-record')).toHaveText('null');
  await b.getByRole('button', { name: 'Verificar e tentar novamente' }).click();
  await expect(b.getByTestId('operation-result')).toContainText('nothing-to-retry');
  // Tab A itself moves to Beta: the record stays behind, in Alfa's scope.
  await a.goto(branchUrl(state, state.branches.beta, '/e2e/writes'));
  await expect(a.getByTestId('operation-ready')).toHaveText('pronto');
  await expect(a.getByTestId('operation-record')).toHaveText('null');
  await a.getByRole('button', { name: 'Verificar e tentar novamente' }).click();
  await expect(a.getByTestId('operation-result')).toContainText('nothing-to-retry');
  expect(posted).toEqual([]);

  // Back in Alfa, the same key and bytes are verified — in Alfa, and only there.
  await a.goto(branchUrl(state, state.branches.alfa, '/e2e/writes'));
  await expect(a.getByTestId('operation-record')).toContainText(sent[0]?.key ?? 'missing');
  const replay = a.waitForRequest(
    (request) => request.method() === 'POST' && request.url().endsWith('/bff/api/transaction'),
  );
  await a.getByRole('button', { name: 'Verificar e tentar novamente' }).click();
  const replayed = await replay;
  expect(replayed.headers()['idempotency-key']).toBe(sent[0]?.key);
  expect(replayed.headers()['x-loto-branch']).toBe(state.branches.alfa);
  expect(replayed.postData()).toBe(sent[0]?.body);
  await expect(a.getByTestId('operation-result')).toContainText('"kind":"confirmed"');
  expect(await count(alfaApi)).toBe(alfaBefore + 1);
  expect(await count(betaApi)).toBe(betaBefore);
});

test('another person signing in on this browser never inherits an open tab’s branch authority', async ({
  context,
}) => {
  const { a, b } = await anaInTwoBranches(context);
  await readFixture(a, state.branches.alfa);
  await expect(a.getByTestId('client-clock')).toHaveText(new RegExp(`\\|Admin$`));
  const anaScope = await scopeOf(a);
  // Carla (a Member of Alfa) signs in in another tab of the same browser.
  await signIn(b, state, state.users.carla);
  // Tab A re-reads its branch for the identity now holding the cookies: Carla's own session for
  // Alfa, with her role — never Ana's Admin authority carried over by the open address.
  await expect(a.getByTestId('client-session')).toHaveText(
    `${state.users.carla.email}|${branchNames.alfa}`,
  );
  await a.getByRole('button', { name: 'Atualizar' }).click();
  await expect(a.getByTestId('client-clock')).toHaveText(
    new RegExp(`^${state.branches.alfa}\\|.*\\|Member$`),
  );
  expect(await scopeOf(a)).not.toBe(anaScope);
  expect((await scopeOf(a)).split('.')[0]).not.toBe(anaScope.split('.')[0]);
});
