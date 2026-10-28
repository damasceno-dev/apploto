// Native UIAutomator/adb fallback for Android 16 stylus/IME interception in Maestro.
// No remote JS, system input-setting change, app-data reset or direct storage manipulation.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { nativeAppId } from './native-app.mjs';
import { nativeEnvironment } from './native-environment.mjs';
import { imeWindowVisible } from './native-ime.mjs';
import { nativeNodes as nodes } from './native-ui.mjs';
const [serial, statePath, fixturePath, output, port = '8083'] = process.argv.slice(2);
if (!serial || !statePath || !fixturePath || !output)
  throw new Error(
    'Usage: node tooling/native-session-android.mjs <serial> <owned-state> <fixture-json> <outside-checkout-output> [Metro-port]',
  );
const root = fileURLToPath(new URL('../../', import.meta.url));
if (!path.isAbsolute(output) || path.resolve(output).startsWith(root))
  throw new Error('Keep native captures outside the checkout.');
const state = JSON.parse(await readFile(statePath, 'utf8'));
const fixture = JSON.parse(await readFile(fixturePath, 'utf8'));
if (
  !state.users?.ana?.email?.endsWith('@example.invalid') ||
  !['127.0.0.1', 'localhost'].includes(new URL(state.apiUrl).hostname)
)
  throw new Error('Use owned disposable fixtures only.');
const env = nativeEnvironment();
assert(env.ANDROID_HOME, 'Android SDK not found.');
const adb = path.join(env.ANDROID_HOME, 'platform-tools/adb');
const run = (...args) =>
  execFileSync(adb, ['-s', serial, ...args], { env, encoding: 'utf8', timeout: 30000 });
const app = nativeAppId();
const deviceFile = `/sdcard/loto-session-${process.pid}.xml`;
function dump() {
  const result = run('shell', 'uiautomator', 'dump', deviceFile);
  if (!result.includes('UI hierchary dumped')) throw new Error('Native UI is not ready.');
  return run('shell', 'cat', deviceFile);
}
const pause = () => new Promise((resolve) => setTimeout(resolve, 300));
const screen = /Physical size: (\d+)x(\d+)/.exec(run('shell', 'wm', 'size'));
assert(screen, 'Missing native screen dimensions.');
const width = Number(screen[1]),
  height = Number(screen[2]);
