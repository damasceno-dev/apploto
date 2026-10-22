import { SessionFixture } from './session-fixture';

// End-to-end build only (`LOTO_WEB_E2E_FIXTURES=1`): technical controls, not a product screen.
export default function SessionFixturePage() {
  return (
    <main>
      <h1>Fixture: sessão</h1>
      <SessionFixture />
    </main>
  );
}
