// Runs a generated M5 journey (the same Maestro-style command list) on an explicit Android
// emulator through adb + UIAutomator. Maestro's Android search treats off-screen React Native
// nodes as visible and its fast search swipes do not move this scroll view; here visibility is the
// node's real on-screen bounds and scrolling uses slow drags. Never resets data or the device.
import { execFileSync } from 'node:child_process';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { nativeEnvironment } from './native-environment.mjs';
import { nativeNodes } from './native-ui.mjs';
import { imeWindowVisible } from './native-ime.mjs';
import { nativeAppId } from './native-app.mjs';
const [serial, flow, directory] = process.argv.slice(2);
const root = fileURLToPath(new URL('../../', import.meta.url));
if (
  !/^emulator-\d+$/.test(serial ?? '') ||
  !path.isAbsolute(flow ?? '') ||
  !path.isAbsolute(directory ?? '') ||
  path.resolve(directory).startsWith(root)
)
  throw new Error('Use an explicit emulator, a generated flow and an outside-checkout directory.');
const adb = path.join(nativeEnvironment().ANDROID_HOME, 'platform-tools/adb');
const source = await readFile(flow, 'utf8');
const app = JSON.parse(source.split('\n')[0]).appId;
if (app !== nativeAppId()) throw new Error('Foreign app refused.');
const commands = source
  .split('\n')
  .filter((line) => line.startsWith('- '))
  .map((line) => JSON.parse(line.slice(2)));
await mkdir(directory, { recursive: true });
const sh = (...args) =>
  execFileSync(adb, ['-s', serial, ...args], { encoding: 'utf8', maxBuffer: 64 << 20 });
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const devUrl =
  'exp+lotero://expo-development-client/?url=' +
  encodeURIComponent('http://127.0.0.1:' + (process.env.LOTO_METRO_PORT ?? '8083'));
const [width, height] = /(\d+)x(\d+)/
  .exec(sh('shell', 'wm', 'size'))
  .slice(1)
  .map(Number);
const log = [];
const started = Date.now();
const say = (line) => {
  // Elapsed seconds since launch: the dev client reloads JS about three minutes after process start.
  const stamped = `[${Math.round((Date.now() - started) / 1000)}s] ${line}`;
  log.push(stamped);
  console.log(stamped);
};
let bottom = 0;
function nodes() {
  // An open keypad covers the lower screen: nothing under it counts as tappable.
  bottom = imeWindowVisible(sh('shell', 'dumpsys', 'input_method')) ? height * 0.66 : height - 90;
  // uiautomator refuses to dump while the screen is still animating (cold start, caret blink): retry.
  for (let attempt = 1; ; attempt++) {
    try {
      sh('shell', 'uiautomator', 'dump', '/sdcard/loto-m5.xml');
      break;
    } catch (error) {
      if (attempt >= 6) throw error;
      Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 1000);
    }
  }
  return nativeNodes(sh('shell', 'cat', '/sdcard/loto-m5.xml')).map((n) => {
    const b = /\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\]/.exec(n.bounds ?? '');
    return { ...n, box: b ? b.slice(1).map(Number) : null };
  });
}
// The app header occupies the top; content below the gesture bar is not tappable.
// Header controls sit above it; scroll content must clear the header (no tap under the top bar).
const header = 340;
const onScreen = (n) =>
  !!n.box &&
  n.box[3] > n.box[1] &&
  n.box[2] > n.box[0] &&
  n.box[1] >= 60 &&
  n.box[3] <= bottom &&
  (n.box[1] >= header || n.box[3] <= header);
const matches = (n, selector) => {
  if (typeof selector === 'string') selector = { text: selector };
  if (selector.id) return new RegExp(`^(?:${selector.id})$`).test(n['resource-id'] ?? '');
  const re = new RegExp(`^(?:${selector.text})$`, 's');
  return re.test(n.text ?? '') || re.test(n['content-desc'] ?? '');
};
const find = (selector, list = nodes()) => list.find((n) => matches(n, selector) && onScreen(n));
const present = (selector, list = nodes()) => list.some((n) => matches(n, selector) && n.box);
const drag = (fromY, toY) =>
  // `input swipe` ignores fractional coordinates: always integers. The drag runs along the left
  // page margin: one starting on a text field is taken by the EditText instead of the scroll view.
  sh(
    'shell',
    'input',
    'swipe',
    String(Math.round(width * 0.03)),
    String(Math.round(fromY)),
    String(Math.round(width * 0.03)),
    String(Math.round(toY)),
    '900',
  );
async function reveal(selector, budget = 60) {
  for (const direction of ['down', 'up'])
    for (let i = 0; i < budget; i++) {
      const list = nodes();
      const hit = find(selector, list);
      if (process.env.DAY_DEBUG)
        console.log(
          direction,
          i,
          list
            .filter((n) => /calc$/.test(n['resource-id'] ?? ''))
            .map((n) => n['resource-id'].slice(12, 20) + n.bounds)
            .join(' '),
        );
      if (hit) return hit;
      const off = list.find((n) => matches(n, selector) && n.box && n.box[3] > n.box[1]);
      // Move toward a known off-screen node; otherwise sweep in the current direction.
      const down = off ? off.box[1] > height / 2 : direction === 'down';
      // Drag in the band above an open keypad (which starts near 68% of the height).
      // LOTO_FAST_SCROLL=1 uses long drags for long review pages (default keeps the short M5 drags).
      const [near, far] = process.env.LOTO_FAST_SCROLL === '1' ? [0.82, 0.2] : [0.58, 0.3];
      if (down) drag(height * near, height * far);
      else drag(height * far, height * near);
      await sleep(700);
    }
  return null;
}
async function waitFor(selector, timeout, visible = true) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    if (visible ? find(selector) : !find(selector)) return true;
    await sleep(800);
  }
  return false;
}
const tapNode = (n) =>
  sh(
    'shell',
    'input',
    'tap',
    String(Math.round((n.box[0] + n.box[2]) / 2)),
    String(Math.round((n.box[1] + n.box[3]) / 2)),
  );
