import { expect, test, type Request } from '@playwright/test';
import {
  branchNames,
  branchSlot,
  branchUrl,
  forgedJwt,
  openBranch,
  putBranchSlot,
  putCookie,
  sessionCookies,
  signIn,
  signOut,
  snapshot,
} from './helpers';
import { apiCall, readState, type StackState } from './stack';

let state: StackState;
test.beforeAll(async () => {
  state = await readState();
});

const today = /^\d{4}-\d{2}-\d{2}$/;

test('gate resolves before any branch request; the deep link waits as an intent', async ({
  page,
  context,
}) => {
  const forwarded: Request[] = [];
  page.on('request', (request) => {
    if (request.url().includes('/bff/api/')) forwarded.push(request);
  });

  const deepLink = `/f/${state.branches.alfa}/e2e/read?filtro=hoje`;
  const gated = await page.goto(`${state.webUrl}${deepLink}`);
  expect(new URL(page.url()).pathname).toBe('/entrar');
  expect(gated?.status()).toBe(404); // Login is a Web M4 screen; M2 only decides the gate.
  expect(forwarded).toHaveLength(0);
  const intent = (await sessionCookies(context, state))['__Host-loto-intent'];
  expect(intent).toMatchObject({ httpOnly: true, secure: true, sameSite: 'Lax', path: '/' });

  // The link names its branch, so the login offers it directly (the gate re-verifies it there).
  const signedIn = await signIn(page, state, state.users.ana);
  expect(signedIn.state).toBe('choose-branch');
  expect(signedIn.resume).toBe(deepLink);
  expect(signedIn.landing).toBe(deepLink);
  expect(signedIn.branches?.map((branch) => branch.name).sort()).toEqual([
    branchNames.alfa,
    branchNames.beta,
  ]);
  const cookies = await sessionCookies(context, state);
  expect(Object.keys(cookies).sort()).toEqual(
    ['__Host-loto-id', '__Host-loto-meta', '__Host-loto-rt'].sort(),
  );
  // The browser's non-secret device id (session-command serialization) is host-only and httpOnly.
  const device = (await context.cookies(state.webUrl)).find((c) => c.name === '__Host-loto-device');
  expect(device).toMatchObject({ httpOnly: true, secure: true, sameSite: 'Lax', path: '/' });
  for (const cookie of Object.values(cookies)) {
    expect(cookie).toMatchObject({
      httpOnly: true,
      secure: true,
      sameSite: 'Lax',
      path: '/',
      domain: 'localhost',
    });
  }
  // Identity-tier read through the forwarder works before any branch exists.
  await page.getByRole('button', { name: 'Ler filiais' }).click();
  await expect(page.getByTestId('identity-read')).toHaveText('filiais:2');
  expect(forwarded.map((request) => new URL(request.url()).pathname)).toEqual([
    '/bff/api/branch/my-branches',
  ]);

  const current = page.waitForRequest((request) =>
    request.url().endsWith('/bff/api/branch/current'),
  );
  const response = await page.goto(`${state.webUrl}${signedIn.landing}`);
  expect(response?.status()).toBe(200);
  expect(response?.headers()['cache-control']).toContain('no-store');
  const serverClock = await page.getByTestId('server-clock').textContent();
  expect(serverClock?.split('|')[0]).toBe(state.branches.alfa);
  expect(serverClock?.split('|')[1]).toMatch(today);
  await expect(page.getByTestId('client-clock')).toHaveText(`${serverClock}|Admin`);
  const sent = await current;
  const headers = await sent.allHeaders();
  expect(headers.authorization).toBeUndefined();
  expect(headers['x-loto-branch']).toBe(state.branches.alfa);
  expect(headers['x-loto-scope']).toBe(await page.getByTestId('client-scope').textContent());
  expect((await (await sent.response())?.allHeaders())?.['cache-control']).toBe(
    'no-store, private',
  );
  // Only the branch this tab opened holds a session cookie, host-only and httpOnly like the rest.
  const slot = (await sessionCookies(context, state))[`__Host-loto-b-${state.branches.alfa}`];
  expect(slot).toMatchObject({ httpOnly: true, secure: true, sameSite: 'Lax', path: '/' });
  expect((await sessionCookies(context, state))['__Host-loto-intent']).toBeUndefined();
});

