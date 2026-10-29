// Manual lifecycle for the SAME owned fixture harness used by Playwright. Credentials/logs stay
// inside its private temporary directory; this command prints only an origin and the state path.
import { realpath } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { startStack, stopStack, readState, stateEnv } from '../e2e/stack.ts';

const [action, file] = process.argv.slice(2);
if (action === 'start') {
  try {
    const stateFile = await startStack();
    const state = await readState();
    console.log(`Entry: ${state.webUrl}/entrar`);
    console.log(`Private fixture state: ${stateFile}`);
    console.log('Use a fake person from users in that private file. No credentials are printed.');
  } catch (error) {
    await stopStack();
    throw error;
  }
} else if (action === 'stop' && file) {
  const root = await realpath(tmpdir());
  const stateFile = await realpath(file);
  if (
    path.dirname(path.dirname(stateFile)) !== root ||
    !path.basename(path.dirname(stateFile)).startsWith('loto-web-m2-e2e-') ||
    path.basename(stateFile) !== 'state.json'
  ) {
    throw new Error('Expected the private state.json returned by this owned fixture harness.');
  }
  process.env[stateEnv] = stateFile;
  await stopStack();
  console.log('Stopped the resources recorded by this fixture harness.');
} else {
  throw new Error('Use e2e:stack start, or e2e:stack stop <private-state.json>.');
}
