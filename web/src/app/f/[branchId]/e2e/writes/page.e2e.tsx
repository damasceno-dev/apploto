import { WritesFixture } from './writes-fixture';

// End-to-end build only: guarded update, idempotent create and device input against the real API,
// in this tab's branch.
export default function WritesFixturePage() {
  return (
    <main>
      <h1>Fixture: escritas</h1>
      <WritesFixture />
    </main>
  );
}
