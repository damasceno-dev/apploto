// Vitest (Shared M2 D1, item 3.1): the client core's behavior tests. Tests inject a fake `fetch`
// and never touch the network.
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    include: ['test/**/*.test.ts'],
    environment: 'node',
  },
});
