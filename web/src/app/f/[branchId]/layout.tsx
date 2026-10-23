import { BranchProviders } from '@/client/app-providers';
import { requireBranchSession } from '@/server/gate/guards';

/**
 * The branch namespace (D-M2-1): everything under `/f/{branchId}` works in that branch, in this tab
 * only. The guard runs before any branch fetch in the segment; the providers give this tab its own
 * branch session and query scope. The id is navigation context — authority is the signed-in
 * identity's own session for it, verified by the proxy and by the API on every call.
 */
export default async function BranchLayout({ children, params }: LayoutProps<'/f/[branchId]'>) {
  const { branchId } = await params;
  const session = await requireBranchSession(branchId);
  return (
    <BranchProviders branchId={branchId} session={session}>
      {children}
    </BranchProviders>
  );
}
