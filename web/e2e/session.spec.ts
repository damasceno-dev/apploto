import { expect, test, type Request } from '@playwright/test';
import {
  branchNames,
  forgedJwt,
  openBranch,
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

  const gated = await page.goto(`${state.webUrl}/e2e/branch/read?filtro=hoje`);
  expect(new URL(page.url()).pathname).toBe('/entrar');
  expect(gated?.status()).toBe(404); // Login is a Web M4 screen; M2 only decides the gate.
  expect(forwarded).toHaveLength(0);
  const intent = (await sessionCookies(context, state))['__Host-loto-intent'];
  expect(intent).toMatchObject({ httpOnly: true, secure: true, sameSite: 'Lax', path: '/' });

  const signedIn = await signIn(page, state, state.users.ana);
  expect(signedIn.state).toBe('choose-branch');
  expect(signedIn.branches?.map((branch) => branch.name).sort()).toEqual([
    branchNames.alfa,
    branchNames.beta,
  ]);
  const cookies = await sessionCookies(context, state);
  expect(Object.keys(cookies).sort()).toEqual(
    ['__Host-loto-id', '__Host-loto-intent', '__Host-loto-meta', '__Host-loto-rt'].sort(),
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

  const opened = await openBranch(page, branchNames.alfa);
  expect(opened.state).toBe('branch');
  expect(opened.resume).toBe('/e2e/branch/read?filtro=hoje');
  expect((await sessionCookies(context, state))['__Host-loto-intent']).toBeUndefined();

  const current = page.waitForRequest((request) =>
    request.url().endsWith('/bff/api/branch/current'),
  );
  const response = await page.goto(`${state.webUrl}${opened.resume}`);
  expect(response?.headers()['cache-control']).toContain('no-store');
  const serverClock = await page.getByTestId('server-clock').textContent();
  expect(serverClock?.split('|')[0]).toBe(state.branches.alfa);
  expect(serverClock?.split('|')[1]).toMatch(today);
  await expect(page.getByTestId('client-clock')).toHaveText(`${serverClock}|Admin`);
  const sent = await current;
  const headers = await sent.allHeaders();
  expect(headers.authorization).toBeUndefined();
  expect(headers['x-loto-scope']).toBe(opened.scope);
  expect((await (await sent.response())?.allHeaders())?.['cache-control']).toBe(
    'no-store, private',
  );
});

