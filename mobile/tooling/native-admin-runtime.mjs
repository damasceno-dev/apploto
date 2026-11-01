// M10: restart the same owned API/database with the deterministic loopback holiday provider and
// one-minute tokens (renewal during admin editing). No build, data reset or external source.
import { execFileSync, spawn } from 'node:child_process';
import { openSync } from 'node:fs';
import { readFile, writeFile } from 'node:fs/promises';
import { createServer } from 'node:net';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { repoRoot } from './native-stack.mjs';
const [statePath, duration = '1'] = process.argv.slice(2);
const minutes = Number(duration);
if (!Number.isInteger(minutes) || minutes < 1 || minutes > 15)
  throw new Error('Use <owned-state.json> [1–15 token minutes].');
const s = JSON.parse(await readFile(statePath, 'utf8'));
if (
  !s.owned ||
  !s.owned.containers.every((name) => /^loto-mobile-shell-(?:db|mail)-[a-f0-9]+$/.test(name)) ||
  new URL(s.apiUrl).hostname !== '127.0.0.1'
)
  throw new Error('Owned loopback state required.');
const inspect = (name) =>
  JSON.parse(execFileSync('docker', ['inspect', name], { encoding: 'utf8' }))[0];
const db = inspect(s.owned.containers.find((name) => name.includes('-db-')));
const mail = inspect(s.owned.containers.find((name) => name.includes('-mail-')));
const password = db.Config.Env.find((item) => item.startsWith('POSTGRES_PASSWORD=')).slice(
  'POSTGRES_PASSWORD='.length,
);
const dbPort = db.NetworkSettings.Ports['5432/tcp'][0].HostPort;
const smtpPort = mail.NetworkSettings.Ports['1025/tcp'][0].HostPort;
for (const pid of s.owned.pids) {
  let command;
  try {
    command = execFileSync('ps', ['-p', String(pid), '-o', 'command='], { encoding: 'utf8' });
  } catch {
    continue; // An exited PID is never treated as a live resource.
  }
  const cwd = execFileSync('lsof', ['-a', '-p', String(pid), '-d', 'cwd', '-Fn'], {
    encoding: 'utf8',
  });
  const ours =
    command.includes('dotnet run --project server/server.API') ||
    command.includes('native-holiday-provider.mjs');
  if (!ours || !cwd.split('\n').includes('n' + path.resolve(repoRoot)))
    throw new Error('Recorded PID ownership no longer matches this checkout; stop refused.');
  process.kill(-pid, 'SIGTERM');
}
const port = await new Promise((resolve, reject) => {
  const server = createServer();
  server.listen(0, '127.0.0.1', () => {
    const address = server.address();
    server.close(() => (typeof address === 'object' && address ? resolve(address.port) : reject()));
  });
});
const log = openSync(path.join(s.owned.directory, 'm10-api.log'), 'a');
const provider = spawn(
  process.execPath,
  [fileURLToPath(new URL('./native-holiday-provider.mjs', import.meta.url)), String(port)],
  { cwd: repoRoot, detached: true, stdio: ['ignore', log, log] },
);
provider.unref();
const holidays = `http://127.0.0.1:${port}`;
const api = spawn(
  'dotnet',
  ['run', '--project', 'server/server.API', '--no-launch-profile', '--no-build'],
  {
    cwd: repoRoot,
    env: {
      ...process.env,
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
      ASPNETCORE_ENVIRONMENT: 'Development',
      'Logging__LogLevel__Microsoft.AspNetCore.Hosting.Diagnostics': 'Information',
      ASPNETCORE_URLS: s.apiUrl,
      ConnectionStrings__DefaultConnection: `Host=127.0.0.1;Port=${dbPort};Database=loto_dev_mobile_shell;Username=postgres;Password=${password}`,
      Token__ExpirationTimeInMinutes: String(minutes),
      AccountSecurity__ApplicationUrl: s.accountUrl + '/',
      AccountSecurity__LoginRequestsPerMinute: '1000',
      SecurityMail__Enabled: 'true',
      SecurityMail__Host: '127.0.0.1',
      SecurityMail__Port: smtpPort,
      SecurityMail__LocalCapture: 'true',
      ExternalHolidaySources__NagerDate__BaseUrl: holidays + '/nager/',
      ExternalHolidaySources__BrasilApi__BaseUrl: holidays + '/brasilapi/',
      ExternalHolidaySources__NagerDate__TimeoutSeconds: '3',
      ExternalHolidaySources__BrasilApi__TimeoutSeconds: '3',
    },
    detached: true,
    stdio: ['ignore', log, log],
  },
);
api.unref();
s.owned.pids = [provider.pid, api.pid];
s.tokenMinutes = minutes;
s.holidayProvider = holidays;
await writeFile(statePath, JSON.stringify(s), { mode: 0o600 });
const deadline = Date.now() + 30000;
while (true) {
  try {
    if ((await fetch(s.apiUrl + '/branch/current')).status === 401) break;
  } catch {
    /* starting */
  }
  if (Date.now() >= deadline) throw new Error('Owned API did not become ready.');
  await new Promise((resolve) => setTimeout(resolve, 250));
}
console.log(
  `Same owned API/database restarted with ${minutes}-minute tokens and the loopback holiday provider.`,
);
