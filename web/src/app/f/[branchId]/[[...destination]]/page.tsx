import { notFound } from 'next/navigation';
import { getBranchCurrent, getOperatorSelfContext, getBranchMyBranches } from '@loto/api';
import { serverRead } from '@/server/api';
import { requireBranchSession, gateRedirect } from '@/server/gate/guards';
import {
  matchDestination,
  canEnter,
  defaultMode,
  homeKey,
  destinationHref,
} from '@/lib/destinations';
import { ApplicationFrame } from '@/components/shell/application-frame';
import { Notice } from '@/components/ui/notice';
import { IdentitySheet } from '@/components/entry/identity-sheet';
import { optionalShellRead } from '@/server/shell-context';

export default async function DestinationPage({
  params,
}: {
  readonly params: Promise<{ branchId: string; destination?: string[] }>;
}) {
  const { branchId, destination = [] } = await params;
  await requireBranchSession(branchId);
  const path = '/' + destination.join('/');
  const entry = destination.length ? matchDestination(path) : null;
  if (destination.length && !entry) notFound();
  const current = await serverRead({ tier: 'branch', branchId }, (client) =>
    getBranchCurrent({ client }),
  );
  const role = current.data.branch.role;
  if (destination.length === 0)
    gateRedirect(
      destinationHref(homeKey(defaultMode(role)), branchId, current.data.branchLocalDate),
    );
  if (!entry || !canEnter(entry, role))
    return (
      <IdentitySheet title="Acesso indisponível">
        <Notice kind="neutral" title="Seu perfil não permite acessar esta página." />
        <a
          href={destinationHref(homeKey(defaultMode(role)), branchId, current.data.branchLocalDate)}
        >
          Voltar ao início
        </a>
      </IdentitySheet>
    );
  const [self, branches] = await Promise.all([
    optionalShellRead(() =>
      serverRead({ tier: 'branch', branchId }, (client) => getOperatorSelfContext({ client })),
    ),
    optionalShellRead(() =>
      serverRead({ tier: 'identity' }, (client) => getBranchMyBranches({ client })),
    ),
  ]);
  return (
    <ApplicationFrame
      initialSelf={self.data?.data ?? null}
      initialSelfFailure={self.failure}
      initialBranchFailure={branches.failure}
      initialRole={role}
      initialDate={current.data.branchLocalDate}
      multiple={branches.data === null ? null : branches.data.data.branches.length > 1}
    />
  );
}
