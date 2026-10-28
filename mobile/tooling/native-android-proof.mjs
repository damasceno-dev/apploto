// Native UI runner: no remote JS debugger, Node/browser execution or app-data reset.
import { execFileSync } from 'node:child_process';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { nativeAppId } from './native-app.mjs';
import { nativeNodes } from './native-ui.mjs';

const [adb, serial, output, portText = '8083'] = process.argv.slice(2);
if (!adb || !serial || !output)
  throw new Error(
    'Usage: node tooling/native-android-proof.mjs <adb> <serial> <outside-repo-output> [Metro port]',
  );
const repo = fileURLToPath(new URL('../../', import.meta.url));
if (!path.isAbsolute(output) || path.resolve(output).startsWith(repo))
  throw new Error('Capture outside the checkout');
const port = Number(portText);
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('Invalid Metro port');
const app = nativeAppId();
const run = (...args) =>
  execFileSync(adb, ['-s', serial, ...args], { encoding: 'utf8', timeout: 30000 });
const pause = () => new Promise((resolve) => setTimeout(resolve, 500));
const deviceFile = `/sdcard/loto-proof-${process.pid}.xml`;
function dump() {
  const result = run('shell', 'uiautomator', 'dump', deviceFile);
  if (!result.includes('UI hierchary dumped')) throw new Error('Native UI not ready');
  return run('shell', 'cat', deviceFile);
}
async function until(predicate) {
  const end = Date.now() + 45000;
  let last;
  while (Date.now() < end) {
    try {
      const xml = dump();
      const nodes = nativeNodes(xml);
      const menuClose = nodes.find((node) => node['content-desc'] === 'Close');
      if (
        menuClose &&
        nodes.some((node) => node.text === 'Go home') &&
        nodes.some((node) => node.text === 'Reload')
      ) {
        const bounds = /\[(\d+),(\d+)\]\[(\d+),(\d+)\]/.exec(menuClose.bounds);
        if (!bounds) throw new Error('Missing observed developer-menu close bounds');
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
      const onboarding = nodes.find((node) => node.text === 'Continue');
      if (
        onboarding &&
        nodes.some((node) => node.text?.startsWith('This is the developer menu.'))
      ) {
        const bounds = /\[(\d+),(\d+)\]\[(\d+),(\d+)\]/.exec(onboarding.bounds);
        if (!bounds) throw new Error('Missing observed onboarding bounds');
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
      const value = predicate(xml);
      if (value) return value;
    } catch (error) {
      last = error;
    }
    await pause();
  }
  throw new Error(`Native UI condition timed out: ${String(last ?? 'condition absent')}`);
}
const open = () =>
  run(
    'shell',
    'am',
    'start',
    '-a',
    'android.intent.action.VIEW',
    '-d',
    `lotero://expo-development-client/?url=http%3A%2F%2F127.0.0.1%3A${port}`,
    app,
  );
await mkdir(output, { recursive: true });
const crashStart = run('shell', "date '+%m-%d %H:%M:%S.000'").trim();
const status = await fetch(`http://127.0.0.1:${port}/status`);
if (status.headers.get('x-react-native-project-root') !== path.join(repo, 'mobile'))
  throw new Error('Metro checkout does not match this runner');
run('reverse', `tcp:${port}`, `tcp:${port}`);
const results = [];
try {
  for (let iteration = 1; iteration <= 5; iteration++) {
    const installedApk = run('shell', 'pm', 'path', app)
      .trim()
      .split('\n')[0]
      ?.replace('package:', '');
    if (!installedApk?.endsWith('/base.apk')) throw new Error('Installed app APK not found');
    const apkHash = run('shell', 'sha256sum', installedApk).split(/\s+/)[0];
    const started = performance.now();
    run('shell', 'am', 'force-stop', app);
    open();
    const baseline = await until(
      (xml) => xml.includes('Credenciais consultadas: 0. Requisições: 0.') && xml,
    );
    await writeFile(path.join(output, `cold-${iteration}.xml`), baseline);
    await until(
      (xml) => !xml.includes('text=\"starting\"') && !xml.includes('text=\"recovering\"') && xml,
    );
    let button;
    for (let attempt = 0; attempt < 12 && !button; attempt++) {
      const current = dump();
      button = [...current.matchAll(/<node\b[^>]+>/g)].find((m) =>
        m[0].includes('MEDIR INTL NO HERMES'),
      )?.[0];
      if (!button) run('shell', 'input', 'swipe', '500', '1700', '500', '500', '400');
    }
    const bounds = /bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"/.exec(button ?? '');
    if (!bounds) throw new Error('Measurement button not visible');
    const [x1, y1, x2, y2] = bounds.slice(1).map(Number);
    run(
      'shell',
      'input',
      'tap',
      String(Math.floor((x1 + x2) / 2)),
      String(Math.floor((y1 + y2) / 2)),
    );
    const measurement = await until((xml) => {
      for (const node of nativeNodes(xml)) {
        for (const value of [node.text, node['content-desc']]) {
          if (value?.startsWith('{"intl":')) return JSON.parse(value);
        }
      }
      // The M2 harness can place the newly mounted report below the viewport.
      run('shell', 'input', 'swipe', '500', '1800', '500', '600', '400');
      return null;
    });
    await writeFile(
      path.join(output, `measurement-${iteration}.json`),
      JSON.stringify(measurement, null, 2),
    );
    if (measurement.intl.engine !== 'Hermes' || measurement.core.total !== measurement.core.passed)
      throw new Error(`Native case failure on run ${iteration}`);
    // Exercise scrolling with the long report mounted, then background/foreground the same app.
    run('shell', 'input', 'swipe', '500', '1800', '500', '600', '400');
    run('shell', 'input', 'swipe', '500', '600', '500', '1800', '400');
    run('shell', 'input', 'keyevent', 'KEYCODE_HOME');
    open();
    await until((xml) => xml.includes('Lotero') && xml);
    results.push({
      iteration,
      apkHash,
      elapsedMs: Math.round(performance.now() - started),
      passed: measurement.core.passed,
      total: measurement.core.total,
      disabledQueryDispatches: 0,
    });
    console.log(
      `Cold start ${iteration}/5, native core ${measurement.core.passed}/${measurement.core.total}, background/resume and scroll passed`,
    );
  }
  const crashes = run('logcat', '-b', 'crash', '-d', '-T', crashStart);
  await writeFile(path.join(output, 'crash-buffer.txt'), crashes);
  await writeFile(
    path.join(output, 'summary.json'),
    JSON.stringify({ serial, metroPort: port, crashStart, results }, null, 2),
  );
  if (crashes.includes(`Cmdline: ${app}`) || crashes.includes(`>>> ${app} <<<`))
    throw new Error('A native app crash occurred during this run; inspect crash-buffer.txt');
} finally {
  run('shell', 'rm', '-f', deviceFile);
}
