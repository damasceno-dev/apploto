import type { ReactNode } from 'react';
import { requireBranchSession } from '@/server/gate/guards';

// End-to-end build only: a branch-scoped segment guarded before any of its fetches run.
export default async function BranchFixtureLayout({ children }: { children: ReactNode }) {
  await requireBranchSession();
  return children;
}
