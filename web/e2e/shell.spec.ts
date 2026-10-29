import { createRequire } from 'node:module';
import { mkdtemp } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { expect, test, type Page } from '@playwright/test';
import { apiCall, readState, type StackState, type StackUser } from './stack';
import { branchUrl } from './helpers';
let state: StackState;
test.beforeAll(async () => {
  state = await readState();
});
async function login(page: Page, user: StackUser) {
  await page.goto(`${state.webUrl}/entrar`);
  await page.getByLabel('E-mail', { exact: true }).fill(user.email);
  await page.getByLabel('Senha', { exact: true }).fill(user.password);
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page).not.toHaveURL(/\/entrar$/);
}
test('real password entry and zero, one and many memberships', async ({ page }) => {
  await login(page, state.users.operator);
  await expect(page.locator('[data-kind=setup]')).toContainText(
    'Você ainda não tem acesso a uma lotérica.',
  );
  await expect(page.locator('[data-kind=setup]')).toContainText(
    'Abra o link do convite recebido por e-mail.',
  );
  await login(page, state.users.bia);
  await expect(page).toHaveURL(new RegExp(`/f/${state.branches.gama}/gestao/fila$`));
  await expect(page.getByRole('heading', { name: 'Fila de trabalho', exact: true })).toBeVisible();
  await login(page, state.users.ana);
  await expect(page.getByRole('heading', { name: 'Escolha a filial' })).toBeVisible();
  await expect(
    page.getByRole('button', { name: /Lotérica Alfa E2E.*Administrador/ }),
  ).toBeVisible();
  await page.getByRole('button', { name: /Lotérica Alfa E2E.*Administrador/ }).click();
  await expect(page).toHaveURL(new RegExp(`/f/${state.branches.alfa}/gestao/fila$`));
});
for (const [key, linked, role] of [
  ['carla', false, 'Member'],
  ['memberLinked', true, 'Member'],
  ['manager', false, 'Manager'],
  ['managerLinked', true, 'Manager'],
  ['ana', false, 'Admin'],
  ['adminLinked', true, 'Admin'],
] as const) {
  test(`real ${role} ${linked ? 'linked' : 'unlinked'} navigation and direct-entry policy`, async ({
    page,
  }) => {
    await login(page, state.users[key]);
    await page.goto(
      branchUrl(state, state.branches.alfa, role === 'Member' ? '/turno/hoje' : '/gestao/fila'),
    );
    const nav = page.getByRole('navigation', { name: 'Navegação principal' });
    if (role === 'Member') {
      await expect(nav.getByRole('link', { name: 'Ponto', exact: true })).toBeVisible();
      await expect(nav.getByRole('link', { name: 'Dashboard', exact: true })).toHaveCount(0);
      await page.goto(branchUrl(state, state.branches.alfa, '/gestao/contas'));
      await expect(page.getByRole('heading', { name: 'Acesso indisponível' })).toBeVisible();
    } else {
      await expect(nav.getByRole('link', { name: 'Dashboard', exact: true })).toBeVisible();
      await expect(nav.getByRole('link', { name: 'Ponto', exact: true })).toHaveCount(0);
      await page.goto(branchUrl(state, state.branches.alfa, '/fechamentos/abrir'));
      await expect(page.getByRole('heading', { name: 'Abrir dia', exact: true })).toBeVisible();
      await page.goto(branchUrl(state, state.branches.alfa, '/turno/hoje'));
      if (!linked)
        await expect(
          page.getByText('Meu turno não está disponível para você nesta filial.'),
        ).toBeVisible();
      else
        await expect(
          page.getByText('Meu turno não está disponível para você nesta filial.'),
        ).toHaveCount(0);
    }
  });
}
test('one browser identity keeps X/X and X/Y independent during real shell branch switch', async ({
  page,
  context,
}) => {
  await login(page, state.users.ana);
  await page.goto(branchUrl(state, state.branches.alfa, '/transacoes'));
  const second = await context.newPage();
  await second.goto(branchUrl(state, state.branches.alfa, '/transacoes'));
  await page.getByRole('button', { name: 'Trocar filial', exact: true }).click();
  await page.getByRole('button', { name: /Lotérica Beta E2E.*Administrador/ }).click();
  await expect(page).toHaveURL(new RegExp(`/f/${state.branches.beta}/gestao/fila$`));
  await expect(second).toHaveURL(new RegExp(`/f/${state.branches.alfa}/transacoes$`));
  await second.reload();
  await expect(second.getByRole('heading', { name: 'Transações', exact: true })).toBeVisible();
  await second.getByRole('button', { name: /Conta de/ }).click();
  await second.getByRole('menuitem', { name: 'Sair', exact: true }).click();
  await expect(second).toHaveURL(/\/entrar$/);
  await page.reload();
  await expect(page).toHaveURL(/\/entrar$/);
});
test('compact shell drawer supports Escape and keyboard focus return', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await login(page, state.users.managerLinked);
  const menu = page.getByRole('button', { name: 'Menu', exact: true });
  await menu.click();
  await expect(page.getByRole('dialog', { name: 'Menu de navegação' })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(menu).toBeFocused();
  await expect(page.getByRole('dialog', { name: 'Menu de navegação' })).toHaveCount(0);
});

