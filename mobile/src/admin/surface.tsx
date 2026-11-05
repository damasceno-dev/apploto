import { destinations, canEnter, type DestinationKey } from '@/shell/policy';
import { sessionGate } from '@/session/gate';
import { useSession } from '@/session/provider';
import { Notice, Skeleton } from '@/ui';
import { AccountsScreen } from './accounts';
import { AssignmentsScreen } from './assignments';
import { CategoriesScreen } from './categories';
import { ClientsScreen } from './clients';
import { HolidaysScreen } from './holidays';
import { MembersScreen } from './members';
import { OperatorsScreen } from './operators';
import { ProductsScreen } from './products';
import { SettingsScreen } from './settings';

const surfaces = [
  'operators',
  'assignments',
  'accounts',
  'clients',
  'categories',
  'products',
  'holidays',
  'settings',
  'members',
] as const;
export type AdminSurfaceKey = (typeof surfaces)[number];
/** The nine M10 surfaces. The guided M11 flows (Add/Offboard employee) keep their availability. */
export function isAdminSurface(key: DestinationKey): key is AdminSurfaceKey {
  return (surfaces as readonly string[]).includes(key);
}
export function AdminSurface({
  destination,
  preserveVerifiedView = false,
}: {
  destination: AdminSurfaceKey;
  preserveVerifiedView?: boolean;
}) {
  const { snapshot } = useSession();
  if (
    (!preserveVerifiedView && sessionGate(snapshot, 'branch') !== 'allow') ||
    !snapshot.clock ||
    !snapshot.branch
  )
    return <Skeleton />;
  // The shell already guarded this route; the surface re-checks before any read or mutation.
  const entry = destinations.find((d) => d.key === destination);
  if (!entry || !canEnter(entry, snapshot.branch.role))
    return <Notice kind="danger" title="Seu perfil não tem acesso a este destino." />;
  if (destination === 'operators') return <OperatorsScreen />;
  if (destination === 'assignments') return <AssignmentsScreen />;
  if (destination === 'accounts') return <AccountsScreen />;
  if (destination === 'categories') return <CategoriesScreen />;
  if (destination === 'members') return <MembersScreen />;
  if (destination === 'clients') return <ClientsScreen />;
  if (destination === 'products') return <ProductsScreen />;
  if (destination === 'holidays') return <HolidaysScreen />;
  return <SettingsScreen />;
}
