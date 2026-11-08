import { destinations, canEnter, type DestinationKey } from '@/shell/policy';
import { sessionGate } from '@/session/gate';
import { useSession } from '@/session/provider';
import { Notice, Skeleton } from '@/ui';
import { CreateScreen } from './create';
import { TransactionDetailScreen } from './detail';
import { TransactionsScreen } from './list';
import { NewPlanScreen, PlanScreen } from './plan';
import { PaymentScreen } from './payment';
import { SafeDepositScreen } from './safe';
import { StatementScreen } from './statement';

const surfaces = [
  'transactions',
  'create',
  'edit',
  'plan-new',
  'plan',
  'statement',
  'payment',
  'safe-deposit',
] as const;
export type LedgerSurfaceKey = (typeof surfaces)[number];
/** The Mobile M6 destinations (ledger, entry, correction, plans, fiado, Depósito Dinheiro). */
export function isLedgerSurface(key: DestinationKey): key is LedgerSurfaceKey {
  return (surfaces as readonly string[]).includes(key);
}
export function LedgerSurface({
  destination,
  preserveVerifiedView = false,
}: {
  destination: LedgerSurfaceKey;
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
  switch (destination) {
    case 'transactions':
      return <TransactionsScreen />;
    case 'create':
      return <CreateScreen />;
    case 'edit':
      return <TransactionDetailScreen />;
    case 'plan-new':
      return <NewPlanScreen />;
    case 'plan':
      return <PlanScreen />;
    case 'statement':
      return <StatementScreen />;
    case 'payment':
      return <PaymentScreen />;
    case 'safe-deposit':
      return <SafeDepositScreen />;
  }
}
