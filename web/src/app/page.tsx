import { Role, createApiClient, type ApiClient } from '@loto/api';
import * as core from '@loto/core';
import { QueryProof } from './query-proof';

export default function HomePage() {
  const factory: (options: Parameters<typeof createApiClient>[0]) => ApiClient = createApiClient;
  return (
    <main className="mx-auto flex max-w-form-max flex-col gap-stack-md px-page-inline-compact py-page-block-start medium:px-page-inline-wide">
      <h1 className="type-page-title">Lotero</h1>
      <p>Estrutura inicial do aplicativo.</p>
      <p>
        Pacotes locais: API {typeof factory} · {Object.keys(Role).length} tipos de perfil · core{' '}
        {Object.keys(core).length} exportações.
      </p>
      <QueryProof />
    </main>
  );
}
