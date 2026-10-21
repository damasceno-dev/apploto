import { createRequire } from 'node:module';
import { spawn } from 'node:child_process';
import nextEnv from '@next/env';

const [mode, ...args] = process.argv.slice(2);
if (!['dev', 'build', 'start'].includes(mode)) throw new Error('Expected dev, build or start.');
nextEnv.loadEnvConfig(process.cwd(), mode === 'dev');
// The launcher runs with the server condition; this exact module also guards Server Components.
await import('../src/config/server-env.ts');
const require = createRequire(import.meta.url);
const child = spawn(process.execPath, [require.resolve('next/dist/bin/next'), mode, ...args], {
  stdio: 'inherit',
  env: process.env,
});
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => child.kill(signal));
child.on('error', (error) => {
  console.error(error.message);
  process.exitCode = 1;
});
child.on('exit', (code) => {
  process.exitCode = code ?? 1;
});
