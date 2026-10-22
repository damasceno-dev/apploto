import { expect, test, type Page } from '@playwright/test';
import { branchNames, openBranch, sessionCookies, signIn, signOut } from './helpers';
import { readState, type StackState } from './stack';

let state: StackState;
test.beforeAll(async () => {
  state = await readState();
});

/** Calls the API directly with the branch token the browser currently holds (test harness only). */
async function asBrowserBranch(page: Page) {
  const token = (await sessionCookies(page.context(), state))['__Host-loto-br']?.value;
  return (method: string, route: string, body?: unknown, headers: Record<string, string> = {}) =>
    fetch(`${state.apiUrl}${route}`, {
      method,
      headers: {
        authorization: `Bearer ${token}`,
        ...(body === undefined ? {} : { 'content-type': 'application/json' }),
        ...headers,
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
}

async function signInAlfa(page: Page) {
  await signIn(page, state, state.users.ana);
  await openBranch(page, branchNames.alfa);
  await page.goto(`${state.webUrl}/e2e/branch/writes`);
}

test('guarded update: ETag to If-Match, stale 409 verbatim with input kept and a reload path', async ({
  page,
}) => {
  await signInAlfa(page);
  await expect(page.getByTestId('setting-version')).toHaveText(/^\d+$/);
  const loaded = Number(await page.getByTestId('setting-version').textContent());
  const sent = page.waitForRequest(
    (request) => request.method() === 'PUT' && request.url().endsWith('/bff/api/setting'),
  );
  await page.getByLabel('Horas').fill('7');
  await page.getByRole('button', { name: 'Salvar configuração' }).click();
  expect((await (await sent).allHeaders())['if-match']).toBe(`"${loaded}"`);
  await expect(page.getByTestId('setting-result')).toContainText('"kind":"saved"');
  const saved = Number(await page.getByTestId('setting-version').textContent());
  expect(saved).not.toBe(loaded);

  // Another actor saves first; the API's own stale answer is the reference for "verbatim".
  const api = await asBrowserBranch(page);
  const other = await api(
    'PUT',
    '/setting',
    { dailyTargetHours: '9' },
    { 'If-Match': `"${saved}"` },
  );
  expect(other.status).toBe(200);
  const reference = await api(
    'PUT',
    '/setting',
    { dailyTargetHours: '5' },
    { 'If-Match': `"${saved}"` },
  );
  expect(reference.status).toBe(409);
  const { errorMessages } = (await reference.json()) as { errorMessages: string[] };

  await page.getByLabel('Horas').fill('6');
  await page.getByRole('button', { name: 'Salvar configuração' }).click();
  await expect(page.getByTestId('setting-result')).toHaveText(
    JSON.stringify({ kind: 'conflict', messages: errorMessages, recovery: 'reload-and-review' }),
  );
  await expect(page.getByLabel('Horas')).toHaveValue('6');
  await page.getByRole('button', { name: 'Recarregar e conferir' }).click();
  await expect(page.getByTestId('setting-version')).not.toHaveText(String(saved));
  await page.getByRole('button', { name: 'Salvar configuração' }).click();
  await expect(page.getByTestId('setting-result')).toContainText('"kind":"saved"');
});

test('idempotent create: a lost response keeps key and bytes across reload; verification replays once', async ({
  page,
}) => {
  await signInAlfa(page);
  const api = await asBrowserBranch(page);
  const current = (await (await api('GET', '/branch/current')).json()) as {
    branchLocalDate: string;
  };
  const query = `/transaction?AccountId=${state.transaction.accountId}&DateFrom=${current.branchLocalDate}&DateTo=${current.branchLocalDate}`;
  const count = async () =>
    ((await (await api('GET', query)).json()) as { totalCount: number }).totalCount;
  const before = await count();

  await expect(page.getByTestId('operation-ready')).toHaveText('pronto');
  const fillForm = async () => {
    for (const [name, value] of Object.entries({
      accountId: state.transaction.accountId,
      transactionTypeId: state.transaction.transactionTypeId,
      operatorId: state.transaction.operatorId,
      value: '12.34',
      date: current.branchLocalDate,
    })) {
      await page.getByLabel(name, { exact: true }).fill(value);
    }
  };
  await fillForm();

  // The request reaches the API and commits; the browser loses the answer.
  const sent: { key: string | null; body: string | null }[] = [];
  await page.route('**/bff/api/transaction', async (route) => {
    const request = route.request();
    sent.push({ key: request.headers()['idempotency-key'] ?? null, body: request.postData() });
    await route.fetch();
    await route.abort('connectionreset');
  });
  await page.getByRole('button', { name: 'Registrar' }).click();
  await expect(page.getByTestId('operation-result')).toContainText('"kind":"uncertain"');
  expect(await count()).toBe(before + 1);
  await page.unroute('**/bff/api/transaction');

  await page.reload();
  const record = JSON.parse(
    (await page.getByTestId('operation-record').textContent()) ?? 'null',
  ) as {
    status: string;
    key: string;
  };
  expect(record).toMatchObject({ status: 'uncertain', key: sent[0]?.key });

  const replay = page.waitForRequest(
    (request) => request.method() === 'POST' && request.url().endsWith('/bff/api/transaction'),
  );
  await page.getByRole('button', { name: 'Verificar e tentar novamente' }).click();
  const replayed = await replay;
  expect(replayed.headers()['idempotency-key']).toBe(sent[0]?.key);
  expect(replayed.postData()).toBe(sent[0]?.body);
  await expect(page.getByTestId('operation-result')).toContainText('"kind":"confirmed"');
  await expect(page.getByTestId('operation-record')).toHaveText('null');
  expect(await count()).toBe(before + 1);

  // The next intent (typed again after the reload) is a new operation with a new key.
  await fillForm();
  const next = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' && response.url().endsWith('/bff/api/transaction'),
  );
  await page.getByRole('button', { name: 'Registrar' }).click();
  const created = await next;
  expect(created.status()).toBe(201);
  expect(created.request().headers()['idempotency-key']).not.toBe(sent[0]?.key);
  await expect(page.getByTestId('operation-result')).toContainText('"kind":"confirmed"');
  expect(await count()).toBe(before + 2);
});

test('device input survives reload and re-login for the same person only', async ({
  page,
  context,
}) => {
  await signInAlfa(page);
  await expect(page.getByTestId('draft-state')).toHaveText('none');
  await page.locator('textarea[name="notes"]').fill('Contagem parcial: 3 maços');
  await page.reload();
  await expect(page.getByTestId('draft-state')).toHaveText('found');
  await expect(page.locator('textarea[name="notes"]')).toHaveValue('Contagem parcial: 3 maços');

  const storage = await page.evaluate(() => JSON.stringify({ ...localStorage }));
  const cookies = await sessionCookies(context, state);
  for (const secret of [
    cookies['__Host-loto-id']?.value,
    cookies['__Host-loto-rt']?.value,
    cookies['__Host-loto-br']?.value,
  ]) {
    expect(storage).not.toContain(secret ?? '-');
  }
  expect(storage).not.toContain(state.users.ana.email);

  await page.goto(`${state.webUrl}/e2e/session`);
  await signOut(page);
  await signIn(page, state, state.users.bia);
  await page.goto(`${state.webUrl}/e2e/branch/writes`);
  await expect(page.getByTestId('draft-state')).toHaveText('none');
  await expect(page.locator('textarea[name="notes"]')).toHaveValue('');

  await page.goto(`${state.webUrl}/e2e/session`);
  await signOut(page);
  await signInAlfa(page);
  await expect(page.getByTestId('draft-state')).toHaveText('found');
  await expect(page.locator('textarea[name="notes"]')).toHaveValue('Contagem parcial: 3 maços');
});
