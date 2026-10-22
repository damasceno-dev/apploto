import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      'server-only': fileURLToPath(new URL('./test/support/empty.ts', import.meta.url)),
      // As in next.config.ts: Shared's generated hooks must see the app's TanStack and React copies.
      '@tanstack/react-query': fileURLToPath(
        new URL('./node_modules/@tanstack/react-query', import.meta.url),
      ),
    },
    dedupe: ['react', 'react-dom'],
  },
  oxc: { jsx: { runtime: 'automatic' } },
  test: {
    include: ['test/unit/**/*.test.{ts,tsx}'],
    environment: 'node',
    restoreMocks: true,
  },
});
