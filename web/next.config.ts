import type { NextConfig } from 'next';

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
  // Preserve the repository-owned AGENTS.md rather than appending generated instructions.
  agentRules: false,
};

export default config;
