import type { NextConfig } from 'next';

// End-to-end builds add the technical fixtures (`page.e2e.tsx`) in their own output directory, so
// an ordinary production build never contains them.
const e2eFixtures = process.env.LOTO_WEB_E2E_FIXTURES === '1';

const config: NextConfig = {
  transpilePackages: ['@loto/api', '@loto/core'],
  turbopack: {
    resolveAlias: {
      // Shared's dev peer is tooling-only; every client hook must see the app's context.
      '@tanstack/react-query': './node_modules/@tanstack/react-query',
    },
  },
  reactStrictMode: true,
  typedRoutes: true,
  pageExtensions: e2eFixtures ? ['e2e.tsx', 'tsx', 'ts'] : ['tsx', 'ts'],
  distDir: e2eFixtures ? '.next-e2e' : '.next',
  // Preserve the repository-owned AGENTS.md rather than appending generated instructions.
  agentRules: false,
};

export default config;
