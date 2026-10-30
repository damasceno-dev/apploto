// Entry point for Mobile's owned disposable API fixtures. Raw state stays outside Git.
import { realpath } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { startStack, stopStack, stateEnv } from './native-stack.mjs';
const [mode, stateFile] = process.argv.slice(2);
if (mode === 'start') {
  try {
    console.log('Private fixture state: ' + (await startStack()));
  } catch {
    await stopStack();
    throw new Error('Owned fixture setup failed; inspect its private logs.');
  }
} else if (mode === 'stop' && stateFile) {
  const file = await realpath(stateFile);
  const temporary = await realpath(tmpdir());
  if (
    path.dirname(path.dirname(file)) !== temporary ||
    !path.basename(path.dirname(file)).startsWith('loto-mobile-shell-') ||
    path.basename(file) !== 'state.json'
  )
    throw new Error('Expected owned fixture state under the temporary directory.');
  process.env[stateEnv] = file;
  await stopStack();
  console.log('Stopped only the resources recorded by this fixture run.');
} else throw new Error('Use start or stop <private-state.json>.');
