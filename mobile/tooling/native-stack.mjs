// Mobile-owned disposable API fixtures, adapted from the supported-flow fixture at dfd4474.
// This is an explicit app-owned snapshot: no Web source is imported or rewritten at runtime.
// PostgreSQL/Mailpit/.NET resources belong only to this run. Fake identities use supported
// first-operator, password setup, onboarding and invitation flows; no financial UI is exercised.
import { execFile, spawn } from 'node:child_process';
import { randomBytes, randomUUID } from 'node:crypto';
import { openSync } from 'node:fs';
import { mkdtemp, readFile, writeFile } from 'node:fs/promises';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
const run = promisify(execFile);
export const repoRoot = fileURLToPath(new URL('../..', import.meta.url));
export const stateEnv = 'LOTO_MOBILE_SHELL_STATE';
export async function readState() {
  const file = process.env.LOTO_MOBILE_SHELL_STATE;
  if (!file)
    throw new Error('The native fixture state is missing; use native-shell-stack.mjs start.');
  return JSON.parse(await readFile(file, 'utf8'));
}
async function freePort() {
  return new Promise((resolve, reject) => {
    const server = createServer();
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      server.close(() =>
        typeof address === 'object' && address
          ? resolve(address.port)
          : reject(new Error('no port')),
      );
    });
  });
}
async function waitFor(what, probe, timeoutMs = 120_000) {
  const deadline = Date.now() + timeoutMs;
  let last = null;
  while (Date.now() < deadline) {
    try {
      const value = await probe();
      if (value !== null) return value;
    } catch (error) {
      last = error;
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`Timed out waiting for ${what}${last ? `: ${String(last)}` : ''}`);
}
function start(command, args, env, cwd, log) {
  // Logs go straight to files: the children outlive the process that started them.
  const output = openSync(log, 'a');
  const child = spawn(command, args, {
    cwd,
    env,
    detached: true,
    stdio: ['ignore', output, output],
  });
  child.unref();
  return child;
}
async function json(response) {
  const text = await response.text();
  if (!response.ok) throw new Error(`${response.url} → ${response.status} ${text}`);
  return text ? JSON.parse(text) : undefined;
}
export function apiCall(apiUrl) {
  return async (method, route, body, token, headers = {}) =>
    json(
      await fetch(`${apiUrl}${route}`, {
        method,
        headers: {
          ...(body === undefined ? {} : { 'content-type': 'application/json' }),
          ...(token ? { authorization: `Bearer ${token}` } : {}),
          ...headers,
        },
        body: body === undefined ? undefined : JSON.stringify(body),
      }),
    );
}
/** The capability token from the newest captured message to `email` (local Mailpit only). */
async function capability(mailUrl, email, seen) {
  return waitFor(`mail to ${email}`, async () => {
    const search = await json(
      await fetch(`${mailUrl}/api/v1/search?query=${encodeURIComponent(`to:"${email}"`)}`),
    );
    for (const message of search.messages) {
      if (seen.has(message.ID)) continue;
      const full = await json(await fetch(`${mailUrl}/api/v1/message/${message.ID}`));
      const match = /#token=([A-Za-z0-9%._~-]+)/.exec(`${full.Text}\n${full.HTML}`);
      if (match?.[1]) {
        seen.add(message.ID);
        return decodeURIComponent(match[1]);
      }
    }
    return null;
  });
}
export async function startStack() {
  const directory = await mkdtemp(path.join(tmpdir(), 'loto-mobile-shell-'));
  const id = randomBytes(4).toString('hex');
  const containers = [];
  const pids = [];
  const stateFile = path.join(directory, 'state.json');
  const persist = async (state) => writeFile(stateFile, JSON.stringify(state));
  const owned = () => ({ containers, pids, directory });
  await persist({ owned: owned() });
  process.env.LOTO_MOBILE_SHELL_STATE = stateFile;
  const [dbPort, smtpPort, mailPort, apiPort, accountPort] = await Promise.all([
    freePort(),
    freePort(),
    freePort(),
    freePort(),
    freePort(),
  ]);
  const dbPassword = randomBytes(16).toString('hex');
  const db = `loto-mobile-shell-db-${id}`;
  const mail = `loto-mobile-shell-mail-${id}`;
  await run('docker', [
    'run',
    '-d',
    '--rm',
    '--name',
    db,
    '-p',
    `127.0.0.1:${dbPort}:5432`,
    '-e',
    'POSTGRES_DB=loto_dev_mobile_shell',
    '-e',
    `POSTGRES_PASSWORD=${dbPassword}`,
    'postgres:16-alpine',
  ]);
  containers.push(db);
  await persist({ owned: owned() });
  await run('docker', [
    'run',
    '-d',
    '--rm',
    '--name',
    mail,
    '-p',
    `127.0.0.1:${smtpPort}:1025`,
    '-p',
    `127.0.0.1:${mailPort}:8025`,
    '-e',
    'MP_MAX_MESSAGES=200',
    '-e',
    'MP_DATABASE=/dev/shm/mailpit.db',
    '--tmpfs',
    '/dev/shm',
    'axllent/mailpit:v1.31.5',
  ]);
  containers.push(mail);
  await persist({ owned: owned() });
  await waitFor('PostgreSQL', async () => {
    await run('docker', [
      'exec',
      db,
      'pg_isready',
      '-U',
      'postgres',
      '-d',
      'loto_dev_mobile_shell',
    ]);
    return true;
  });
  const connection = `Host=127.0.0.1;Port=${dbPort};Database=loto_dev_mobile_shell;Username=postgres;Password=${dbPassword}`;
  const accountUrl = `http://localhost:${accountPort}`;
  const apiUrl = `http://127.0.0.1:${apiPort}`;
  const mailUrl = `http://127.0.0.1:${mailPort}`;
  const tokenMinutes = 1;
  const baseEnv = { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1' };
  // Build once from this checkout (restore included), then migrate the owned database. The
  // connection travels in the environment, never on a command line.
  for (const project of [
    'server/server.API/server.API.csproj',
    'server/server.Cli/server.Cli.csproj',
  ]) {
    await run('dotnet', ['build', project, '-v', 'quiet', '-nologo'], {
      cwd: repoRoot,
      env: baseEnv,
      maxBuffer: 64 * 1024 * 1024,
    });
  }
  await run(
    'dotnet',
    [
      'ef',
      'database',
      'update',
      '--no-build',
      '--project',
      'server/server.Infrastructure',
      '--startup-project',
      'server/server.API',
    ],
    {
      cwd: repoRoot,
      env: {
        ...baseEnv,
        ASPNETCORE_ENVIRONMENT: 'Development',
        ConnectionStrings__DefaultConnection: connection,
      },
      maxBuffer: 64 * 1024 * 1024,
    },
  );
  const mailEnv = {
    Enabled: 'true',
    Host: '127.0.0.1',
    Port: String(smtpPort),
    LocalCapture: 'true',
  };
  const api = start(
    'dotnet',
    ['run', '--project', 'server/server.API', '--no-launch-profile', '--no-build'],
    {
      ...baseEnv,
      ASPNETCORE_ENVIRONMENT: 'Development',
      ASPNETCORE_URLS: apiUrl,
      ConnectionStrings__DefaultConnection: connection,
      Token__ExpirationTimeInMinutes: String(tokenMinutes),
      AccountSecurity__ApplicationUrl: `${accountUrl}/`,
      AccountSecurity__LoginRequestsPerMinute: '1000',
      ...Object.fromEntries(
        Object.entries(mailEnv).map(([key, value]) => [`SecurityMail__${key}`, value]),
      ),
    },
    repoRoot,
    path.join(directory, 'api.log'),
  );
  if (api.pid) pids.push(api.pid);
  await persist({ owned: owned() });
  await waitFor('API', async () =>
    (await fetch(`${apiUrl}/branch/current`)).status === 401 ? true : null,
  );
  // First platform operator through the supported CLI, activated through the ordinary reset flow.
  const seen = new Set();
  const users = {
    operator: {
      email: 'operador-e2e@example.invalid',
      password: randomBytes(12).toString('base64url'),
    },
    ana: { email: 'ana-e2e@example.invalid', password: randomBytes(12).toString('base64url') },
    bia: { email: 'bia-e2e@example.invalid', password: randomBytes(12).toString('base64url') },
    carla: { email: 'carla-e2e@example.invalid', password: randomBytes(12).toString('base64url') },
    memberLinked: {
      email: 'member-linked-e2e@example.invalid',
      password: randomBytes(12).toString('base64url'),
    },
    manager: {
      email: 'manager-e2e@example.invalid',
      password: randomBytes(12).toString('base64url'),
    },
    managerLinked: {
      email: 'manager-linked-e2e@example.invalid',
      password: randomBytes(12).toString('base64url'),
    },
    adminLinked: {
      email: 'admin-linked-e2e@example.invalid',
      password: randomBytes(12).toString('base64url'),
    },
  };
  await run(
    'dotnet',
    [
      'run',
      '--project',
      'server/server.Cli',
      '--',
      'bootstrap-platform',
      '--environment',
      'Development',
      '--connection-env',
      'LOTO_MOBILE_SHELL_CONNECTION',
      '--name',
      'Operador E2E',
      '--email',
      users.operator.email,
      '--execute',
    ],
    {
      cwd: repoRoot,
      env: {
        ...baseEnv,
        LOTO_MOBILE_SHELL_CONNECTION: connection,
        LOTO_CLI_AccountSecurity__ApplicationUrl: `${accountUrl}/`,
        ...Object.fromEntries(
          Object.entries(mailEnv).map(([key, value]) => [`LOTO_CLI_SecurityMail__${key}`, value]),
        ),
      },
      maxBuffer: 64 * 1024 * 1024,
    },
  );
  const call = apiCall(apiUrl);
  await call('POST', '/user/reset-password', {
    token: await capability(mailUrl, users.operator.email, seen),
    newPassword: users.operator.password,
  });
  const login = async (user) => (await call('POST', '/user/login', user)).responseToken.token;
  const operatorToken = await login(users.operator);
  const onboard = (name, ownerEmail) =>
    call(
      'POST',
      '/platform/onboarding',
      {
        requestId: randomUUID(),
        name,
        ownerEmail,
        terminals: [],
        additionalProducts: [],
        operators: [{ name: `Operadora ${name}`, terminalIndexes: [] }],
      },
      operatorToken,
    );
  const alfa = await onboard('Lotérica Alfa E2E', users.ana.email);
  await call('POST', '/invitation/register', {
    token: await capability(mailUrl, users.ana.email, seen),
    name: 'Ana E2E',
    password: users.ana.password,
  });
  const beta = await onboard('Lotérica Beta E2E', users.ana.email);
  await call(
    'POST',
    '/invitation/accept',
    { token: await capability(mailUrl, users.ana.email, seen) },
    await login(users.ana),
  );
  const gama = await onboard('Lotérica Gama E2E', users.bia.email);
  await call('POST', '/invitation/register', {
    token: await capability(mailUrl, users.bia.email, seen),
    name: 'Bia E2E',
    password: users.bia.password,
  });
  // Ana (Admin of Alfa) invites a Member; Carla joins through the invitation.
  const anaIdentity = await login(users.ana);
  const alfaToken = (
    await call('POST', '/branch/session', { branchId: alfa.branchId }, anaIdentity)
  ).token;
  await call('POST', '/invitation', { email: users.carla.email, role: 'Member' }, alfaToken);
  await call('POST', '/invitation/register', {
    token: await capability(mailUrl, users.carla.email, seen),
    name: 'Carla E2E',
    password: users.carla.password,
  });
  // A bank account (no open day needed) and a transaction type that accepts it, proven once here.
  const account = await call('POST', '/account/bank', { name: 'Banco E2E' }, alfaToken);
  const operators = await call('GET', '/operator', undefined, alfaToken);
  const types = await call('GET', '/transaction-type', undefined, alfaToken);
  const current = await call('GET', '/branch/current', undefined, alfaToken);
  const operatorId = operators.operators[0]?.id ?? '';
  let transactionTypeId = '';
  for (const type of types.items) {
    const response = await fetch(`${apiUrl}/transaction`, {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        authorization: `Bearer ${alfaToken}`,
        'Idempotency-Key': randomUUID(),
      },
      body: JSON.stringify({
        date: current.branchLocalDate,
        value: '1.00',
        transactionTypeId: type.id,
        accountId: account.id,
        recordedByOperatorId: operatorId,
        description: 'E2E probe',
      }),
    });
    if (response.ok) {
      transactionTypeId = type.id;
      break;
    }
  }
  if (!transactionTypeId)
    throw new Error('No seeded transaction type accepts a bank-account movement.');
  // M4 role/link fixtures use ordinary invitations, registration and account assignment only.
  const terminal = await call(
    'POST',
    '/account/terminal',
    { name: 'Caixa shell E2E', createTabAccount: true },
    alfaToken,
  );
  for (const [key, role, linked] of [
    ['memberLinked', 'Member', true],
    ['manager', 'Manager', false],
    ['managerLinked', 'Manager', true],
    ['adminLinked', 'Admin', true],
  ]) {
    const user = users[key];
    await call('POST', '/invitation', { email: user.email, role }, alfaToken);
    const registered = await call('POST', '/invitation/register', {
      token: await capability(mailUrl, user.email, seen),
      name: `Pessoa ${key} E2E`,
      password: user.password,
    });
    if (linked) {
      const employee = await call(
        'POST',
        '/operator',
        { name: `Pessoa ${key} E2E`, userId: registered.userId },
        alfaToken,
      );
      await call(
        'POST',
        `/operator/${employee.id}/accounts`,
        { accountId: terminal.id },
        alfaToken,
      );
    }
  }
  const state = {
    accountUrl,
    apiUrl,
    mailUrl,
    tokenMinutes,
    users,
    branches: { alfa: alfa.branchId, beta: beta.branchId, gama: gama.branchId },
    transaction: { accountId: account.id, transactionTypeId, operatorId },
    owned: owned(),
  };
  await persist(state);
  return stateFile;
}
/** Stops only what `startStack` started (by recorded name/pid), then forgets the state. */
export async function stopStack() {
  const file = process.env.LOTO_MOBILE_SHELL_STATE;
  if (!file) return;
  const state = JSON.parse(await readFile(file, 'utf8'));
  for (const pid of state.owned.pids) {
    try {
      process.kill(-pid, 'SIGTERM');
    } catch {
      // Already gone.
    }
  }
  for (const name of state.owned.containers) {
    await run('docker', ['rm', '-f', name]).catch(() => undefined);
  }
}
