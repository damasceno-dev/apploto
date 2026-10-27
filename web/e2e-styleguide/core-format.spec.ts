// Web M3 7.4 in Chrome: the gallery's components and formatting section run on the real
// `@loto/core` adapter. Values deliberately sit outside the old fixture tables. Server render vs
// browser: the same core functions must produce byte-identical text (no hydration mismatch), the
// exact code points, no date shift, and audit instants only in an explicit or the device zone.
import { expect, test, type Page } from '@playwright/test';

const NBSP = ' ';
const MINUS = '−';

/**
 * Console errors and page errors, including React hydration warnings. A failed resource load is
 * recorded by URL instead (the console line carries none), so the gallery's missing favicon is
 * told apart from anything else.
 */
function collectErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('console', (message) => {
    if (message.text().startsWith('Failed to load resource')) return;
    if (message.type() === 'error' || /hydrat/i.test(message.text())) errors.push(message.text());
  });
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('response', (response) => {
    if (response.status() >= 400 && new URL(response.url()).pathname !== '/favicon.ico')
      errors.push(`${response.status()} ${response.url()}`);
  });
  return errors;
}

const text = (page: Page, testId: string) => page.getByTestId(testId).textContent();

/** Opens the gallery and waits for hydration: the device-zone audit time is filled only then. */
async function openHydrated(page: Page, hash: string) {
  await page.goto(`/styleguide${hash}`);
  await expect(page.getByTestId('fmt-instant-device')).not.toBeEmpty();
}

test('the server HTML carries core-formatted text with the contract code points', async ({
  request,
}) => {
  const html = await (await request.get('/styleguide')).text();
  // React escapes nothing here: the characters are in the HTML as code points.
  expect(html).toContain(`R$${NBSP}1.234.567,89`);
  expect(html).toContain(`${MINUS}R$${NBSP}20,00 · falta`);
  expect(html).toContain(`+R$${NBSP}10,00 · crédito do cliente`);
  expect(html).toContain('29/02/2028');
  expect(html).toContain('sexta-feira, 7 de março de 2031');
  expect(html).toContain('7h 20min');
  expect(html).toContain('529.982.247-25');
  expect(html).toContain('um milhão duzentos e trinta e quatro mil');
  // The explicitly zoned audit instant is rendered on the server; the device-zone one is not.
  expect(html).toMatch(/data-time-zone="America\/Sao_Paulo"[^>]*>08\/07\/2026 14:30</);
  expect(html).toMatch(/<time datetime="2026-07-08T17:30:00Z"><\/time>/i);
  expect(html).not.toMatch(/R\$ \d/); // never an ASCII space after R$
});

for (const timezoneId of ['America/Sao_Paulo', 'Asia/Kolkata', 'America/New_York']) {
  test(`hydrates without mismatch; the device-zone instant follows the browser (${timezoneId})`, async ({
    browser,
  }) => {
    const context = await browser.newContext({ timezoneId });
    const page = await context.newPage();
    const errors = collectErrors(page);
    await openHydrated(page, '#formatacao');
    const expected = {
      'America/Sao_Paulo': '08/07/2026 14:30',
      'Asia/Kolkata': '08/07/2026 23:00',
      'America/New_York': '08/07/2026 13:30',
    }[timezoneId];
    await expect(page.getByTestId('fmt-instant-device')).toHaveText(expected ?? '');
    // The explicit zone never depends on the device; business dates never shift.
    await expect(page.getByTestId('fmt-instant-explicit')).toHaveText('08/07/2026 14:30');
    await expect(page.getByTestId('fmt-date')).toHaveText('29/02/2028');
    expect(await text(page, 'fmt-money')).toBe(`R$${NBSP}1.234.567,89`);
    expect(await text(page, 'fmt-signed')).toBe(`${MINUS}R$${NBSP}20,00 · falta`);
    expect(errors).toEqual([]);
    await context.close();
  });
}

