// Keep the same owned build/database. Default to one-minute tokens so journeys exercise renewal;
// an explicit 1–15-minute duration is available for other owned fixture experiments.
import { execFileSync, spawn } from 'node:child_process';
import { readFile, writeFile } from 'node:fs/promises';
import { openSync } from 'node:fs';
import path from 'node:path';
import { repoRoot } from './native-stack.mjs';
const [statePath, duration = '1'] = process.argv.slice(2);
const minutes = Number(duration);
if (!Number.isInteger(minutes) || minutes < 1 || minutes > 15)
  throw new Error('Use an owned M9 state and 1–15 token minutes.');
const s = JSON.parse(await readFile(statePath, 'utf8'));
if (
  !s.owned ||
  !s.time ||
  !s.owned.containers.every((name) => /^loto-mobile-shell-(?:db|mail)-[a-f0-9]+$/.test(name)) ||
  new URL(s.apiUrl).hostname !== '127.0.0.1'
)
  throw new Error('Owned M9 state required.');
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
    continue; // An exited API PID is never treated as a live resource.
  }
  const cwd = execFileSync('lsof', ['-a', '-p', String(pid), '-d', 'cwd', '-Fn'], {
    encoding: 'utf8',
  });
  if (
    !command.includes('dotnet run --project server/server.API') ||
    !cwd.split('\n').includes('n' + path.resolve(repoRoot))
  )
    throw new Error('Recorded PID ownership no longer matches this checkout; stop refused.');
  process.kill(-pid, 'SIGTERM');
}
const output = openSync(path.join(s.owned.directory, 'm9-api.log'), 'a');
const child = spawn(
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
    },
    detached: true,
    stdio: ['ignore', output, output],
  },
);
child.unref();
s.owned.pids.push(child.pid);
s.tokenMinutes = minutes;
await writeFile(statePath, JSON.stringify(s), { mode: 0o600 });
const deadline = Date.now() + 20000;
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
  `Same owned API/database restarted with ${minutes}-minute tokens; no build or data reset.`,
);
