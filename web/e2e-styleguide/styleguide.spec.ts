// Web M3 Phase 7.2: the running gallery in a real browser — both themes and first paint, system
// changes, 375px and zoom, enlarged text, reduced motion, keyboard flows through the overlays and
// the table, nested overlays in the top layer, plus an automated accessibility scan (a supplement
// to keyboard checks, not a substitute for a screen-reader pass).
import { createRequire } from 'node:module';
import { expect, test, type Page } from '@playwright/test';

const require = createRequire(import.meta.url);
const axePath = require.resolve('axe-core/axe.min.js');

const cssVar = (page: Page, name: string) =>
  page.evaluate(
    (variable) => getComputedStyle(document.documentElement).getPropertyValue(variable).trim(),
    name,
  );
const bodyBackground = (page: Page) =>
  page.evaluate(() => getComputedStyle(document.body).backgroundColor);
/** A CSS time in milliseconds (the minifier writes `120ms` as `.12s`). */
const ms = (value: string) =>
  value.endsWith('ms') ? Number(value.slice(0, -2)) : Number(value.slice(0, -1)) * 1000;
const noPageOverflow = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth);

/** The background each pinned scope resolves to, as rendered. */
async function scopeBackgrounds(page: Page) {
  return page.evaluate(() =>
    Object.fromEntries(
      [...document.querySelectorAll<HTMLElement>('[data-theme-scope]')]
        .slice(0, 2)
        .map((element) => [element.dataset.themeScope, getComputedStyle(element).backgroundColor]),
    ),
  );
}

test('renders every token category and all 25 component families of the contract index', async ({
  page,
}) => {
  await page.goto('/styleguide');
  const families = [
    'button',
    'form-field',
    'input',
    'money-input',
    'select',
    'date-picker',
    'choice',
    'badge',
    'card',
    'summary-strip',
    'detail-list',
    'table',
    'filter-bar',
    'pagination',
    'modal-sheet',
    'menu',
    'tabs',
    'disclosure',
    'stepper',
    'notice',
    'toast',
    'save-status',
    'skeleton',
    'empty-state',
    'page-header',
  ];
  expect(families).toHaveLength(25);
  for (const id of [
    ...families,
    'tokens-cores',
    'tokens-dominio',
    'tokens-tipografia',
    'tokens-espacos',
    'tokens-layout',
  ]) {
    await expect(page.locator(`section#${id}`), id).toHaveCount(1);
  }
  // No request leaves the gallery: everything is fixture data.
  const requests: string[] = [];
  page.on('request', (request) => {
    if (!/\/_next\/|\/styleguide|favicon/.test(new URL(request.url()).pathname))
      requests.push(request.url());
  });
  await page.reload();
  await page.waitForLoadState('networkidle');
  expect(requests).toEqual([]);
});

test.describe('themes and first paint', () => {
  test('Sistema follows the device, live, with no reload', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'light' });
    await page.goto('/styleguide');
    const scopes = await scopeBackgrounds(page);
    expect(await page.locator('html').getAttribute('data-theme-preference')).toBe('system');
    expect(await bodyBackground(page)).toBe(scopes.light);
    const navigation = page.evaluate(() => performance.getEntriesByType('navigation').length);
    await page.emulateMedia({ colorScheme: 'dark' });
    await expect.poll(() => bodyBackground(page)).toBe(scopes.dark);
    expect(await navigation).toBe(1);
  });

  test('the server paints the chosen theme in the first HTML — no script needed, no flash', async ({
    browser,
    baseURL,
  }) => {
    const context = await browser.newContext({ javaScriptEnabled: false, colorScheme: 'light' });
    await context.addCookies([
      {
        name: '__Host-loto-theme',
        value: 'dark',
        url: `https://${new URL(baseURL ?? '').host}/`,
        secure: true,
        sameSite: 'Lax',
      },
    ]);
    const page = await context.newPage();
    const response = await page.goto('/styleguide');
    expect(await response?.text()).toContain('data-theme-preference="dark"');
    const scopes = await scopeBackgrounds(page);
    expect(await bodyBackground(page)).toBe(scopes.dark);
    await context.close();
  });

  test('choosing Escuro applies at once, persists on reload and reaches the other tab', async ({
    context,
  }) => {
    const page = await context.newPage();
    const other = await context.newPage();
    await page.emulateMedia({ colorScheme: 'light' });
    await other.emulateMedia({ colorScheme: 'light' });
    await page.goto('/styleguide');
    await other.goto('/styleguide');
    const scopes = await scopeBackgrounds(page);
    await page.getByRole('radio', { name: 'Escuro' }).first().click();
    await expect(page.locator('html')).toHaveAttribute('data-theme-preference', 'dark');
    expect(await bodyBackground(page)).toBe(scopes.dark);
    await expect(other.locator('html')).toHaveAttribute('data-theme-preference', 'dark');
    const cookie = (await context.cookies()).find((item) => item.name === '__Host-loto-theme');
    expect(cookie).toMatchObject({
      value: 'dark',
      secure: true,
      sameSite: 'Lax',
      path: '/',
      httpOnly: false,
    });
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-theme-preference', 'dark');
    await page.getByRole('radio', { name: 'Sistema' }).first().click();
    await expect(page.locator('html')).toHaveAttribute('data-theme-preference', 'system');
  });
});

