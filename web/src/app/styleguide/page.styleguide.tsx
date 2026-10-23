import type { Metadata } from 'next';
import { Gallery } from './gallery';

// Compiled only in `next dev` or with LOTO_WEB_STYLEGUIDE=1 (see next.config.ts): an ordinary
// production build has no /styleguide route, which `tooling/check-bundles.mjs` asserts.
export const metadata: Metadata = { title: 'Guia de estilo · Lotero', robots: { index: false } };

export default function StyleguidePage() {
  return <Gallery />;
}
