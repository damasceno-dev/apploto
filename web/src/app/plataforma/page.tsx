import { getUserProfile } from '@loto/api';
import { requireIdentity } from '@/server/gate/guards';
import { serverRead } from '@/server/api';
import { FeatureAvailability } from '@/components/entry/feature-availability';
import { IdentitySheet } from '@/components/entry/identity-sheet';
import { ButtonLink } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
export default async function PlatformPage() {
  await requireIdentity();
  const profile = await serverRead({ tier: 'identity' }, (client) => getUserProfile({ client }));
  if (!profile.data.isPlatformOperator)
    return (
      <IdentitySheet title="Acesso indisponível">
        <Notice kind="neutral" title="Esta área exige acesso de operador da plataforma." />
        <ButtonLink href="/filiais">Voltar para escolher a filial</ButtonLink>
      </IdentitySheet>
    );
  return <FeatureAvailability title="Plataforma" />;
}
