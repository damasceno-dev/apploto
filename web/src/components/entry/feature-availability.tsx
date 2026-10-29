import { AccountReturn } from './account-return';
import { IdentitySheet } from './identity-sheet';
import { Notice } from '@/components/ui/notice';
import { ButtonLink } from '@/components/ui/button';
export function FeatureAvailability({
  title,
  back = '/filiais',
  contextual = false,
}: {
  readonly title: string;
  readonly back?: string;
  readonly contextual?: boolean;
}) {
  return (
    <IdentitySheet title={title}>
      <Notice kind="info" title="Disponível em uma próxima etapa">
        Esta função de conta e acesso ainda não está disponível neste aplicativo.
      </Notice>
      {contextual ? <AccountReturn /> : <ButtonLink href={back as never}>Voltar</ButtonLink>}
    </IdentitySheet>
  );
}
