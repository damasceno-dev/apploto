import type { NextConfig } from 'next';
import { PHASE_DEVELOPMENT_SERVER } from 'next/constants';

// End-to-end builds add the technical fixtures (`page.e2e.tsx`) in their own output directory, so
// an ordinary production build never contains them. The component gallery (`page.styleguide.tsx`,
// Web M3) exists in `next dev` and in an explicitly requested gallery build (its own directory,
// used by the gallery's browser tests); an ordinary production build has no gallery route at all.
const e2eFixtures = process.env.LOTO_WEB_E2E_FIXTURES === '1';
const styleguideBuild = process.env.LOTO_WEB_STYLEGUIDE === '1';

export default function config(phase: string): NextConfig {
  const styleguide = styleguideBuild || phase === PHASE_DEVELOPMENT_SERVER;
  return {
    transpilePackages: ['@loto/api', '@loto/core'],
    turbopack: {
      resolveAlias: {
        // Shared's dev peer is tooling-only; every client hook must see the app's context.
        '@tanstack/react-query': './node_modules/@tanstack/react-query',
      },
    },
    reactStrictMode: true,
    typedRoutes: true,
    pageExtensions: [
      ...(e2eFixtures ? ['e2e.tsx'] : []),
      ...(styleguide ? ['styleguide.tsx'] : []),
      'tsx',
      'ts',
    ],
    distDir: e2eFixtures ? '.next-e2e' : styleguideBuild ? '.next-styleguide' : '.next',
    // Preserve the repository-owned AGENTS.md rather than appending generated instructions.
    agentRules: false,
  };
}
