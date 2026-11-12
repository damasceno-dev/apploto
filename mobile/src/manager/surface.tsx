import { destinations, canEnter, type DestinationKey } from '@/shell/policy';
import { sessionGate } from '@/session/gate';
import { useSession } from '@/session/provider';
import { Notice, Skeleton } from '@/ui';
import { ManagerGate, useManagerScopeTeardown } from './common';
import { QueueScreen } from './queue';
import { ReviewScreen } from './review';

const surfaces = ['queue', 'approval'] as const;
export type ManagerSurfaceKey = (typeof surfaces)[number];
/** The Mobile M7 destinations (Fila de trabalho and Conferência do fechamento). */
export function isManagerSurface(key: DestinationKey): key is ManagerSurfaceKey {
  return (surfaces as readonly string[]).includes(key);
}
export function ManagerSurface({
  destination,
  preserveVerifiedView = false,
}: {
  destination: ManagerSurfaceKey;
  preserveVerifiedView?: boolean;
}) {
  const { snapshot } = useSession();
  useManagerScopeTeardown();
  if (
    (!preserveVerifiedView && sessionGate(snapshot, 'branch') !== 'allow') ||
    !snapshot.clock ||
    !snapshot.branch
  )
    return <Skeleton />;
  // The shell guarded the route; the surface re-checks the role before any read or command.
  const entry = destinations.find((d) => d.key === destination);
  if (!entry || !canEnter(entry, snapshot.branch.role))
    return <Notice kind="danger" title="Seu perfil não tem acesso a este destino." />;
  return <ManagerGate>{destination === 'queue' ? <QueueScreen /> : <ReviewScreen />}</ManagerGate>;
}