test('no token is readable by page JavaScript, storage or session JSON', async ({
  page,
  context,
}) => {
  const loginResponse = page.waitForResponse((response) =>
    response.url().endsWith('/bff/session/login'),
  );
  const bia = await signIn(page, state, state.users.bia);
  const body = await (await loginResponse).text();
  await page.goto(`${state.webUrl}${bia.landing.replace('/gestao/fila', '/e2e/read')}`);
  await expect(page.getByTestId('client-clock')).toContainText(state.branches.gama);
  const cookies = await sessionCookies(context, state);
  const slotCookie = cookies[`__Host-loto-b-${state.branches.gama}`]?.value ?? '';
  const secrets = [
    cookies['__Host-loto-id']?.value ?? '',
    cookies['__Host-loto-rt']?.value ?? '',
    branchSlot(cookies, state.branches.gama)?.token ?? '',
    slotCookie,
  ];
  expect(secrets.every((secret) => secret.length > 20)).toBe(true);
  const visible = await page.evaluate(() => ({
    cookie: document.cookie,
    local: JSON.stringify({ ...localStorage }),
    session: JSON.stringify({ ...sessionStorage }),
    html: document.documentElement.outerHTML,
  }));
  expect(visible.cookie).toBe('');
  for (const secret of secrets) {
    for (const surface of [body, visible.local, visible.session, visible.html, page.url()]) {
      expect(surface).not.toContain(secret);
      expect(surface).not.toContain(encodeURIComponent(secret));
    }
  }
});

test('membership rule: zero branches, one branch (auto-open) and role from the session', async ({
  page,
}) => {
  const operator = await signIn(page, state, state.users.operator);
  expect(operator.state).toBe('no-memberships');
  // An unscoped branch address waits for a branch; a branch address not open to them is a 403.
  await page.goto(`${state.webUrl}/transacoes`);
  expect(new URL(page.url()).pathname).toBe('/filiais');
  const foreign = await page.goto(branchUrl(state, state.branches.alfa, '/e2e/read'));
  expect(foreign?.status()).toBe(403);
  await expect(page.getByRole('heading', { name: 'Filial indisponível' })).toBeVisible();

  const carla = await signIn(page, state, state.users.carla);
  expect(carla.state).toBe('branch');
  expect(carla.branch).toMatchObject({ id: state.branches.alfa, role: 'Member' });
  expect(carla.landing).toBe(`/f/${state.branches.alfa}/turno/hoje`);

  const bia = await signIn(page, state, state.users.bia);
  expect(bia).toMatchObject({
    state: 'branch',
    branch: { id: state.branches.gama, role: 'Admin' },
  });
  expect(bia.landing).toBe(`/f/${state.branches.gama}/gestao/fila`);
  expect(bia.scope).not.toBe(carla.scope);
});