function swipe(direction) {
  run(
    'shell',
    'input',
    'touchscreen',
    'swipe',
    String(width / 2),
    String(Math.floor(height * (direction === 'UP' ? 0.3 : 0.75))),
    String(width / 2),
    String(Math.floor(height * (direction === 'UP' ? 0.75 : 0.3))),
    '350',
  );
}
async function view(id, direction = 'DOWN', expected) {
  const deadline = Date.now() + 120000;
  let last = '';
  let mismatchReported = false;
  while (Date.now() < deadline) {
    let xml;
    try {
      xml = dump();
    } catch {
      await pause();
      continue;
    }
    last = xml;
    const all = nodes(xml);
    const menuClose = all.find((n) => n['content-desc'] === 'Close');
    if (menuClose && all.some((n) => n.text === 'Open DevTools')) {
      const b = /\[(\d+),(\d+)\]\[(\d+),(\d+)\]/.exec(menuClose.bounds);
      assert(b);
      run(
        'shell',
        'input',
        'touchscreen',
        'tap',
        String(Math.floor((+b[1] + +b[3]) / 2)),
        String(Math.floor((+b[2] + +b[4]) / 2)),
      );
      await pause();
      continue;
    }
    // Fresh disposable installs can show Expo's app-local developer onboarding.
    const onboarding = all.find((n) => n.text === 'Continue');
    if (onboarding && all.some((n) => n.text?.startsWith('This is the developer menu.'))) {
      const bounds = /\[(\d+),(\d+)\]\[(\d+),(\d+)\]/.exec(onboarding.bounds);
      assert(bounds);
      run(
        'shell',
        'input',
        'touchscreen',
        'tap',
        String(Math.floor((+bounds[1] + +bounds[3]) / 2)),
        String(Math.floor((+bounds[2] + +bounds[4]) / 2)),
      );
      await pause();
      continue;
    }
    // Dismiss an observed IME/tutorial window only; never send Back blindly into the app.
    const imeShown = imeWindowVisible(run('shell', 'dumpsys', 'input_method'));
    if (
      imeShown ||
      all.some((n) => n['resource-id']?.startsWith('com.google.android.inputmethod'))
    ) {
      run('shell', 'input', 'keyevent', 'KEYCODE_BACK');
      await pause();
      continue;
    }
    const node = all.find((n) => n['resource-id'] === id);
    if (node && node.enabled === 'true') {
      const box = /\[(\d+),(\d+)\]\[(\d+),(\d+)\]/.exec(node.bounds);
      if (
        id !== 'session-operation-result' &&
        box &&
        (+box[2] < height * 0.1 || +box[4] > height * 0.88)
      ) {
        swipe(+box[2] < height * 0.1 ? 'UP' : 'DOWN');
        await pause();
        continue;
      }
      if (expected === undefined || node.text === expected) return node;
      if (!mismatchReported) {
        console.log(
          `Native field awaiting expected value: ${id}; observed length ${node.text?.length ?? 0}, expected length ${expected.length}`,
        );
        mismatchReported = true;
      }
      await pause();
      continue;
    }
    swipe(direction);
    await pause();
  }
  await writeFile(path.join(output, `timeout-${id}.xml`), last);
  throw new Error(`Native condition timed out for ${id}`);
}
async function tap(id, direction) {
  const node = await view(id, direction);
  const b = /\[(\d+),(\d+)\]\[(\d+),(\d+)\]/.exec(node.bounds);
  assert(b);
  console.log(`Native control: ${id}`);
  run(
    'shell',
    'input',
    'touchscreen',
    'tap',
    String(Math.floor((Number(b[1]) + Number(b[3])) / 2)),
    String(Math.floor((Number(b[2]) + Number(b[4])) / 2)),
  );
}
async function loadFixture() {
  await tap('session-load-fixture');
  await view('session-fixture-state', 'DOWN', 'fixture-ready');
}
async function cold() {
  run('shell', 'am', 'force-stop', app);
  run(
    'shell',
    'am',
    'start',
    '-a',
    'android.intent.action.VIEW',
    '-d',
    `exp+lotero://expo-development-client/?url=http%3A%2F%2F127.0.0.1%3A${port}`,
    app,
  );
  await pause();
}
await mkdir(output, { recursive: true });
const metro = await fetch(`http://127.0.0.1:${port}/status`);
assert.equal(metro.headers.get('x-react-native-project-root'), path.join(root, 'mobile'));
const notes = 'android retained input final';
try {
  if (!process.argv.includes('--continue-financial')) {
    await cold();
    let status = await view('session-status');
    const readyDeadline = Date.now() + 60000;
    while (['starting', 'recovering'].includes(status.text) && Date.now() < readyDeadline) {
      await pause();
      status = await view('session-status');
    }
    assert(
      !['starting', 'recovering'].includes(status.text),
      'Native session did not finish restoring.',
    );
    if (status.text !== 'anonymous') {
      await tap('session-logout');
      await view('session-status', 'UP', 'anonymous');
    }
    await loadFixture();
    await tap('session-login');
    await view('session-status', 'UP', 'identity');
    console.log('Native identity gate verified.');
    await loadFixture();
    await tap('session-select-branch');
    await view('session-status', 'UP', 'branch');
    await view('session-write-gate', 'DOWN', 'contexto verificado');
    await writeFile(path.join(output, 'verified-branch.xml'), dump());
    console.log('Native branch role/clock gate verified.');
    await loadFixture();
    await view('session-note', 'DOWN', notes);
    await tap('session-save-note');
    await view('session-note-state', 'DOWN', 'Nota guardada neste dispositivo');
    await tap('session-hold-intent');
    await tap('session-resume-intent');
    await view('session-resumed-intent', 'DOWN', '/transacoes');
  }
  await view('session-financial-payload', 'DOWN', JSON.stringify(fixture.body));
  await tap('session-lose-response');
  await tap('session-create-fixture');
  await view('session-operation-kind', 'DOWN', 'uncertain');
  const unknown = JSON.parse((await view('session-operation-result')).text);
  await writeFile(path.join(output, 'uncertain.json'), JSON.stringify(unknown, null, 2));
  console.log('Native response loss retained the exact operation.');
  await cold();
  await view('session-status', 'UP', 'branch');
  await tap('session-replay-fixture');
  await view('session-operation-kind', 'DOWN', 'confirmed');
  const confirmed = JSON.parse((await view('session-operation-result')).text);
  assert.equal(confirmed.data.status, 201);
  assert.equal(confirmed.deviceRecordCleared, true);
  const headers = confirmed.data.headers.map ?? confirmed.data.headers;
  assert.match(headers.etag, /^"\d+"$/);
  await writeFile(path.join(output, 'confirmed.json'), JSON.stringify(confirmed, null, 2));
  await tap('session-restore-note', 'UP');
  await view('session-note-state', 'DOWN', 'found');
  await view('session-note', 'UP', notes);
  await tap('session-grants', 'UP');
  assert((await view('session-grants-result')).text.includes('availableAccounts'));
  await writeFile(
    path.join(output, 'summary.json'),
    JSON.stringify(
      {
        identityGate: true,
        branchClock: true,
        durableInput: true,
        heldIntent: true,
        uncertainRestartReplay: true,
        nativeResponseETag: true,
        currentGrants: true,
      },
      null,
      2,
    ),
  );
  console.log('Native restart, exact replay, ETag, input restoration and fresh grants verified.');
} finally {
  run('shell', 'rm', '-f', deviceFile);
}