test.describe('layout, text and motion', () => {
  test('375px: compact arrangement, no horizontal page scroll, filters in a sheet', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/styleguide');
    await expect(page.getByTestId('environment')).toContainText('Arranjo: compacto');
    expect(await noPageOverflow(page)).toBe(true);
    // Dense tables scroll inside their own labelled region instead of the page.
    const region = page.getByRole('region', { name: 'Tabela de transações, role horizontalmente' });
    expect(await region.evaluate((element) => element.scrollWidth > element.clientWidth)).toBe(
      true,
    );
    await page.getByRole('button', { name: /^Filtros \(\d+\)$/ }).click();
    await expect(page.getByRole('dialog', { name: 'Filtros de transações' })).toBeVisible();
  });

  test('200% zoom (half the CSS width) and doubled text keep content reachable without page scroll', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 640, height: 480 });
    await page.goto('/styleguide');
    expect(await noPageOverflow(page)).toBe(true);
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.addStyleTag({ content: 'html { font-size: 200% }' });
    expect(await noPageOverflow(page)).toBe(true);
    // A long option label wraps in its trigger instead of being cut.
    const carried = page.getByRole('combobox', { name: 'Conta (fora das opções)' });
    await expect(carried).toContainText('Terminal 9 (fora das opções atuais)');
    expect(await carried.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(
      true,
    );
  });

  test('reduced motion swaps every transition for an instant one', async ({ page }) => {
    await page.goto('/styleguide');
    expect(ms(await cssVar(page, '--loto-motion-control-duration'))).toBe(120);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await expect.poll(async () => ms(await cssVar(page, '--loto-motion-control-duration'))).toBe(0);
    expect(ms(await cssVar(page, '--loto-motion-skeleton-duration'))).toBe(0);
    await expect(page.getByTestId('environment')).toContainText('movimento: reduzido');
  });
});

