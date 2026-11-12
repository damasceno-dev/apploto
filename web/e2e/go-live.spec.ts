import { createRequire } from 'node:module';
import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { expect, test } from '@playwright/test';
import { apiCall, readState } from './stack';
const require = createRequire(import.meta.url);

import { branchSlot, branchUrl, sessionCookies, signIn } from './helpers';

test('manager reviews a real CSV import, activates once and resumes at compact width', async ({
  page,
  context,
}) => {
  const state = await readState();
  await signIn(page, state, state.users.ana);
  await page.goto(branchUrl(state, state.branches.alfa, '/gestao/implantacao'));
  await expect(page.getByRole('heading', { name: 'Implantação da filial' })).toBeVisible();
  const token = branchSlot(await sessionCookies(context, state), state.branches.alfa)!.token;
  const call = apiCall(state.apiUrl);
  const created = await call<{ id: string }>(
    'POST',
    '/client',
    { name: 'Cliente implantação E2E', phone: '11999999999' },
    token,
  );
  const accounts = await call<{ accounts: { id: string; name: string; type: string }[] }>(
    'GET',
    '/account',
    undefined,
    token,
  );
  const operators = await call<{ operators: { id: string; name: string }[] }>(
    'GET',
    '/operator',
    undefined,
    token,
  );
  const types = await call<{
    items: {
      id: string;
      name: string;
      requiresTabAccountAndClient: boolean;
      fiadoPaymentKind?: string;
      systemRole?: string;
      categoryName: string;
    }[];
  }>('GET', '/transaction-type', undefined, token);
  const tab = accounts.accounts.find((x) => x.type === 'Tab')!;
  const type = types.items.find(
    (x) =>
      x.requiresTabAccountAndClient &&
      !x.fiadoPaymentKind &&
      !x.systemRole &&
      x.categoryName === 'Saídas',
  )!;
  await page.reload();
  await page.setViewportSize({ width: 375, height: 850 });
  await page.getByLabel('Versão da exportação').selectOption('windows');
  await page.getByLabel('Codificação', { exact: true }).selectOption('utf-8-sig');
  const directory = path.resolve('../server/tests/CommonTestUtilities/Fixtures/Access/windows');
  const sourceFiles = await Promise.all(
    (await readdir(directory))
      .filter((name) => name.endsWith('.csv'))
      .map(async (name) => {
        let buffer = await readFile(path.join(directory, name));
        if (name === 'TblLancamentos.csv')
          buffer = Buffer.from(buffer.toString('utf8').replace('125.50', '125.500000001'), 'utf8');
        return { name, mimeType: 'text/csv', buffer };
      }),
  );
  await page.getByLabel('Tabelas CSV').setInputFiles(sourceFiles);
  await page.getByRole('button', { name: 'Enviar para revisão' }).click();
  await expect(page.getByRole('heading', { name: 'Revisar mapeamento e resolução' })).toBeVisible();
  await page.getByLabel('Filtrar tabela ou pendência').fill('TblLancamentos');
  await page.getByText('TblLancamentos · registro 1 · Pendente', { exact: true }).click();
  await page.getByLabel('Destino do registro').selectOption('OpeningFiado');
  await page.getByLabel('Motivo e evidência').fill('Evidência sintética explícita de implantação');
  await page.getByLabel('Conta de destino').selectOption(tab.id);
  await page.getByLabel('Cliente identificado').selectOption(created.id);
  await page.getByLabel('Operador identificado').selectOption(operators.operators[0]!.id);
  await page.getByLabel('Tipo de destino').selectOption(type.id);
  await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') });
  const violations = await page.evaluate(async () => {
    const axe = (
      window as unknown as {
        axe: {
          run: (context: unknown, options: unknown) => Promise<{ violations: { id: string }[] }>;
        };
      }
    ).axe;
    return (
      await axe.run(document, {
        runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'] },
      })
    ).violations.map((x) => x.id);
  });
  expect(violations).toEqual([]);
  await page.getByRole('button', { name: 'Salvar mapeamento' }).click();
  await expect(page.getByText(/Revisão 1 · Mapeamento salvo/)).toBeVisible();
  await page.getByRole('button', { name: 'Gerar prévia' }).click();
  await expect(page.getByText('Valor original: 125.500000001', { exact: true })).toBeVisible();
  await expect(
    page.getByRole('checkbox', {
      name: 'Revisei esta versão, os destinos, as exclusões e as evidências.',
    }),
  ).toBeDisabled();
  await page.getByLabel('Valor validado de destino').fill('125,50');
  await page
    .getByLabel('Motivo e evidência')
    .fill('Valor original preservado; normalização explícita para 125,50 com evidência sintética.');
  await page.getByRole('button', { name: 'Salvar mapeamento' }).click();
  await expect(page.getByText(/Revisão 2 · Mapeamento salvo/)).toBeVisible();
  await page.getByRole('button', { name: 'Gerar prévia' }).click();
  await expect(
    page.getByText('1 registros suportados · 0 excluídos · 0 pendências.'),
  ).toBeVisible();
  await page
    .getByRole('checkbox', {
      name: 'Revisei esta versão, os destinos, as exclusões e as evidências.',
    })
    .check();
  await call(
    'PUT',
    `/client/${created.id}`,
    { name: 'Cliente alterado após prévia E2E', phone: '11999999999' },
    token,
  );
  await page.getByRole('button', { name: 'Confirmar ativação desta versão' }).click();
  await expect(
    page.getByText('A origem, o mapeamento ou os dados da filial mudaram. Gere uma nova prévia.', {
      exact: true,
    }),
  ).toBeVisible();
  const reviewing = await page.getByLabel('Retomar revisão').inputValue();
  expect(
    (await call<{ targetIds: string[] }>('GET', `/go-live/imports/${reviewing}`, undefined, token))
      .targetIds,
  ).toEqual([]);
  await page
    .getByRole('checkbox', {
      name: 'Consultei o resultado no servidor e posso descartar esta tentativa neste dispositivo.',
    })
    .check();
  await page.getByRole('button', { name: 'Descartar registro desta tentativa' }).click();
  await Promise.all([
    page.waitForResponse(
      (response) => response.url().endsWith('/preview') && response.request().method() === 'POST',
    ),
    page.getByRole('button', { name: 'Gerar prévia' }).click(),
  ]);
  await expect(page.getByRole('button', { name: 'Gerar prévia' })).toBeEnabled();
  await page
    .getByRole('checkbox', {
      name: 'Revisei esta versão, os destinos, as exclusões e as evidências.',
    })
    .check();
  let interrupted = false;
  await page.route('**/bff/api/go-live/imports/*/activate', async (route) => {
    if (interrupted) {
      await route.continue();
      return;
    }
    interrupted = true;
    await route.fetch(); // Real atomic backend commit; lose only its response.
    await route.abort('connectionfailed');
  });
  await page.getByRole('button', { name: 'Confirmar ativação desta versão' }).click();
  await expect(page.getByRole('button', { name: 'Verificar e tentar novamente' })).toBeVisible();
  await page.getByRole('button', { name: 'Verificar e tentar novamente' }).click();
  await expect(page.getByText('Ativação confirmada', { exact: true })).toBeVisible();
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.evaluate(() => {
    document.documentElement.style.fontSize = '200%';
  });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.getByLabel('Retomar revisão').focus();
  await page.keyboard.press('Tab');
  const selected = await page.getByLabel('Retomar revisão').inputValue();
  await page.reload();
  await page.getByLabel('Retomar revisão').selectOption(selected);
  await expect(page.getByText('Ativação confirmada', { exact: true })).toBeVisible();
  const persisted = await call<{ targetIds: string[]; status: string }>(
    'GET',
    `/go-live/imports/${selected}`,
    undefined,
    token,
  );
  expect(persisted.status).toBe('Activated');
  expect(persisted.targetIds).toHaveLength(1);
  await page.getByText('Consultar obrigações registradas', { exact: true }).click();
  await page.getByRole('button', { name: 'Atualizar obrigações', exact: true }).click();
  await expect(
    page.getByText(
      'Obrigação importada: revisão manual indisponível. Consulte a importação de origem.',
    ),
  ).toBeVisible();
  await expect(page.getByRole('button', { name: /^Revisar obrigação / })).toBeDisabled();
  const pix = types.items.find((x) => x.fiadoPaymentKind === 'NonCash' && x.name === 'PIX')!;
  // A subsequent real noncash payment consumes the imported opening without a cashbox leg.
  const payment = await call<{ generatedTransactionId: string | null; sessionId: string | null }>(
    'POST',
    '/transaction/fiado-payment',
    {
      accountId: tab.id,
      clientId: created.id,
      date: (await call<{ branchLocalDate: string }>('GET', '/branch/current', undefined, token))
        .branchLocalDate,
      value: 25.5,
      transactionTypeId: pix.id,
      recordedByOperatorId: operators.operators[0]!.id,
    },
    token,
    { 'Idempotency-Key': crypto.randomUUID() },
  );
  expect(payment.generatedTransactionId).toBeNull();
  expect(payment.sessionId).toBeNull();
});

