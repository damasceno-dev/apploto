import { defineConfig } from '@playwright/test';

// Web M3 gallery suite: builds the explicitly requested gallery output (`LOTO_WEB_STYLEGUIDE=1`,
// its own `.next-styleguide` directory) and serves it on localhost. No API: the gallery makes no
// request, so the configured base URL is inert. Drives the installed Google Chrome.
const port = Number(process.env.LOTO_WEB_STYLEGUIDE_PORT ?? 3477);
const env = {
  ...process.env,
  LOTO_WEB_STYLEGUIDE: '1',
  LOTO_API_BASE_URL: 'http://127.0.0.1:59999',
  NEXT_TELEMETRY_DISABLED: '1',
};

export default defineConfig({
  testDir: 'e2e-styleguide',
  testMatch: '*.spec.ts',
  workers: 1,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [['list']],
  use: { channel: 'chrome', headless: true, baseURL: `http://localhost:${port}`, trace: 'off' },
  webServer: {
    command: `node --conditions=react-server tooling/run-next.mjs build && node --conditions=react-server tooling/run-next.mjs start --hostname localhost --port ${port}`,
    url: `http://localhost:${port}/styleguide`,
    env,
    timeout: 600_000,
    // Iterating locally: point at an already running gallery server.
    reuseExistingServer: process.env.LOTO_WEB_STYLEGUIDE_REUSE === '1',
  },
});
