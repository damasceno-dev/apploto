import { defineConfig } from '@playwright/test';

// Web M2 end-to-end suite against the real local stack started by `e2e/global-setup.ts`.
// It drives the installed Google Chrome (`channel: 'chrome'`): no browser download.
export default defineConfig({
  testDir: 'e2e',
  testMatch: '*.spec.ts',
  globalSetup: './e2e/global-setup.ts',
  globalTeardown: './e2e/global-teardown.ts',
  // One worker: the API keeps one active refresh token per user, so parallel tests would end each
  // other's sessions by design.
  workers: 1,
  fullyParallel: false,
  timeout: 180_000,
  expect: { timeout: 15_000 },
  reporter: [['list']],
  use: {
    channel: 'chrome',
    headless: true,
    trace: 'off',
  },
});
