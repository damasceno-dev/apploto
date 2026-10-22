import { WritesFixture } from './writes-fixture';

// End-to-end build only: guarded update, idempotent create and device input against the real API.
export default function WritesFixturePage() {
  return (
    <main>
      <h1>Fixture: escritas</h1>
      <WritesFixture />
    </main>
  );
}
