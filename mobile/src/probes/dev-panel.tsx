import { IntlProbe } from './intl-probe';
import { SessionHarness } from './session-harness';
import { QueryProof } from './query-proof';

export function DevPanel() {
  return (
    <>
      <QueryProof />
      <SessionHarness />
      <IntlProbe />
    </>
  );
}
