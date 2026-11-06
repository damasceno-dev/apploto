import { destinations, canEnter, type DestinationKey } from '@/shell/policy';
import { sessionGate } from '@/session/gate';
import { useSession } from '@/session/provider';
import { Notice, Skeleton } from '@/ui';
import { CloseScreen } from './close';
import { CockpitScreen } from './cockpit';
import { OpenDayScreen } from './open';

const surfaces = ['today', 'open', 'close', 'correction'] as const;
export type DaySurfaceKey = (typeof surfaces)[number];
/** The four M5 operator-day destinations (Hoje, Abrir dia, Fechar dia, Corrigir e reenviar). */
export function isDaySurface(key: DestinationKey): key is DaySurfaceKey {
  return (surfaces as readonly string[]).includes(key);
}
export function DaySurface({
  destination,
  preserveVerifiedView = false,
}: {
  destination: DaySurfaceKey;
  preserveVerifiedView?: boolean;
}) {
  const { snapshot } = useSession();
  if (
    (!preserveVerifiedView && sessionGate(snapshot, 'branch') !== 'allow') ||
    !snapshot.clock ||
    !snapshot.branch
  )
    return <Skeleton />;
  // The shell guarded the route; the surface re-checks role before any read or command.
  const entry = destinations.find((d) => d.key === destination);
  if (!entry || !canEnter(entry, snapshot.branch.role))
    return <Notice kind="danger" title="Seu perfil não tem acesso a este destino." />;
  if (destination === 'today') return <CockpitScreen />;
  if (destination === 'open') return <OpenDayScreen />;
  return <CloseScreen correction={destination === 'correction'} />;
}