test('renewal: reactive after a refused token, proactive in proxy and forwarder, bursts coalesce', async ({
  page,
  context,
}) => {
  test.setTimeout(240_000);
  const alfa = state.branches.alfa;
  await signIn(page, state, state.users.ana);
  const opened = await openBranch(page, branchNames.alfa);
  await page.goto(branchUrl(state, alfa, '/e2e/read'));
  await expect(page.getByTestId('client-clock')).toHaveText(new RegExp(`^${alfa}\\|`));
  const before = await sessionCookies(context, state);

  // Reactive: a branch token the API refuses (valid shape, far expiry) → 401 → one recovery cycle.
  const forged = forgedJwt(Math.floor(Date.now() / 1000) + 3600);
  await putBranchSlot(context, state, alfa, { token: forged });
  const recovered = page.waitForResponse((response) =>
    response.url().endsWith('/bff/api/branch/current'),
  );
  await page.getByRole('button', { name: 'Atualizar' }).click();
  const recoveredResponse = await recovered;
  expect(recoveredResponse.status()).toBe(200);
  expect(recoveredResponse.headers()['x-loto-session']).toBe('renewed');
  const afterReactive = await sessionCookies(context, state);
  expect(afterReactive['__Host-loto-rt']?.value).not.toBe(before['__Host-loto-rt']?.value);
  // The refused token is replaced; the branch generation (the tab's scope) is kept.
  expect(branchSlot(afterReactive, alfa)?.token).not.toBe(forged);
  expect(branchSlot(afterReactive, alfa)?.gen).toBe(branchSlot(before, alfa)?.gen);

  // Proactive in the proxy: past the 30 s margin of the 60 s tokens, a navigation renews first.
  await page.waitForTimeout(31_000);
  await page.goto(branchUrl(state, alfa, '/e2e/read'));
  await expect(page.getByTestId('server-clock')).toContainText(alfa);
  const afterProxy = await sessionCookies(context, state);
  expect(afterProxy['__Host-loto-id']?.value).not.toBe(afterReactive['__Host-loto-id']?.value);
  expect(afterProxy['__Host-loto-rt']?.value).not.toBe(afterReactive['__Host-loto-rt']?.value);

  // A burst of forwarded reads after expiry: one rotation, every read succeeds, session intact.
  await page.waitForTimeout(31_000);
  const statuses = await page.evaluate(
    async ({ scope, branch }) => {
      const read = () =>
        fetch('/bff/api/branch/current', {
          headers: { 'x-loto-bff': '1', 'x-loto-scope': scope ?? '', 'x-loto-branch': branch },
        }).then((response) => response.status);
      return Promise.all(Array.from({ length: 15 }, read));
    },
    { scope: opened.scope, branch: alfa },
  );
  expect(statuses).toEqual(Array.from({ length: 15 }, () => 200));
  const afterBurst = await sessionCookies(context, state);
  expect(afterBurst['__Host-loto-rt']?.value).not.toBe(afterProxy['__Host-loto-rt']?.value);
  await page.goto(branchUrl(state, alfa, '/e2e/read'));
  await expect(page.getByTestId('client-clock')).toHaveText(new RegExp(`^${alfa}\\|`));
  // The burst's rotation is the only valid one: the token it replaced is now refused by the API.
  const call = apiCall(state.apiUrl);
  await expect(
    call('POST', '/user/renew-token', { value: afterProxy['__Host-loto-rt']?.value }),
  ).rejects.toThrow(/401/);
});

test('renewal failure enters the gate; Sair revokes the refresh session on the server', async ({
  page,
  context,
}) => {
  const call = apiCall(state.apiUrl);
  const alfa = state.branches.alfa;
  await signIn(page, state, state.users.ana);
  await openBranch(page, branchNames.alfa);
  const held = await sessionCookies(context, state);
  // Another actor ends this refresh session (as a password change would); tokens then expire.
  await call(
    'POST',
    '/user/logout',
    { refreshToken: held['__Host-loto-rt']?.value },
    held['__Host-loto-id']?.value,
  );
  const past = Math.floor(Date.now() / 1000) - 5;
  await putCookie(context, state, '__Host-loto-id', forgedJwt(past));
  await putBranchSlot(context, state, alfa, { token: forgedJwt(past) });
  await page.goto(branchUrl(state, alfa, '/e2e/read'));
  expect(new URL(page.url()).pathname).toBe('/entrar');
  const left = await sessionCookies(context, state);
  expect(left['__Host-loto-rt']).toBeUndefined();
  expect(left['__Host-loto-id']).toBeUndefined();
  expect(left[`__Host-loto-b-${alfa}`]).toBeUndefined();
  expect(left['__Host-loto-intent']).toBeDefined();

  // The deep link names Alfa: after re-login it is offered for Alfa only — never in Beta.
  const relogged = await signIn(page, state, state.users.ana);
  expect(relogged.state).toBe('choose-branch');
  expect(relogged.resume).toBe(`/f/${alfa}/e2e/read`);
  expect((await openBranch(page, branchNames.beta)).resume).toBeNull();
  const refresh = (await sessionCookies(context, state))['__Host-loto-rt']?.value;
  await page.getByRole('button', { name: 'Sair' }).click();
  await expect(page.getByTestId('outcome')).toContainText('"serverConfirmed":true');
  expect(await sessionCookies(context, state)).toEqual({});
  await expect(call('POST', '/user/renew-token', { value: refresh })).rejects.toThrow(/401/);
});

