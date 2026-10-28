import { readdirSync } from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { nativeEnvironment } from './native-environment.mjs';

const root = fileURLToPath(new URL('../', import.meta.url));
const env = nativeEnvironment();
const sdk = env.ANDROID_HOME;
function run(command, args) {
  return spawnSync(command, args, { cwd: root, env, stdio: 'inherit' });
}
const [mode = 'doctor', ...args] = process.argv.slice(2);
if (mode === 'proof-android') {
  if (!sdk) throw new Error('Android SDK not found; run native:doctor.');
  const result = run('node', [
    'tooling/native-android-proof.mjs',
    path.join(sdk, 'platform-tools/adb'),
    ...args,
  ]);
  if (result.error) throw result.error;
  process.exitCode = result.status ?? 1;
} else if (mode === 'start') {
  // Both simulators can reach IPv4 loopback; do not expose source bundles on the LAN.
  env.NODE_OPTIONS = `${env.NODE_OPTIONS ?? ''} --dns-result-order=ipv4first`.trim();
  const result = run('pnpm', ['exec', 'expo', 'start', '--dev-client', '--localhost', ...args]);
  if (result.error) throw result.error;
  process.exitCode = result.status ?? 1;
} else if (mode === 'doctor') {
  console.log('Native tools (discovery only; no installation or device changes)');
  const checks = [];
  checks.push(run('node', ['--version']));
  checks.push(run('java', ['-version']));
  checks.push(run('xcodebuild', ['-version']));
  checks.push(run('pod', ['--version']));
  run('adb', ['devices', '-l']);
  run('xcrun', ['simctl', 'list', 'devices', 'booted']);
  if (sdk) console.log('Android SDK platforms:', readdirSync(path.join(sdk, 'platforms')));
  process.exitCode = !env.JAVA_HOME || !sdk || checks.some((check) => check.status !== 0) ? 1 : 0;
} else if (mode === 'ios' || mode === 'android') {
  if (mode === 'android' && (!env.JAVA_HOME || !sdk))
    throw new Error('Install/configure a JDK 17 and Android SDK; run native:doctor.');
  const result = run('pnpm', ['exec', 'expo', `run:${mode}`, ...args]);
  if (result.error) throw result.error;
  process.exitCode = result.status ?? 1;
} else {
  throw new Error('Expected doctor, start, proof-android, ios or android');
}
