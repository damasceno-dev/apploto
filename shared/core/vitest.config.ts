// Vitest (Shared M3 5.1): pure helper suites. No DOM environment, no network, no real clock.
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    include: ['test/**/*.test.ts'],
    environment: 'node',
  },
});
