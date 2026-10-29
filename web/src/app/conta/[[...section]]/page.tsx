import { notFound } from 'next/navigation';
import { requireIdentity } from '@/server/gate/guards';
import { FeatureAvailability } from '@/components/entry/feature-availability';
export default async function AccountPage({
  params,
}: {
  readonly params: Promise<{ section?: string[] }>;
}) {
  await requireIdentity();
  const { section = [] } = await params;
  if (section.length > 1 || (section.length === 1 && section[0] !== 'senha')) notFound();
  return (
    <FeatureAvailability
      contextual
      title={section[0] === 'senha' ? 'Alterar senha' : 'Minha conta'}
    />
  );
}
