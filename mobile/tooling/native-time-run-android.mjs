// Run generated native command chunks, using actual OS IME state before Back. Never resets a device.
import { execFileSync, spawn } from 'node:child_process';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { openSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { nativeEnvironment } from './native-environment.mjs';
import { imeWindowVisible } from './native-ime.mjs';
import { nativeAppId } from './native-app.mjs';
const [serial, maestro, flow, directory] = process.argv.slice(2);
const root = fileURLToPath(new URL('../../', import.meta.url));
if (
  !/^emulator-\d+$/.test(serial ?? '') ||
  !path.isAbsolute(maestro ?? '') ||
  !path.isAbsolute(directory ?? '') ||
  path.resolve(directory).startsWith(root)
)
  throw new Error(
    'Use explicit emulator, local Maestro, generated flow and outside-checkout artifacts.',
  );
const environment = nativeEnvironment();
const adb = path.join(environment.ANDROID_HOME, 'platform-tools/adb');
const source = await readFile(flow, 'utf8');
const app = JSON.parse(source.split('\n')[0]).appId;
if (app !== nativeAppId()) throw new Error('Foreign app refused.');
const commands = source
  .split('\n')
  .filter((line) => line.startsWith('- '))
  .map((line) => JSON.parse(line.slice(2)));
await mkdir(directory, { recursive: true });
const evidence = [];
let chunk = [];
let index = 0;
async function runChunk() {
  if (!chunk.length) return;
  const label = 'chunk-' + String(++index).padStart(2, '0');
  const file = path.join(directory, label + '.yaml');
  await writeFile(
    file,
    JSON.stringify({ appId: app }) +
      '\n---\n' +
      chunk.map((command) => '- ' + JSON.stringify(command)).join('\n') +
      '\n',
    { mode: 0o600 },
  );
  const log = openSync(path.join(directory, label + '.log'), 'w', 0o600);
  const child = spawn(
    maestro,
    ['--device', serial, 'test', file, '--test-output-dir', path.join(directory, label)],
    { env: environment, stdio: ['ignore', log, log] },
  );
  const code = await new Promise((resolve, reject) => {
    child.on('error', reject);
    child.on('exit', resolve);
  });
  if (code !== 0) throw new Error('Native chunk failed: ' + label + '; inspect private artifacts.');
  evidence.push({ chunk: label, exit: code });
  console.log('Completed native ' + label);
  chunk = [];
}
for (const command of commands) {
  if (command.hideKeyboard || command.pressKey === 'Enter') {
    await runChunk();
    const activity = execFileSync(
      adb,
      ['-s', serial, 'shell', 'dumpsys', 'activity', 'activities'],
      { encoding: 'utf8' },
    );
    const resumed = activity
      .split('\n')
      .filter((line) => /mResumedActivity|topResumedActivity/.test(line))
      .join('\n');
    if (!resumed.includes(app)) throw new Error('Assigned app is not resumed; Back refused.');
    const diagnostics = execFileSync(adb, ['-s', serial, 'shell', 'dumpsys', 'input_method'], {
      encoding: 'utf8',
    });
    const visible = imeWindowVisible(diagnostics);
    if (visible) execFileSync(adb, ['-s', serial, 'shell', 'input', 'keyevent', '4']);
    evidence.push({ keyboardWindowVisible: visible, closed: visible });
    await writeFile(path.join(directory, 'checks.json'), JSON.stringify(evidence, null, 2), {
      mode: 0o600,
    });
  } else chunk.push(command);
}
await runChunk();
await writeFile(path.join(directory, 'checks.json'), JSON.stringify(evidence, null, 2), {
  mode: 0o600,
});
console.log('Native Android flow completed with observed IME handling.');