test('every visible management destination resolves to its protected catalog placeholder', async ({
  page,
}) => {
  await login(page, state.users.ana);
  await page.goto(branchUrl(state, state.branches.alfa, '/gestao/fila'));
  const nav = page.getByRole('navigation', { name: 'Navegação principal' });
  await expect(nav.getByRole('link', { name: 'Dashboard', exact: true })).toBeVisible();
  const links = await nav
    .getByRole('link')
    .evaluateAll((nodes) =>
      nodes.map((node) => ({ href: node.getAttribute('href')!, title: node.textContent! })),
    );
  expect(links.filter((link) => link.href.includes('/relatorios/'))).toHaveLength(9);
  for (const link of links) {
    const response = await page.goto(`${state.webUrl}${link.href}`);
    expect(response?.status()).toBe(200);
    await expect(page.getByRole('heading', { name: link.title, exact: true })).toBeVisible();
    await expect(
      page.getByText('Esta função estará disponível em uma próxima etapa.'),
    ).toBeVisible();
  }
  const unknown = await page.goto(branchUrl(state, state.branches.alfa, '/gestao/inventado'));
  expect(unknown?.status()).toBe(404);
});

test('responsive themes, reduced motion and supplementary axe checks use the actual shell', async ({
  page,
}) => {
  const require = createRequire(import.meta.url);
  const captures = await mkdtemp(path.join(tmpdir(), 'loto-m4-visual-'));
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await login(page, state.users.ana);
  for (const width of [1440, 900, 375]) {
    await page.setViewportSize({ width, height: 1000 });
    await page.goto(branchUrl(state, state.branches.alfa, '/gestao/fila'));
    for (const theme of ['light', 'dark', 'system']) {
      await page.evaluate((preference) => {
        document.cookie = `__Host-loto-theme=${preference};Path=/;Secure;SameSite=Lax`;
      }, theme);
      await page.reload();
      await expect(page.locator('html')).toHaveAttribute('data-theme-preference', theme);
      await expect(
        page.getByRole('heading', { name: 'Fila de trabalho', exact: true }),
      ).toBeVisible();
      await expect(page.getByTestId('branch-clock')).toContainText(
        /\d{2}\/\d{2}\/\d{4}.*horário da filial/,
      );
      await page.screenshot({
        path: path.join(captures, `shell-${width}-${theme}.png`),
        fullPage: true,
      });
      await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') });
      const violations = await page.evaluate(async () => {
        const axe = (
          window as unknown as {
            axe: {
              run: (
                context: unknown,
                options: unknown,
              ) => Promise<{ violations: { id: string }[] }>;
            };
          }
        ).axe;
        const result = await axe.run(document, {
          runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'] },
        });
        return result.violations.map((item) => item.id);
      });
      expect(violations).toEqual([]);
    }
  }
  await page.evaluate(() => {
    document.documentElement.style.fontSize = '200%';
  });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

async function administrator() {
  const call = apiCall(state.apiUrl);
  const identity = await call<{ responseToken: { token: string } }>(
    'POST',
    '/user/login',
    state.users.ana,
  );
  const branch = await call<{ token: string }>(
    'POST',
    '/branch/session',
    { branchId: state.branches.alfa },
    identity.responseToken.token,
  );
  return { call, token: branch.token };
}
test('linked manager mode survives shared-route reload and browser back', async ({ page }) => {
  await login(page, state.users.managerLinked);
  await page.getByRole('button', { name: /Conta de/ }).click();
  await page.getByRole('menuitemradio', { name: 'Meu turno', exact: true }).click();
  await expect(page).toHaveURL(/\/turno\/hoje$/);
  await page
    .getByRole('navigation', { name: 'Navegação principal' })
    .getByRole('link', { name: 'Transações', exact: true })
    .click();
  await expect(page.getByRole('heading', { name: 'Transações', exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Transações', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /Conta de.*Meu turno/ })).toBeVisible();
  expect(
    await page.evaluate(() =>
      Object.keys(JSON.parse(sessionStorage.getItem('loto-navigation-v2') ?? '{}')).some((key) =>
        key.includes('@'),
      ),
    ),
  ).toBe(false);
  await page.goBack();
  await expect(page).toHaveURL(/\/turno\/hoje$/);
});
test('foreground rechecks account loss and link loss without claiming removal history', async ({
  page,
}) => {
  await login(page, state.users.memberLinked);
  await expect(page.getByRole('heading', { name: 'Hoje', exact: true })).toBeVisible();
  const admin = await administrator();
  const list = await admin.call<{
    operators: { id: string; name: string; userId: string | null }[];
  }>('GET', '/operator', undefined, admin.token);
  const memberships = await admin.call<{ branchUsers: { userId: string; email: string }[] }>(
    'GET',
    '/branch/users',
    undefined,
    admin.token,
  );
  const user = memberships.branchUsers.find(
    (item) => item.email === state.users.memberLinked.email,
  )!;
  const employee = list.operators.find((item) => item.userId === user.userId)!;
  const accounts = await admin.call<{ operatorAccounts: { accountId: string }[] }>(
    'GET',
    `/operator/${employee.id}/accounts`,
    undefined,
    admin.token,
  );
  try {
    for (const account of accounts.operatorAccounts)
      await admin.call(
        'DELETE',
        `/operator/${employee.id}/accounts/${account.accountId}`,
        undefined,
        admin.token,
      );
    await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
    await expect(page.getByText('Seu operador ainda não tem uma conta disponível.')).toBeVisible();
    await admin.call(
      'PUT',
      `/operator/${employee.id}`,
      { name: employee.name, userId: null },
      admin.token,
    );
    await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
    await expect(page.getByText('Seu login ainda não está vinculado a um operador.')).toBeVisible();
  } finally {
    await admin.call(
      'PUT',
      `/operator/${employee.id}`,
      { name: employee.name, userId: user.userId },
      admin.token,
    );
    for (const account of accounts.operatorAccounts)
      await admin.call(
        'POST',
        `/operator/${employee.id}/accounts`,
        { accountId: account.accountId },
        admin.token,
      );
  }
});
test('live role reduction removes management destinations before returning to the Member home', async ({
  page,
}) => {
  await login(page, state.users.adminLinked);
  await expect(page.getByRole('heading', { name: 'Fila de trabalho', exact: true })).toBeVisible();
  const admin = await administrator();
  const members = await admin.call<{ branchUsers: { id: string; email: string }[] }>(
    'GET',
    '/branch/users',
    undefined,
    admin.token,
  );
  const member = members.branchUsers.find((item) => item.email === state.users.adminLinked.email)!;
  try {
    await admin.call('PUT', `/branch/users/${member.id}/role`, { role: 'Member' }, admin.token);
    await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
    await expect(page).toHaveURL(/\/turno\/hoje$/);
    await expect(
      page.getByText('Seu perfil nesta filial mudou. Abrimos o início disponível para seu acesso.'),
    ).toBeVisible();
    await expect(
      page
        .getByRole('navigation', { name: 'Navegação principal' })
        .getByRole('link', { name: 'Dashboard', exact: true }),
    ).toHaveCount(0);
  } finally {
    await admin.call('PUT', `/branch/users/${member.id}/role`, { role: 'Admin' }, admin.token);
  }
});

test('real generic password refusal and advisory throttling never disclose account existence', async ({
  page,
}) => {
  await page.goto(`${state.webUrl}/entrar`);
  await page.getByLabel('E-mail', { exact: true }).fill('missing-person-shell@example.invalid');
  for (let attempt = 0; attempt < 6; attempt += 1) {
    await page.getByLabel('Senha', { exact: true }).fill('wrong temporary password');
    const response = page.waitForResponse(
      (response) =>
        response.url().endsWith('/bff/session/login') && response.request().method() === 'POST',
    );
    await page.getByRole('button', { name: 'Entrar', exact: true }).click();
    const refused = await response;
    expect(refused.status()).toBe(attempt < 5 ? 401 : 429);
    const body = (await refused.json()) as { errorMessages: string[] };
    for (const message of body.errorMessages)
      await expect(page.getByText(message, { exact: true })).toBeVisible();
    await expect(page.getByLabel('Senha', { exact: true })).toHaveValue('');
    await expect(page.getByLabel('E-mail', { exact: true })).toHaveValue(
      'missing-person-shell@example.invalid',
    );
    if (attempt === 5) expect(Number(refused.headers()['retry-after'])).toBeGreaterThan(0);
  }
  await expect(page).toHaveURL(/\/entrar$/);
  await expect(page.getByRole('link', { name: 'Esqueci minha senha' })).toBeVisible();
});

test('unknown login outcome stays unconfirmed and never automatically retries password entry', async ({
  page,
}) => {
  await page.goto(`${state.webUrl}/entrar`);
  let attempts = 0;
  await page.route('**/bff/session/login', (route) => {
    attempts += 1;
    return route.fulfill({
      status: 503,
      contentType: 'application/json',
      body: JSON.stringify({ errorMessages: ['Resposta indisponível.'] }),
    });
  });
  await page.getByLabel('E-mail', { exact: true }).fill(state.users.carla.email);
  await page.getByLabel('Senha', { exact: true }).fill(state.users.carla.password);
  await page.getByRole('button', { name: 'Mostrar senha' }).click();
  await expect(page.getByLabel('Senha', { exact: true })).toHaveAttribute('type', 'text');
  await page.getByLabel('Senha', { exact: true }).press('Enter');
  await expect(page.getByText('Resultado não confirmado', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Senha', { exact: true })).toHaveValue('');
  expect(attempts).toBe(1);
});

test('link loss on a shared shift route returns to Gestão without blocking transactions or close flows', async ({
  page,
}) => {
  await login(page, state.users.managerLinked);
  await page.getByRole('button', { name: /Conta de/ }).click();
  await page.getByRole('menuitemradio', { name: 'Meu turno', exact: true }).click();
  await expect(page).toHaveURL(/\/turno\/hoje$/);
  await page
    .getByRole('navigation', { name: 'Navegação principal' })
    .getByRole('link', { name: 'Transações', exact: true })
    .click();
  await expect(page.getByRole('button', { name: /Conta de.*Meu turno/ })).toBeVisible();
  const admin = await administrator();
  const employees = await admin.call<{
    operators: { id: string; name: string; userId: string | null }[];
  }>('GET', '/operator', undefined, admin.token);
  const members = await admin.call<{ branchUsers: { userId: string; email: string }[] }>(
    'GET',
    '/branch/users',
    undefined,
    admin.token,
  );
  const user = members.branchUsers.find((item) => item.email === state.users.managerLinked.email)!;
  const employee = employees.operators.find((item) => item.userId === user.userId)!;
  try {
    await admin.call(
      'PUT',
      `/operator/${employee.id}`,
      { name: employee.name, userId: null },
      admin.token,
    );
    await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
    await expect(page.getByRole('button', { name: /Conta de.*Gestão/ })).toBeVisible();
    await expect(page).toHaveURL(/\/transacoes$/);
    await expect(page.getByRole('heading', { name: 'Transações', exact: true })).toBeVisible();
    await expect(
      page.getByText('Meu turno não está disponível para você nesta filial.'),
    ).toHaveCount(0);
    for (const [route, title] of [
      ['/fechamentos/abrir', 'Abrir dia'],
      ['/fechamentos/12345678-1234-1234-1234-123456789abc', 'Fechar dia'],
      ['/fechamentos/12345678-1234-1234-1234-123456789abc/correcao', 'Corrigir e reenviar'],
    ]) {
      await page.goto(branchUrl(state, state.branches.alfa, route!));
      await expect(page.getByRole('heading', { name: title, exact: true })).toBeVisible();
      await expect(
        page.getByText('Meu turno não está disponível para você nesta filial.'),
      ).toHaveCount(0);
    }
  } finally {
    await admin.call(
      'PUT',
      `/operator/${employee.id}`,
      { name: employee.name, userId: user.userId },
      admin.token,
    );
  }
});

test('an unlinked Manager keeps the setup explanation while a self-context refetch is pending', async ({
  page,
}) => {
  await login(page, state.users.manager);
  let release!: () => void;
  const waiting = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/bff/api/operator/self-context', async (route) => {
    await waiting;
    await route.continue().catch(() => undefined);
  });
  try {
    await page.goto(branchUrl(state, state.branches.alfa, '/turno/hoje'));
    await expect(
      page.getByText('Meu turno não está disponível para você nesta filial.'),
    ).toBeVisible();
    await expect(page.getByText('Esta função estará disponível em uma próxima etapa.')).toHaveCount(
      0,
    );
  } finally {
    release();
  }
});

test('returning to Gestão is independent of a failed self-context read', async ({ page }) => {
  await login(page, state.users.managerLinked);
  await page.getByRole('button', { name: /Conta de/ }).click();
  await page.getByRole('menuitemradio', { name: 'Meu turno', exact: true }).click();
  await expect(page).toHaveURL(/\/turno\/hoje$/);
  await page.route('**/bff/api/operator/self-context', (route) =>
    route.fulfill({
      status: 503,
      contentType: 'application/json',
      body: JSON.stringify({ errorMessages: ['Vínculo indisponível.'] }),
    }),
  );
  await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
  await expect(page.getByText('Vínculo indisponível.', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: /Conta de/ }).click();
  await page.getByRole('menuitemradio', { name: 'Gestão', exact: true }).click();
  await expect(page).toHaveURL(/\/gestao\/fila$/);
  await expect(page.getByRole('heading', { name: 'Fila de trabalho', exact: true })).toBeVisible();
});

test('open drawers pass axe contrast checks in both themes at compact and tablet widths', async ({
  page,
}) => {
  const require = createRequire(import.meta.url);
  const captures = await mkdtemp(path.join(tmpdir(), 'loto-m4-drawer-'));
  await login(page, state.users.ana);
  for (const width of [900, 375])
    for (const theme of ['light', 'dark']) {
      await page.setViewportSize({ width, height: 1000 });
      await page.goto(branchUrl(state, state.branches.alfa, '/gestao/fila'));
      await page.evaluate((preference) => {
        document.cookie = `__Host-loto-theme=${preference};Path=/;Secure;SameSite=Lax`;
      }, theme);
      await page.reload();
      await page.getByRole('button', { name: 'Menu', exact: true }).click();
      const drawer = page.getByRole('dialog', { name: 'Menu de navegação' });
      await expect(drawer).toBeVisible();
      await drawer.evaluate(async (element) => {
        await Promise.all(
          element.getAnimations().map((animation) => animation.finished.catch(() => undefined)),
        );
      });
      await expect(drawer).toHaveCSS('opacity', '1');
      await page.screenshot({
        path: path.join(captures, `drawer-${width}-${theme}.png`),
        fullPage: true,
      });
      await expect(
        drawer.getByRole('button', { name: 'Trocar filial', exact: true }),
      ).toBeVisible();
      await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') });
      const violations = await page.evaluate(async () => {
        const axe = (
          window as unknown as {
            axe: {
              run: (
                context: unknown,
                options: unknown,
              ) => Promise<{
                violations: { id: string; nodes: { target: unknown; failureSummary?: string }[] }[];
              }>;
            };
          }
        ).axe;
        const result = await axe.run(document, {
          runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'] },
        });
        return result.violations.map((item) => ({
          id: item.id,
          targets: item.nodes.map((node) => ({ target: node.target, reason: node.failureSummary })),
        }));
      });
      expect(violations).toEqual([]);
      await page.keyboard.press('Escape');
    }
});

test('entry screens have no sideways page scroll at 375px and doubled text', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  for (const route of ['/entrar', '/filiais']) {
    if (route === '/filiais') await login(page, state.users.ana);
    await page.goto(`${state.webUrl}${route}`);
    await expect(
      page.getByRole('heading', {
        name: route === '/entrar' ? 'Entrar' : 'Escolha a filial',
        exact: true,
      }),
    ).toBeVisible();
    await page.evaluate(() => {
      document.documentElement.style.fontSize = '200%';
    });
    expect(
      await page.evaluate(() => ({
        page: document.documentElement.scrollWidth,
        viewport: innerWidth,
      })),
    ).toEqual({ page: 375, viewport: 375 });
  }
});

test('blueprint navigation order is preserved in management and shift modes', async ({ page }) => {
  await login(page, state.users.managerLinked);
  const nav = page.getByRole('navigation', { name: 'Navegação principal' });
  const adminGroup = nav
    .locator('section')
    .filter({ has: page.getByRole('heading', { name: 'Cadastros', exact: true }) });
  await expect(adminGroup.getByRole('link').first()).toHaveText('Operadores');
  expect((await adminGroup.getByRole('link').allTextContents()).slice(0, 4)).toEqual([
    'Operadores',
    'Contas dos operadores',
    'Contas',
    'Clientes',
  ]);
  await page.getByRole('button', { name: /Conta de/ }).click();
  await page.getByRole('menuitemradio', { name: 'Meu turno', exact: true }).click();
  await expect(page).toHaveURL(/\/turno\/hoje$/);
  await expect(nav.getByRole('link', { name: 'Hoje', exact: true })).toBeVisible();
  expect((await nav.getByRole('link').allTextContents()).slice(0, 4)).toEqual([
    'Hoje',
    'Nova transação',
    'Transações',
    'Clientes',
  ]);
});

test('a non-platform identity can return to branch selection from the platform denial', async ({
  page,
}) => {
  await login(page, state.users.ana);
  await page.goto(`${state.webUrl}/plataforma`);
  await expect(page.getByText('Esta área exige acesso de operador da plataforma.')).toBeVisible();
  await page.getByRole('link', { name: 'Voltar para escolher a filial' }).click();
  await expect(page.getByRole('heading', { name: 'Escolha a filial' })).toBeVisible();
});