test.describe('MoneyInput with real keystrokes', () => {
  test('arbitrary amounts right to left, the spoken value, the 14-digit cap and deletion to blank', async ({
    page,
  }) => {
    await openHydrated(page, '#money-input');
    const field = page.getByLabel('Contagem de Raspadinha');
    const spoken = async () =>
      field.evaluate((input) => {
        const id = input.getAttribute('aria-describedby')?.split(' ').at(-1) ?? '';
        return document.getElementById(id)?.textContent;
      });
    await field.click();
    expect(await spoken()).toBe('não informado');
    await page.keyboard.type('0');
    await expect(field).toHaveValue('0,00');
    expect(await spoken()).toBe('zero reais');
    await page.keyboard.type('4815162342');
    await expect(field).toHaveValue('48.151.623,42');
    expect(await spoken()).toBe(
      'quarenta e oito milhões cento e cinquenta e um mil seiscentos e vinte e três reais e quarenta e dois centavos',
    );
    await page.keyboard.type('9999');
    await expect(field).toHaveValue('481.516.234.299,99');
    await page.keyboard.type('9'); // the 15th digit is refused
    await expect(field).toHaveValue('481.516.234.299,99');
    await expect(page.getByTestId('money-state')).toHaveText(
      'Estado relatado: amount:48151623429999',
    );
    for (let index = 0; index < 14; index += 1) await page.keyboard.press('Backspace');
    await expect(field).toHaveValue('');
    await expect(page.getByTestId('money-state')).toHaveText('Estado relatado: blank');
  });

  test('paste goes through the core parser; signed or malformed text changes nothing', async ({
    page,
  }) => {
    await openHydrated(page, '#money-input');
    const field = page.getByLabel('Contagem de Raspadinha');
    const paste = (value: string) =>
      field.evaluate((input, pasted) => {
        const data = new DataTransfer();
        data.setData('text/plain', pasted);
        input.dispatchEvent(
          new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }),
        );
      }, value);
    await paste(`R$${NBSP}31.415,92`);
    await expect(field).toHaveValue('31.415,92');
    await paste('-10,00');
    await paste('1.5');
    await paste('1.000.000.000.000,00');
    await expect(field).toHaveValue('31.415,92');
    await expect(page.getByTestId('money-state')).toHaveText('Estado relatado: amount:3141592');
  });
});

