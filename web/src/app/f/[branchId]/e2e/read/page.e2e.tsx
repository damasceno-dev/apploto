import { gateDestinations } from '@/lib/route-policy';
import { serverBranchClock } from '@/server/api';
import { ClientClock } from './client-clock';

// End-to-end build only: the same branch clock read on the server (per-request client, this route's
// branch) and in the browser (generated hook through the forwarder, this tab's branch).
// Fixture pages are compiled only in the end-to-end build, so typed routes do not list them.
export default async function BranchReadFixturePage({
  params,
}: {
  readonly params: Promise<{ readonly branchId: string }>;
}) {
  const { branchId } = await params;
  const clock = await serverBranchClock(branchId);
  return (
    <main>
      <h1>Fixture: leitura da filial</h1>
      <p data-testid="server-clock">
        {clock ? `${clock.branchId}|${clock.branchLocalDate}` : 'inválido'}
      </p>
      <ClientClock />
      <a href={gateDestinations.branchPicker}>Trocar filial</a>
    </main>
  );
}