test.describe('keyboard flows', () => {
  test('dialog: focus moves in, stays in, Escape closes and returns it; a listbox opens above it', async ({
    page,
  }) => {
    await page.goto('/styleguide#modal-sheet');
    const trigger = page.getByRole('button', { name: 'Novo cliente (diálogo com seletores)' });
    await trigger.focus();
    await page.keyboard.press('Enter');
    const dialog = page.getByRole('dialog', { name: 'Novo cliente' });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByLabel('Nome', { exact: true })).toBeFocused();
    for (let index = 0; index < 12; index += 1) {
      await page.keyboard.press('Tab');
      expect(
        await page.evaluate(
          () =>
            document.activeElement?.closest('dialog') !== null ||
            document.activeElement === document.body,
        ),
      ).toBe(true);
    }
    // Nested overlay: the type listbox opens in the top layer above the modal dialog.
    await page.getByRole('combobox', { name: 'Tipo de transação' }).focus();
    await page.keyboard.press('ArrowDown');
    const listbox = page.getByRole('listbox');
    await expect(listbox).toBeVisible();
    const onTop = await listbox.evaluate((element) => {
      const box = element.getBoundingClientRect();
      return element.contains(document.elementFromPoint(box.left + box.width / 2, box.top + 8));
    });
    expect(onTop).toBe(true);
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');
    await expect(page.getByRole('combobox', { name: 'Tipo de transação' })).toContainText(
      'Recebimento de fiado',
    );
    await expect(dialog).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    await expect(trigger).toBeFocused();
  });

  test('at 375px a picker inside a dialog opens in the popover layer — one modal only', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/styleguide#modal-sheet');
    await page.getByRole('button', { name: 'Novo cliente (diálogo com seletores)' }).click();
    const counts = () =>
      page.evaluate(() => ({
        modals: document.querySelectorAll('dialog:modal').length,
        popovers: document.querySelectorAll(':popover-open').length,
      }));
    await page
      .getByRole('dialog', { name: 'Novo cliente' })
      .getByRole('button', { name: 'Abrir calendário' })
      .click();
    await expect(page.getByRole('grid')).toBeVisible();
    expect(await counts()).toEqual({ modals: 1, popovers: 1 });
    await page.keyboard.press('Escape');
    await expect(page.getByRole('grid')).toBeHidden();
    await expect(page.getByRole('dialog', { name: 'Novo cliente' })).toBeVisible();
    await page.getByRole('combobox', { name: 'Tipo de transação' }).click();
    await expect(page.getByRole('listbox')).toBeVisible();
    expect(await counts()).toEqual({ modals: 1, popovers: 1 });
  });

  test('calendar inside a dialog: keyboard grid, choose a day, Escape returns focus', async ({
    page,
  }) => {
    await page.goto('/styleguide#modal-sheet');
    await page.getByRole('button', { name: 'Novo cliente (diálogo com seletores)' }).click();
    const opener = page
      .getByRole('dialog', { name: 'Novo cliente' })
      .getByRole('button', { name: 'Abrir calendário' });
    await opener.click();
    const grid = page.getByRole('grid');
    await expect(grid).toBeVisible();
    await expect(page.locator(':focus')).toHaveAttribute(
      'aria-label',
      'quinta-feira, 24 de setembro de 2026, hoje',
    );
    await page.keyboard.press('ArrowLeft');
    await expect(page.locator(':focus')).toHaveAttribute('data-date', '2026-09-23');
    await page.keyboard.press('Enter');
    await expect(page.getByLabel('Vencimento')).toHaveValue('23/09/2026');
    await expect(opener).toBeFocused();
  });

  test('session menu: arrows, type-ahead, unavailable item with its reason, Escape back to the trigger', async ({
    page,
  }) => {
    await page.goto('/styleguide#menu');
    const trigger = page.getByRole('button', { name: 'Conta de Ricardo Moura, Gestão' });
    await trigger.focus();
    await page.keyboard.press('ArrowDown');
    await expect(page.getByRole('menuitemradio', { name: /Gestão/ })).toBeFocused();
    await page.keyboard.press('m');
    const unavailable = page.getByRole('menuitem', { name: 'Meu turno' });
    await expect(unavailable).toBeFocused();
    await expect(unavailable).toHaveAttribute('aria-disabled', 'true');
    await expect(unavailable).toHaveAccessibleDescription(
      /Indisponível: seu login não está vinculado/,
    );
    await page.keyboard.press('End');
    await expect(page.getByRole('menuitem', { name: 'Sair' })).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(trigger).toBeFocused();
  });

  test('tabs and segmented control move with the arrows', async ({ page }) => {
    await page.goto('/styleguide#tabs');
    await page.getByRole('tab', { name: 'Dívidas em aberto (12)' }).focus();
    await page.keyboard.press('ArrowRight');
    await expect(page.getByRole('tab', { name: 'Saldo crédito (3)' })).toBeFocused();
    await expect(page.getByRole('tabpanel')).toContainText('Painel de crédito');
    await page.keyboard.press('Tab');
    await expect(page.getByRole('tabpanel')).toBeFocused();
  });

  test('table rows: the row opens its record; nested actions and text selection do not', async ({
    page,
  }) => {
    await page.goto('/styleguide#table');
    const row = page.getByRole('row').filter({ hasText: 'Venda de bolão' });
    await row.getByRole('button', { name: 'Ver parcelas' }).click();
    expect(new URL(page.url()).searchParams.get('transacao')).toBeNull();
    await row.getByRole('checkbox').click({ force: true });
    expect(new URL(page.url()).searchParams.get('transacao')).toBeNull();
    // Drag to select the value text: a selection never navigates.
    const value = row.getByRole('cell', { name: 'R$ 943,10', exact: true });
    const box = await value.boundingBox();
    if (box) {
      await page.mouse.move(box.x + 2, box.y + box.height / 2);
      await page.mouse.down();
      await page.mouse.move(box.x + box.width - 2, box.y + box.height / 2);
      await page.mouse.up();
    }
    expect(new URL(page.url()).searchParams.get('transacao')).toBeNull();
    await page.evaluate(() => window.getSelection()?.removeAllRanges());
    // Keyboard users reach the explicit, named link; a plain row click activates the same link.
    await expect(
      row.getByRole('link', { name: 'Abrir transação 24/09/2026 R$ 943,10 Terminal 2' }),
    ).toBeVisible();
    await row.getByText('Terminal 2', { exact: true }).click();
    await expect.poll(() => new URL(page.url()).searchParams.get('transacao')).toBe('1');
  });
});

test.describe('automated accessibility scan (supplement)', () => {
  for (const theme of ['light', 'dark'] as const) {
    test(`no WCAG A/AA violations axe can detect — ${theme}`, async ({ page }) => {
      await page.emulateMedia({ colorScheme: theme });
      await page.goto('/styleguide');
      await page.addScriptTag({ path: axePath });
      const violations = await page.evaluate(async () => {
        const axe = (
          window as unknown as {
            axe: {
              run: (
                context: unknown,
                options: unknown,
              ) => Promise<{ violations: { id: string; nodes: { target: unknown }[] }[] }>;
            };
          }
        ).axe;
        const result = await axe.run(document, {
          runOnly: {
            type: 'tag',
            values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'],
          },
        });
        return result.violations.map((violation) => ({
          id: violation.id,
          targets: violation.nodes.slice(0, 5).map((node) => node.target),
        }));
      });
      expect(violations).toEqual([]);
    });
  }
});