const typeText = (value) =>
  sh('shell', 'input', 'text', value.replace(/ /g, '%s').replace(/([()&|;<>*'"\\$`])/g, '\\$1'));
let step = 0;
async function run(list) {
  for (const command of list) {
    const [name, arg] = Object.entries(command)[0];
    const label = `${String(++step).padStart(3, '0')} ${name} ${JSON.stringify(arg).slice(0, 120)}`;
    switch (name) {
      case 'launchApp':
        if (arg?.stopApp !== false) sh('shell', 'am', 'force-stop', app);
        // A development build opens its launcher; load the owned Metro project explicitly.
        sh('shell', 'am', 'start', '-a', 'android.intent.action.VIEW', '-d', devUrl, app);
        await sleep(6000);
        await sleep(3000);
        break;
      case 'stopApp':
        sh('shell', 'am', 'force-stop', app);
        break;
      case 'openLink':
        // One quoted remote argument: the device shell would split the query at `&`.
        sh(
          'shell',
          `am start -a android.intent.action.VIEW -d '${arg.link.replace(/'/g, '')}' ${app}`,
        );
        await sleep(2500);
        break;
      case 'waitForAnimationToEnd':
        await sleep(Math.min(arg?.timeout ?? 1000, 1500));
        break;
      case 'extendedWaitUntil': {
        const target = arg.visible ?? arg.notVisible;
        const selector = typeof target === 'string' ? { text: target } : target;
        const total = arg.timeout ?? 30000;
        // A visible-wait target may be in the scroll content: look briefly, scroll to find it, and only
        // then spend the rest of the timeout waiting (a long idle wait would outlast the dev client).
        let ok = await waitFor(
          selector,
          arg.visible ? Math.min(total, 6000) : total,
          !!arg.visible,
        );
        if (!ok && arg.visible) ok = !!(await reveal(selector, 20));
        if (!ok && arg.visible) ok = await waitFor(selector, total, true);
        if (!ok) throw new Error('Wait failed: ' + label);
        break;
      }
      case 'assertNotVisible':
        if (find({ text: arg })) throw new Error('Unexpectedly visible: ' + label);
        break;
      case 'tapOn': {
        const selector = arg.id ? { id: arg.id } : { text: arg.text };
        const hit = (await reveal(selector)) ?? null;
        if (!hit) throw new Error('Tap target missing: ' + label);
        // A drag can leave fling momentum: let the list settle and re-read the node before tapping.
        await sleep(900);
        tapNode(find(selector) ?? hit);
        await sleep(900);
        break;
      }
      case 'longPressOn':
        break;
      case 'inputText':
        typeText(arg);
        await sleep(500);
        break;
      case 'eraseText':
        for (let i = 0; i < Math.min(arg, 30); i++) sh('shell', 'input', 'keyevent', '67');
        break;
      case 'pressKey':
        if (arg === 'Enter') sh('shell', 'input', 'keyevent', '66');
        // Back closes an open keypad first, so a dialog's confirm stays reachable without a drag.
        // Only while the keypad is actually shown: with no keypad, Back would navigate away.
        else if (arg === 'Back') {
          if (imeWindowVisible(sh('shell', 'dumpsys', 'input_method')))
            sh('shell', 'input', 'keyevent', '4');
        } else throw new Error('Unsupported key ' + arg);
        break;
      case 'swipe':
        // Generated content drags only (never edge gestures); executed slowly.
        drag(
          (height * parseInt(arg.start.split(',')[1])) / 100,
          (height * parseInt(arg.end.split(',')[1])) / 100,
        );
        await sleep(700);
        break;
      case 'scrollUntilVisible': {
        const hit = await reveal(arg.element);
        if (!hit && !arg.optional) throw new Error('Not found: ' + label);
        break;
      }
      case 'takeScreenshot':
        await writeFile(
          path.join(directory, arg + '.png'),
          execFileSync(adb, ['-s', serial, 'exec-out', 'screencap', '-p'], { maxBuffer: 64 << 20 }),
        );
        break;
      case 'hideKeyboard':
        break;
      case 'repeat':
        // Search loops are replaced by real on-screen reveal of their target.
        if (arg.while?.notVisible) await reveal(arg.while.notVisible);
        break;
      case 'runFlow':
        if (arg.when?.visible) {
          const sel =
            typeof arg.when.visible === 'string' ? { text: arg.when.visible } : arg.when.visible;
          if (!find(sel)) break;
        }
        if (arg.when?.notVisible) {
          const sel =
            typeof arg.when.notVisible === 'string'
              ? { text: arg.when.notVisible }
              : arg.when.notVisible;
          if (present(sel) && find(sel)) break;
        }
        await run(arg.commands);
        break;
      default:
        throw new Error('Unsupported command ' + name);
    }
    say(label + ' ok');
  }
}
try {
  await run(commands);
  say('PASS');
} catch (error) {
  say('FAIL ' + error.message);
  await writeFile(
    path.join(directory, 'failure.png'),
    execFileSync(adb, ['-s', serial, 'exec-out', 'screencap', '-p'], { maxBuffer: 64 << 20 }),
  );
  process.exitCode = 1;
} finally {
  await writeFile(path.join(directory, 'run.log'), log.join('\n'), { mode: 0o600 });
}