test.describe('restricted arithmetic input (M9.6 Phase 8 demonstration)', () => {
  test('shows the evaluated amount before acceptance and only then fills the field', async ({
    page,
  }) => {
    await openHydrated(page, '#formatacao');
    const input = page.getByTestId('expression-input');
    const state = page.getByTestId('expression-state');
    const accepted = page.getByLabel('Valor aceito');
    await input.fill('100+50+20');
    await expect(state).toHaveText('ok:17000');
    await expect(page.getByText('= R$ 170,00')).toBeVisible();
    await expect(accepted).toHaveValue('');
    await page.getByRole('button', { name: `Usar R$${NBSP}170,00` }).click();
    await expect(accepted).toHaveValue('170,00');
    await input.fill('(10,50 × 3) ÷ 2');
    await expect(state).toHaveText('ok:1575');
    await input.fill('10/3');
    await expect(state).toHaveText('ok:333');
    await expect(page.getByText('(arredondado para centavos)')).toBeVisible();
    await input.fill('100 +');
    await expect(state).toHaveText('incomplete');
    await expect(page.getByRole('button', { name: 'Usar resultado' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    await input.fill('10 ÷ 0');
    await expect(state).toHaveText('invalid:division-by-zero');
    await expect(page.getByText('Não é possível dividir por zero.')).toBeVisible();
    await input.fill('alert(1)');
    await expect(state).toHaveText('invalid:unsupported-character');
    await input.fill('1.5 + 2');
    await expect(state).toHaveText('invalid:malformed-number');
    await input.fill('');
    await expect(state).toHaveText('empty');
    // Nothing was accepted after the first explicit choice.
    await expect(accepted).toHaveValue('170,00');
  });

  test('keyboard only: type, Tab to the action, Enter accepts', async ({ page }) => {
    await openHydrated(page, '#formatacao');
    await page.getByTestId('expression-input').focus();
    await page.keyboard.type('2 * 3,50');
    await page.keyboard.press('Tab'); // the accepted-value field
    await page.keyboard.press('Tab'); // the action
    await expect(page.getByRole('button', { name: `Usar R$${NBSP}7,00` })).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page.getByLabel('Valor aceito')).toHaveValue('7,00');
  });
});

test.describe('DatePicker and identifiers with core', () => {
  test('typed dates are validated by the real calendar; names come from core', async ({ page }) => {
    await openHydrated(page, '#date-picker');
    const field = page.getByLabel('Data da transação');
    await field.fill('29/02/2024');
    await field.blur();
    await expect(field).toHaveValue('29/02/2024');
    await expect(page.getByText('Data inválida. Use dd/mm/aaaa.')).toHaveCount(0);
    await field.fill('29/02/2026');
    await field.blur();
    await expect(field).toHaveValue('29/02/2026');
    await expect(page.getByText('Data inválida. Use dd/mm/aaaa.')).toBeVisible();
    await field.fill('31/08/2026');
    await field.blur();
    await page.getByRole('button', { name: 'Abrir calendário' }).first().click();
    await expect(page.getByText('agosto de 2026', { exact: true })).toBeVisible();
    await expect(page.locator(':focus')).toHaveAttribute(
      'aria-label',
      'segunda-feira, 31 de agosto de 2026',
    );
    await page.keyboard.press('ArrowRight');
    await expect(page.getByText('setembro de 2026', { exact: true })).toBeVisible();
    await expect(page.locator(':focus')).toHaveAttribute(
      'aria-label',
      'terça-feira, 1 de setembro de 2026',
    );
    await page.keyboard.press('Escape');
  });

  test('CPF is punctuated while typed and stays verbatim when overlong; badges use copy §5', async ({
    page,
  }) => {
    await openHydrated(page, '#input');
    const cpf = page.getByLabel('CPF');
    await cpf.fill('5299822472');
    await expect(cpf).toHaveValue('529.982.247-2');
    await cpf.fill('52998224725');
    await expect(cpf).toHaveValue('529.982.247-25');
    await cpf.fill('529982247251');
    await expect(cpf).toHaveValue('529982247251');
    await expect(page.locator('#tokens-dominio [data-value="Submitted"]').first()).toHaveText(
      'Enviado',
    );
    await expect(page.locator('#tokens-dominio [data-value="Days0To30"]').first()).toHaveText(
      '0–30 dias',
    );
    await expect(page.getByTestId('enum-labels').locator('dt')).toHaveCount(25);
  });
});

test('375px in both themes: the formatting section and the demo fit without page scroll', async ({
  page,
}) => {
  await page.setViewportSize({ width: 375, height: 800 });
  for (const scheme of ['light', 'dark'] as const) {
    await page.emulateMedia({ colorScheme: scheme, reducedMotion: 'reduce' });
    await openHydrated(page, '#formatacao');
    await expect(page.locator('section#formatacao')).toBeVisible();
    await page.getByTestId('expression-input').fill('999.999.999.999,99');
    await expect(page.getByTestId('expression-state')).toHaveText('ok:99999999999999');
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
      ),
    ).toBe(true);
  }
});

