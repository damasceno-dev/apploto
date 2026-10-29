import { serverSnapshot, gateRedirect } from '@/server/gate/guards';
export default async function HomePage() {
  const snapshot = await serverSnapshot();
  gateRedirect(
    snapshot.state === 'anonymous' || snapshot.state === 'renewal-unconfirmed'
      ? '/entrar'
      : '/filiais',
  );
}
