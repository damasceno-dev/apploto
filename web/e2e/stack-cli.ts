// Keeps one end-to-end stack across Playwright runs while iterating:
//   node e2e/stack-cli.ts start   → prints the state file path (export it as LOTO_WEB_E2E_STATE)
//   LOTO_WEB_E2E_STATE=<path> node e2e/stack-cli.ts stop
import { startStack, stopStack } from './stack.ts';

const command = process.argv[2];
if (command === 'start') {
  try {
    console.log(await startStack());
    process.exit(0);
  } catch (error) {
    await stopStack();
    throw error;
  }
} else if (command === 'stop') {
  await stopStack();
} else {
  throw new Error('Usage: node e2e/stack-cli.ts start|stop');
}