test.describe('review round 2 (2026-10-27) in Chrome', () => {
  test('read-only MoneyInput and DateField: a real paste and the calendar change nothing', async ({
    page,
  }) => {
    await openHydrated(page, '#money-input');
    const amount = page.getByLabel('Valor conferido');
    await amount.focus();
    await page.evaluate(() => navigator.clipboard?.writeText?.('999,99').catch(() => undefined));
    await amount.evaluate((input) => {
      const data = new DataTransfer();
      data.setData('text/plain', '999,99');
      input.dispatchEvent(
        new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }),
      );
    });
    await page.keyboard.type('5');
    await page.keyboard.press('Backspace');
    await expect(amount).toHaveValue('12,34');
    const date = page.getByLabel('Data registrada');
    await expect(date).toHaveValue('24/09/2026');
    const opener = date.locator('xpath=..').getByRole('button', { name: 'Abrir calendário' });
    await expect(opener).toBeDisabled();
    await expect(page.getByTestId('readonly-changes')).toHaveText(
      'Alterações no valor somente leitura: 0',
    );
  });

  test('choosing the current date again replaces invalid typed text and its error', async ({
    page,
  }) => {
    await openHydrated(page, '#date-picker');
    const field = page.getByLabel('Data da transação');
    const opener = field.locator('xpath=..').getByRole('button', { name: 'Abrir calendário' });
    const chooseToday = async () => {
      await opener.click();
      await page
        .getByRole('button', { name: 'quinta-feira, 24 de setembro de 2026, hoje' })
        .click();
    };
    await chooseToday();
    await expect(field).toHaveValue('24/09/2026');
    await field.fill('99/99/9999');
    await field.blur();
    await expect(page.getByText('Data inválida. Use dd/mm/aaaa.')).toBeVisible();
    await chooseToday();
    await expect(field).toHaveValue('24/09/2026');
    await expect(page.getByText('Data inválida. Use dd/mm/aaaa.')).toHaveCount(0);
  });

  test('the calendar at 01/01/0001 does not navigate past the supported range', async ({
    page,
  }) => {
    const errors = collectErrors(page);
    await openHydrated(page, '#date-picker');
    const field = page.getByLabel('Data da transação');
    await field.fill('01/01/0001');
    await field.blur();
    await field.locator('xpath=..').getByRole('button', { name: 'Abrir calendário' }).click();
    await expect(page.locator(':focus')).toHaveAttribute(
      'aria-label',
      'segunda-feira, 1 de janeiro de 0001',
    );
    for (const key of ['ArrowLeft', 'ArrowUp', 'PageUp', 'Home']) await page.keyboard.press(key);
    await expect(page.locator(':focus')).toHaveAttribute('data-date', '0001-01-01');
    await page.keyboard.press('ArrowRight');
    await expect(page.locator(':focus')).toHaveAttribute('data-date', '0001-01-02');
    await page.keyboard.press('Escape');
    expect(errors).toEqual([]);
  });

  test('CPF punctuates while typed; Backspace over a separator removes the digit before it', async ({
    page,
  }) => {
    await openHydrated(page, '#input');
    const cpf = page.getByLabel('CPF');
    await cpf.click();
    await page.keyboard.type('5299');
    await expect(cpf).toHaveValue('529.9');
    await page.keyboard.type('822');
    await expect(cpf).toHaveValue('529.982.2');
    // Caret right after the second dot, then Backspace.
    await cpf.evaluate((input: HTMLInputElement) => input.setSelectionRange(8, 8));
    await page.keyboard.press('Backspace');
    await expect(cpf).toHaveValue('529.982');
    expect(await cpf.evaluate((input: HTMLInputElement) => input.selectionStart)).toBe(6);
    await cpf.evaluate((input: HTMLInputElement) => input.setSelectionRange(7, 7));
    await page.keyboard.type('24725');
    await expect(cpf).toHaveValue('529.982.247-25');
  });

  test('Backspace on an invalid character keeps the digits (529.98a → 529.98)', async ({
    page,
  }) => {
    await openHydrated(page, '#input');
    const cpf = page.getByLabel('CPF');
    await cpf.click();
    await page.keyboard.type('52998a');
    await expect(cpf).toHaveValue('529.98a');
    await page.keyboard.press('Backspace');
    await expect(cpf).toHaveValue('529.98');
    // Overlong passthrough: removing a dot removes only the dot.
    await cpf.fill('529.982.247-251');
    await cpf.evaluate((input: HTMLInputElement) => input.setSelectionRange(4, 4));
    await page.keyboard.press('Backspace');
    await expect(cpf).toHaveValue('529982.247-251');
  });
});
