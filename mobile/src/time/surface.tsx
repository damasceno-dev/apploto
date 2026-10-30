import { destinations, canEnter, type DestinationKey } from '@/shell/policy';
import { sessionGate } from '@/session/gate';
import { useSession } from '@/session/provider';
import { Notice, Skeleton } from '@/ui';
import { ClockScreen } from './clock';
import { ManagementScreen } from './management';
import { ReportScreen } from './reports';
export function isTimeSurface(key: DestinationKey) {
  return [
    'clock',
    'time',
    'time-detail',
    'my-transactions',
    'operator-transactions',
    'my-hours',
    'hours',
  ].includes(key);
}
export function TimeSurface({
  destination,
  preserveVerifiedView = false,
}: {
  destination: DestinationKey;
  preserveVerifiedView?: boolean;
}) {
  const { snapshot } = useSession();
  if (
    (!preserveVerifiedView && sessionGate(snapshot, 'branch') !== 'allow') ||
    !snapshot.clock ||
    !snapshot.branch
  )
    return <Skeleton />;
  const entry = destinations.find((d) => d.key === destination);
  if (!entry || !canEnter(entry, snapshot.branch.role))
    return <Notice kind="danger" title="Seu perfil não tem acesso a este destino." />;
  if (destination === 'clock') return <ClockScreen />;
  if (destination === 'time' || destination === 'time-detail') return <ManagementScreen />;
  return (
    <ReportScreen
      kind={
        destination === 'my-transactions' || destination === 'operator-transactions'
          ? 'transactions'
          : 'balance'
      }
      own={destination === 'my-transactions' || destination === 'my-hours'}
    />
  );
}