test('two users stay isolated, in separate browsers and after a user change in one browser', async ({
  browser,
}) => {
  const anaContext = await browser.newContext();
  const biaContext = await browser.newContext();
  const ana = await anaContext.newPage();
  const bia = await biaContext.newPage();
  await signIn(ana, state, state.users.ana);
  await openBranch(ana, branchNames.alfa);
  await signIn(bia, state, state.users.bia);
  const anaAddress = branchUrl(state, state.branches.alfa, '/e2e/read');
  await Promise.all([
    ana.goto(anaAddress),
    bia.goto(branchUrl(state, state.branches.gama, '/e2e/read')),
  ]);
  await expect(ana.getByTestId('client-clock')).toHaveText(
    new RegExp(`^${state.branches.alfa}\\|`),
  );
  await expect(bia.getByTestId('client-clock')).toHaveText(
    new RegExp(`^${state.branches.gama}\\|`),
  );
  await expect(ana.getByTestId('client-session')).toHaveText(
    `${state.users.ana.email}|${branchNames.alfa}`,
  );
  await expect(bia.getByTestId('client-session')).toHaveText(
    `${state.users.bia.email}|${branchNames.gama}`,
  );
  // A copied address does not carry authority: Bia's browser cannot open Ana's branch with it.
  const copied = await bia.goto(anaAddress);
  expect(copied?.status()).toBe(403);

  // The same browser changes user in another tab. Ana's tab learns it (cross-tab notice or its
  // next refused read) and re-reads its branch for the identity now signed in: Bia has no Alfa
  // membership, so the tab ends at the 403 — it never keeps Ana's authority or shows her data. A
  // request still carrying Ana's scope is refused before it reaches the API.
  const anaScope = await ana.getByTestId('client-scope').textContent();
  const otherTab = await anaContext.newPage();
  await signIn(otherTab, state, state.users.bia);
  // No action in Ana's tab is needed: the cross-tab notice makes it re-read and reach the gate.
  await expect(ana.getByRole('heading', { name: 'Filial indisponível' })).toBeVisible();
  expect(ana.url()).toBe(anaAddress);
  const stale = await otherTab.evaluate(
    async ({ scope, branch }) => {
      const response = await fetch('/bff/api/branch/current', {
        headers: { 'x-loto-bff': '1', 'x-loto-scope': scope ?? '', 'x-loto-branch': branch },
      });
      return `${response.status}:${response.headers.get('x-loto-bff-error')}`;
    },
    { scope: anaScope, branch: state.branches.alfa },
  );
  expect(stale).toBe('409:session-scope-changed');
  await anaContext.close();
  await biaContext.close();
});

test('an unscoped branch address waits for a chosen branch and resumes inside it', async ({
  page,
}) => {
  await signIn(page, state, state.users.ana);
  await page.goto(`${state.webUrl}/transacoes?de=2026-10-01`);
  expect(new URL(page.url()).pathname).toBe('/filiais');
  await page.goto(`${state.webUrl}/e2e/session`);
  const beta = await openBranch(page, branchNames.beta);
  // The address carried no tenant, so it resumes in the chosen branch's namespace.
  expect(beta.resume).toBe('/transacoes?de=2026-10-01');
  expect(beta.landing).toBe(`/f/${state.branches.beta}/transacoes?de=2026-10-01`);
  await page.goto(branchUrl(state, state.branches.beta, '/e2e/read'));
  await expect(page.getByTestId('client-clock')).toHaveText(
    new RegExp(`^${state.branches.beta}\\|`),
  );
});

