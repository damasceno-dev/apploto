import { serverBranchClock } from '@/server/api';
import { ClientClock } from './client-clock';

// End-to-end build only: the same branch clock read on the server (per-request client) and in the
// browser (generated hook through the forwarder).
export default async function BranchReadFixturePage() {
  const clock = await serverBranchClock();
  return (
    <main>
      <h1>Fixture: leitura da filial</h1>
      <p data-testid="server-clock">
        {clock ? `${clock.branchId}|${clock.branchLocalDate}` : 'inválido'}
      </p>
      <ClientClock />
    </main>
  );
}
