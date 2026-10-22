import { stopStack } from './stack';

export default async function globalTeardown() {
  // A reused stack belongs to `e2e:stack`; it is stopped there.
  if (process.env.LOTO_WEB_E2E_REUSED === '1') return;
  await stopStack();
}
