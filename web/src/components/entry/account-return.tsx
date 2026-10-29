'use client';
import { useEffect, useState } from 'react';
import { useGetBranchMyBranches } from '@loto/api/queries';
import { useScopedApiClient } from '@/client/query/scoped-query-provider';
import { useSession } from '@/client/session/session-provider';
import { parseBranchPath } from '@/lib/branch-route';
import { canEnter, matchDestination } from '@/lib/destinations';
import { validateIntentPath } from '@/lib/route-policy';
import { ButtonLink } from '@/components/ui/button';
export function AccountReturn() {
  const client = useScopedApiClient();
  const session = useSession();
  const branches = useGetBranchMyBranches({
    request: { client },
    query: { refetchOnMount: 'always' },
  });
  const [back, setBack] = useState('/filiais');
  useEffect(() => {
    if (!branches.data || branches.isError) return;
    try {
      const value = JSON.parse(sessionStorage.getItem('loto-account-origin') ?? 'null') as {
        identity?: unknown;
        href?: unknown;
      } | null;
      if (
        value?.identity !== session.snapshot.user?.userId ||
        typeof value?.href !== 'string' ||
        validateIntentPath(value.href) !== value.href
      )
        return;
      const route = parseBranchPath(value.href.split('?')[0] ?? '');
      const branch = branches.data.data.branches.find((branch) => branch.id === route?.branchId);
      const destination = route ? matchDestination(route.rest) : null;
      if (
        branch &&
        destination &&
        !destination.path.includes(':id') &&
        canEnter(destination, branch.role)
      ) {
        const href = value.href;
        const frame = requestAnimationFrame(() => setBack(href));
        return () => cancelAnimationFrame(frame);
      }
    } catch {
      /* Untrusted origin never supplies permission or a URL fallback. */
    }
  }, [branches.data, branches.isError, session.snapshot.user?.userId]);
  return <ButtonLink href={back as never}>Voltar</ButtonLink>;
}