test('no token is readable by page JavaScript, storage or session JSON', async ({
  page,
  context,
}) => {
  const loginResponse = page.waitForResponse((response) =>
    response.url().endsWith('/bff/session/login'),
  );
  await signIn(page, state, state.users.bia);
  const body = await (await loginResponse).text();
  const cookies = await sessionCookies(context, state);
  const secrets = ['__Host-loto-id', '__Host-loto-rt', '__Host-loto-br'].map(
    (name) => cookies[name]?.value ?? '',
  );
  expect(secrets.every((secret) => secret.length > 20)).toBe(true);
  const visible = await page.evaluate(() => ({
    cookie: document.cookie,
    local: JSON.stringify({ ...localStorage }),
    session: JSON.stringify({ ...sessionStorage }),
    html: document.documentElement.outerHTML,
  }));
  expect(visible.cookie).toBe('');
  for (const secret of secrets) {
    for (const surface of [body, visible.local, visible.session, visible.html]) {
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
  await page.goto(`${state.webUrl}/e2e/branch/read`);
  expect(new URL(page.url()).pathname).toBe('/filiais');

  const carla = await signIn(page, state, state.users.carla);
  expect(carla.state).toBe('branch');
  expect(carla.branch).toMatchObject({ id: state.branches.alfa, role: 'Member' });

  const bia = await signIn(page, state, state.users.bia);
  expect(bia).toMatchObject({
    state: 'branch',
    branch: { id: state.branches.gama, role: 'Admin' },
  });
  expect(bia.scope).not.toBe(carla.scope);
});

test('renewal: reactive after a refused token, proactive in proxy and forwarder, bursts coalesce', async ({
  page,
  context,
}) => {
  test.setTimeout(240_000);
  await signIn(page, state, state.users.ana);
  const opened = await openBranch(page, branchNames.alfa);
  await page.goto(`${state.webUrl}/e2e/branch/read`);
  await expect(page.getByTestId('client-clock')).toHaveText(
    new RegExp(`^${state.branches.alfa}\\|`),
  );
  const before = await sessionCookies(context, state);

  // Reactive: a branch token the API refuses (valid shape, far expiry) → 401 → one recovery cycle.
  const forged = forgedJwt(Math.floor(Date.now() / 1000) + 3600);
  await putCookie(context, state, '__Host-loto-br', forged);
  const recovered = page.waitForResponse((response) =>
    response.url().endsWith('/bff/api/branch/current'),
  );
  await page.getByRole('button', { name: 'Atualizar' }).click();
  const recoveredResponse = await recovered;
  expect(recoveredResponse.status()).toBe(200);
  expect(recoveredResponse.headers()['x-loto-session']).toBe('renewed');
  const afterReactive = await sessionCookies(context, state);
  expect(afterReactive['__Host-loto-rt']?.value).not.toBe(before['__Host-loto-rt']?.value);
  // The refused token is replaced. (API JWTs have no unique id: one minted in the same second as
  // the original is byte-identical to it, so the new value may equal `before`.)
  expect(afterReactive['__Host-loto-br']?.value).not.toBe(forged);

  // Proactive in the proxy: past the 30 s margin of the 60 s tokens, a navigation renews first.
  await page.waitForTimeout(31_000);
  await page.goto(`${state.webUrl}/e2e/branch/read`);
  await expect(page.getByTestId('server-clock')).toContainText(state.branches.alfa);
  const afterProxy = await sessionCookies(context, state);
  expect(afterProxy['__Host-loto-id']?.value).not.toBe(afterReactive['__Host-loto-id']?.value);
  expect(afterProxy['__Host-loto-rt']?.value).not.toBe(afterReactive['__Host-loto-rt']?.value);

  // A burst of forwarded reads after expiry: one rotation, every read succeeds, session intact.
  await page.waitForTimeout(31_000);
  const statuses = await page.evaluate(async (scope) => {
    const read = () =>
      fetch('/bff/api/branch/current', {
        headers: { 'x-loto-bff': '1', 'x-loto-scope': scope ?? '' },
      }).then((response) => response.status);
    return Promise.all(Array.from({ length: 15 }, read));
  }, opened.scope);
  expect(statuses).toEqual(Array.from({ length: 15 }, () => 200));
  const afterBurst = await sessionCookies(context, state);
  expect(afterBurst['__Host-loto-rt']?.value).not.toBe(afterProxy['__Host-loto-rt']?.value);
  await page.goto(`${state.webUrl}/e2e/branch/read`);
  await expect(page.getByTestId('client-clock')).toHaveText(
    new RegExp(`^${state.branches.alfa}\\|`),
  );
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
  await putCookie(context, state, '__Host-loto-br', forgedJwt(past));
  await page.goto(`${state.webUrl}/e2e/branch/read`);
  expect(new URL(page.url()).pathname).toBe('/entrar');
  const left = await sessionCookies(context, state);
  expect(left['__Host-loto-rt']).toBeUndefined();
  expect(left['__Host-loto-id']).toBeUndefined();
  expect(left['__Host-loto-intent']).toBeDefined();

  // The deep link was gated while Alfa's session was lost: it is bound to Alfa, so it does not
  // resume in another branch.
  const relogged = await signIn(page, state, state.users.ana);
  expect(relogged.state).toBe('choose-branch');
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
  await Promise.all([
    ana.goto(`${state.webUrl}/e2e/branch/read`),
    bia.goto(`${state.webUrl}/e2e/branch/read`),
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

  // The same browser changes user in another tab. The first tab learns it (cross-tab notice or
  // its next refused read), drops Ana's scope and shows only Bia's data; a request still carrying
  // Ana's scope is refused before it reaches the API.
  const anaScope = await ana.evaluate(async () => {
    const response = await fetch('/bff/session', { headers: { 'x-loto-bff': '1' } });
    return ((await response.json()) as { scope: string }).scope;
  });
  const otherTab = await anaContext.newPage();
  await signIn(otherTab, state, state.users.bia);
  await ana.getByRole('button', { name: 'Atualizar' }).click();
  await expect(ana.getByTestId('client-session')).toHaveText(
    `${state.users.bia.email}|${branchNames.gama}`,
  );
  await expect(ana.getByTestId('client-clock')).toHaveText(
    new RegExp(`^${state.branches.gama}\\|`),
  );
  const stale = await ana.evaluate(async (scope) => {
    const response = await fetch('/bff/api/branch/current', {
      headers: { 'x-loto-bff': '1', 'x-loto-scope': scope },
    });
    return `${response.status}:${response.headers.get('x-loto-bff-error')}`;
  }, anaScope);
  expect(stale).toBe('409:session-scope-changed');
  await anaContext.close();
  await biaContext.close();
});

test('same-user branch switch drops the old branch and opens the new one', async ({
  page,
  context,
}) => {
  await signIn(page, state, state.users.ana);
  await openBranch(page, branchNames.alfa);
  await page.goto(`${state.webUrl}/e2e/branch/read`);
  await expect(page.getByTestId('client-clock')).toHaveText(
    new RegExp(`^${state.branches.alfa}\\|`),
  );
  await page.goto(`${state.webUrl}/e2e/session`);
  await page.getByRole('button', { name: 'Trocar filial' }).click();
  await expect.poll(async () => (await snapshot(page)).state).toBe('choose-branch');
  expect((await sessionCookies(context, state))['__Host-loto-br']).toBeUndefined();
  await page.goto(`${state.webUrl}/e2e/branch/read`);
  expect(new URL(page.url()).pathname).toBe('/filiais');
  await page.goto(`${state.webUrl}/e2e/session`);
  const beta = await openBranch(page, branchNames.beta);
  // The deep link was requested with no branch selected, so it resumes in the chosen branch (it
  // carries no tenant); one captured from a branch session is bound to it (failure test above).
  expect(beta.resume).toBe('/e2e/branch/read');
  await page.goto(`${state.webUrl}/e2e/branch/read`);
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
  await api.dispose();
  // The session survived every refused attempt.
  await page.goto(`${state.webUrl}/e2e/branch/read`);
  await expect(page.getByTestId('client-clock')).toHaveText(
    new RegExp(`^${state.branches.gama}\\|`),
  );
});

test('cookies of a signed-out session never pass the gate or the forwarder again', async ({
  page,
  context,
}) => {
  await signIn(page, state, state.users.bia);
  const before = await sessionCookies(context, state);
  const scope = (await snapshot(page)).scope;
  await signOut(page);
  // Put the old credentials back, as a lost logout response or a replayed jar would.
  for (const [name, cookie] of Object.entries(before)) {
    await putCookie(context, state, name, encodeURIComponent(cookie.value));
  }
  await page.goto(`${state.webUrl}/e2e/branch/read`);
  expect(new URL(page.url()).pathname).toBe('/entrar');
  const status = await page.evaluate(async (old) => {
    const response = await fetch('/bff/api/branch/current', {
      headers: { 'x-loto-bff': '1', 'x-loto-scope': old ?? '' },
    });
    return `${response.status}:${response.headers.get('x-loto-bff-error')}`;
  }, scope);
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