test('member deep link refuses access before loading private import sources', async ({ page }) => {
  const state = await readState();
  await signIn(page, state, state.users.carla);
  const reads: string[] = [];
  page.on('request', (request) => {
    if (request.url().includes('/bff/api/go-live')) reads.push(request.url());
  });
  await page.goto(branchUrl(state, state.branches.alfa, '/gestao/implantacao'));
  await expect(
    page.getByText('Seu perfil não permite acessar esta página.', { exact: true }),
  ).toBeVisible();
  expect(reads).toHaveLength(0);
});

test('fresh manual setup records explicit zero and debt, then starts without a cashbox claim', async ({
  page,
  context,
}) => {
  const state = await readState();
  await signIn(page, state, state.users.ana);
  await page.goto(branchUrl(state, state.branches.beta, '/gestao/implantacao'));
  const token = branchSlot(await sessionCookies(context, state), state.branches.beta)!.token;
  const call = apiCall(state.apiUrl);
  await page.getByText('Adicionar um destino que ainda não existe', { exact: true }).click();
  const create = async (kind: string, name: string, route: string) => {
    await page.getByLabel('Cadastro necessário').selectOption(kind);
    await page.getByLabel('Nome do novo cadastro').fill(name);
    if (kind === 'client') await page.getByLabel('Telefone comprovado').fill('11988887777');
    const response = page.waitForResponse(
      (r) => r.url().endsWith(route) && r.request().method() === 'POST',
    );
    await page.getByRole('button', { name: 'Criar destino para implantação' }).click();
    const result = await response;
    expect(result.ok()).toBe(true);
    return (await result.json()) as { id: string; tabAccountId?: string };
  };
  const box = await create('cashbox', 'Caixa implantação manual E2E', '/account/terminal');
  const operator = await create('operator', 'Operador implantação manual E2E', '/operator');
  const customer = await create('client', 'Cliente implantação manual E2E', '/client');
  await page.getByText('Conferir e liberar contas para um operador', { exact: true }).click();
  await page
    .getByLabel('Operador da liberação')
    .selectOption({ label: 'Operador implantação manual E2E' });
  await page
    .getByLabel('Conta a liberar')
    .selectOption({ label: 'Terminal · Caixa implantação manual E2E' });
  await page
    .getByRole('checkbox', { name: 'Conferi o operador, a conta e a liberação de acesso.' })
    .check();
  await page.getByRole('button', { name: 'Confirmar liberação desta conta' }).click();
  await expect(page.getByText('Liberação de conta confirmada.', { exact: true })).toBeVisible();
  await page
    .getByLabel('Caixa da contagem inicial')
    .selectOption({ label: 'Caixa implantação manual E2E' });
  await page
    .getByRole('checkbox', { name: 'Confirmo expressamente que a contagem física inicial é zero.' })
    .check();
  const countResponse = page.waitForResponse(
    (r) => r.url().endsWith('/session/opening-count') && r.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Registrar contagem inicial' }).click();
  const count = (await (await countResponse).json()) as { id: string; revision: number };
  await page.getByLabel('Conta', { exact: true }).selectOption(box.tabAccountId!);
  await page.getByLabel('Cliente', { exact: true }).selectOption(customer.id);
  await page.getByLabel('Operador do registro').selectOption(operator.id);
  await page.getByLabel('Tipo da obrigação').selectOption({ label: 'Saídas · Cliente' });
  await page.getByLabel('Valor', { exact: true }).fill('100,00');
  await page.getByLabel('Referência da origem').fill('D5 manual E2E');
  await page
    .getByLabel('Evidência da obrigação')
    .fill('Saldo anterior sintético, sem caixa ou reconhecimento de receita.');
  await page
    .getByRole('checkbox', {
      name: 'Conferi os valores, vencimentos, a origem e a evidência desta obrigação.',
    })
    .check();
  const openingResponse = page.waitForResponse(
    (r) => r.url().endsWith('/go-live/openings') && r.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Registrar obrigação', exact: true }).click();
  const openingResult = await openingResponse;
  expect(openingResult.status()).toBe(201);
  const opening = (await openingResult.json()) as { id: string; transactionIds: string[] };
  const row = await call<{
    sessionId: string | null;
    generatedTransactionId: string | null;
    goLiveOpeningId: string;
    value: number;
  }>('GET', `/transaction/${opening.transactionIds[0]}`, undefined, token);
  expect(row.sessionId).toBeNull();
  expect(row.goLiveOpeningId).toBe(opening.id);
  expect(row.value).toBe(100);
  const started = await call<{ session: { id: string; custody: { sequence: number } } }>(
    'POST',
    '/session/start',
    {
      accountId: box.id,
      opening: {
        custodianOperatorId: operator.id,
        sourceKind: 'OpeningCount',
        sourceId: count.id,
        sourceRevision: count.revision,
        explicitZero: true,
        items: [],
        reason:
          'Exceção explícita do gestor no cenário local, sem reconhecimento atribuído ao operador ausente.',
      },
    },
    token,
    { 'Idempotency-Key': crypto.randomUUID() },
  );
  expect(started.session.custody.sequence).toBe(1);
});
