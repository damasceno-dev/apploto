import { stateEnv, startStack, stopStack } from './stack';

export default async function globalSetup() {
  // `pnpm --filter @loto/web e2e:stack start` keeps a stack between runs; reuse it when given.
  if (process.env[stateEnv]) {
    process.env.LOTO_WEB_E2E_REUSED = '1';
    return;
  }
  try {
    await startStack();
  } catch (error) {
    // Remove whatever this run already started before reporting the failure.
    await stopStack();
    throw error;
  }
}