test('the real BFF refuses cross-origin state changes, header-less calls and unknown targets', async ({
  page,
  context,
  playwright,
}) => {
  await signIn(page, state, state.users.bia);
  const storage = await context.storageState();
  const api = await playwright.request.newContext({ baseURL: state.webUrl, storageState: storage });
  const crossOrigin = await api.post('/bff/session/logout', {
    headers: {
      origin: 'https://evil.example',
      'x-loto-bff': '1',
      'content-type': 'application/json',
    },
    data: {},
  });
  expect(crossOrigin.status()).toBe(403);
  const noHeader = await api.post('/bff/session/logout', {
    headers: { origin: state.webUrl, 'content-type': 'application/json' },
    data: {},
  });
  expect(noHeader.status()).toBe(400);
  const unknown = await api.get('/bff/api/user/renew-token', { headers: { 'x-loto-bff': '1' } });
  expect(unknown.status()).toBe(404);
  expect(unknown.headers()['x-loto-bff-error']).toBe('unknown-operation');
  const badBranch = await api.get('/bff/api/branch/current', {
    headers: { 'x-loto-bff': '1', 'x-loto-branch': '../gama' },
  });
  expect(badBranch.status()).toBe(400);
  expect(badBranch.headers()['x-loto-bff-error']).toBe('invalid-branch');
  await api.dispose();
  // The session survived every refused attempt.
  await page.goto(branchUrl(state, state.branches.gama, '/e2e/read'));
  await expect(page.getByTestId('client-clock')).toHaveText(
    new RegExp(`^${state.branches.gama}\\|`),
  );
});

test('cookies of a signed-out session never pass the gate or the forwarder again', async ({
  page,
  context,
}) => {
  const bia = await signIn(page, state, state.users.bia);
  const before = await sessionCookies(context, state);
  await signOut(page);
  // Put the old credentials back, as a lost logout response or a replayed jar would.
  for (const [name, cookie] of Object.entries(before)) {
    await putCookie(context, state, name, encodeURIComponent(cookie.value));
  }
  await page.goto(branchUrl(state, state.branches.gama, '/e2e/read'));
  expect(new URL(page.url()).pathname).toBe('/entrar');
  const status = await page.evaluate(
    async ({ old, branch }) => {
      const response = await fetch('/bff/api/branch/current', {
        headers: { 'x-loto-bff': '1', 'x-loto-scope': old ?? '', 'x-loto-branch': branch },
      });
      return `${response.status}:${response.headers.get('x-loto-bff-error')}`;
    },
    { old: bia.scope, branch: state.branches.gama },
  );
  expect(status).toBe('409:session-scope-changed');
});

test('a session read that started before a login finished never reverts the UI to signed out', async ({
  page,
}) => {
  await page.goto(`${state.webUrl}/e2e/session`);
  let release!: () => void;
  const held = new Promise<void>((done) => (release = done));
  await page.route('**/bff/session', async (route) => {
    // The read reaches the BFF while signed out; its answer is delivered only after the login.
    const response = await route.fetch();
    await held;
    await route.fulfill({ response });
  });
  await page.evaluate(() => window.dispatchEvent(new CustomEvent('loto:session-signal')));
  await page.getByLabel('E-mail').fill(state.users.bia.email);
  await page.getByLabel('Senha').fill(state.users.bia.password);
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect
    .poll(async () => (await snapshot(page)).user?.email ?? null)
    .toBe(state.users.bia.email);
  release();
  await page.waitForTimeout(500);
  expect((await snapshot(page)).user?.email).toBe(state.users.bia.email);
  await page.unroute('**/bff/session');
});
