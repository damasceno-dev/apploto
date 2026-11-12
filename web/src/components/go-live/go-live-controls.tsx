'use client';
import { accountTypeLabels } from '@loto/core';
import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import {
  postGoLiveImports,
  postGoLiveImportsIdMapping,
  postGoLiveImportsIdPreview,
  postGoLiveImportsIdActivate,
  postGoLiveOpenings,
  postGoLiveOpeningsIdAmend,
  postGoLiveOpeningsIdCancel,
  toIfMatch,
  type RequestGoLiveImportJson,
  type RequestGoLiveMappingJson,
  type RequestGoLiveOpeningJson,
  type RequestActivateGoLiveImportJson,
  type ResponseGoLiveImportJson,
  type ResponseGoLiveOpeningJson,
} from '@loto/api';
import {
  useGetAccount,
  useGetClient,
  useGetOperator,
  useGetTransactionType,
  useGetProduct,
  useGetTerminal,
  useGetPeoplePayCategory,
} from '@loto/api/queries';
import { useScopedApiClient } from '@/client/query/scoped-query-provider';
import { useIdempotentOperation } from '@/client/mutations/use-idempotent-operation';
import { Button } from '@/components/ui/button';
import { FormField } from '@/components/ui/form-field';
import { NativeSelect, type SelectOption } from '@/components/ui/select';
import { Notice } from '@/components/ui/notice';
export const importStatusLabels: Record<string, string> = {
  Uploaded: 'Arquivos recebidos',
  Mapped: 'Mapeamento salvo',
  NeedsResolution: 'Resolução pendente',
  ReviewedPreview: 'Prévia válida para confirmação',
  Activated: 'Ativada',
};
const choose: SelectOption = { value: '', label: 'Escolha…' };
export type UploadManifest = { name: string; sha256: string }[];
export interface UploadMemory {
  request: RequestGoLiveImportJson;
  manifest: UploadManifest;
}
type Command =
  | { kind: 'upload'; profile: string; encoding: string; manifest: UploadManifest }
  | { kind: 'mapping'; id: string; request: RequestGoLiveMappingJson }
  | { kind: 'preview'; id: string; request: RequestGoLiveMappingJson }
  | { kind: 'activate'; id: string; request: RequestActivateGoLiveImportJson }
  | { kind: 'opening'; request: RequestGoLiveOpeningJson }
  | {
      kind: 'amend';
      id: string;
      version: number;
      reason: string;
      request: RequestGoLiveOpeningJson;
    }
  | { kind: 'cancel'; id: string; version: number; reason: string };
type Outcome = ResponseGoLiveImportJson | ResponseGoLiveOpeningJson;

export function useCommand(
  context: string,
  accepted: (value: Outcome) => void,
  source?: { current: UploadMemory | null },
) {
  const queries = useQueryClient();
  const [messages, setMessages] = useState<readonly string[]>([]);
  const [busy, setBusy] = useState(false);
  const [discardConfirmed, setDiscardConfirmed] = useState(false);
  const operation = useIdempotentOperation<Command, Outcome>({
    context,
    operation: 'go-live',
    send: async (payload, key, options) => {
      const headers = { 'Idempotency-Key': key };
      switch (payload.kind) {
        case 'upload': {
          const memory = source?.current;
          if (
            !memory ||
            memory.request.profile !== payload.profile ||
            memory.request.encoding !== payload.encoding ||
            JSON.stringify(memory.manifest) !== JSON.stringify(payload.manifest)
          )
            throw new Error('Selecione os mesmos CSV para verificar este envio.');
          return (await postGoLiveImports(memory.request, headers, options)).data;
        }
        case 'mapping':
          return (await postGoLiveImportsIdMapping(payload.id, payload.request, headers, options))
            .data;
        case 'preview':
          return (await postGoLiveImportsIdPreview(payload.id, payload.request, headers, options))
            .data;
        case 'activate':
          return (await postGoLiveImportsIdActivate(payload.id, payload.request, headers, options))
            .data;
        case 'amend':
          return (
            await postGoLiveOpeningsIdAmend(
              payload.id,
              { reason: payload.reason, opening: payload.request },
              { ...headers, 'If-Match': toIfMatch(payload.version) },
              options,
            )
          ).data;
        case 'cancel':
          return (
            await postGoLiveOpeningsIdCancel(
              payload.id,
              { reason: payload.reason },
              { ...headers, 'If-Match': toIfMatch(payload.version) },
              options,
            )
          ).data;
        case 'opening':
          return (await postGoLiveOpenings(payload.request, headers, options)).data;
      }
    },
  });
  const run = async (payload?: Command) => {
    setBusy(true);
    try {
      const result = payload ? await operation.submit(payload) : await operation.verifyAndRetry();
      if (result.kind === 'confirmed') {
        accepted(result.result);
        void queries.invalidateQueries();
        operation.discard();
        setMessages(['Operação confirmada.']);
      } else if (result.kind === 'failed')
        setMessages(
          'messages' in result.failure && result.failure.messages.length
            ? result.failure.messages
            : ['Resultado não confirmado. Verifique antes de tentar novamente.'],
        );
      else setMessages(['Operação pendente. Confira o resultado antes de continuar.']);
    } catch {
      setMessages([
        'Não foi possível verificar o resultado. Preserve este registro e consulte a importação antes de reenviar.',
      ]);
    } finally {
      setBusy(false);
    }
  };
  return {
    run,
    busy: busy || !operation.ready || operation.record !== null,
    feedback: (
      <>
        {messages.map((message, index) => (
          <Notice key={index} kind="info" title={message} />
        ))}
        {!operation.durable && (
          <Notice
            kind="warning"
            title="O registro de recuperação não está salvo neste dispositivo."
          />
        )}
        {operation.record && (
          <>
            <Notice kind="warning" title="Existe uma operação com resultado a conferir.">
              <p>
                Os dados enviados e a chave estão preservados. Consulte o estado no servidor; não
                registre a mesma obrigação novamente quando o resultado ainda for desconhecido.
              </p>
              <details>
                <summary>Dados exatos da tentativa</summary>
                <pre className="whitespace-pre-wrap break-all">{operation.record.payloadJson}</pre>
              </details>
              <label>
                <input
                  type="checkbox"
                  checked={discardConfirmed}
                  onChange={(e) => setDiscardConfirmed(e.target.checked)}
                />{' '}
                Consultei o resultado no servidor e posso descartar esta tentativa neste
                dispositivo.
              </label>
              <Button
                disabled={!discardConfirmed || busy}
                onClick={() => {
                  operation.discard();
                  setDiscardConfirmed(false);
                }}
              >
                Descartar registro desta tentativa
              </Button>
            </Notice>
            <Button onClick={() => void run()} disabled={busy}>
              Verificar e tentar novamente
            </Button>
          </>
        )}
        {operation.storageProblem && (
          <Notice kind="warning" title="Não foi possível ler o registro deste dispositivo.">
            <Button onClick={operation.retryStorage}>Verificar armazenamento</Button>
            <Button onClick={operation.continueWithoutDeviceRecord}>
              Continuar sem registro neste dispositivo
            </Button>
          </Notice>
        )}
      </>
    ),
  };
}

export function Picker({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value?: string | null;
  options: readonly SelectOption[];
  onChange: (value: string) => void;
}) {
  return (
    <FormField label={label}>
      <NativeSelect
        value={value ?? ''}
        options={[choose, ...options]}
        onChange={(event) => onChange(event.target.value)}
      />
    </FormField>
  );
}

export function useEntities() {
  const client = useScopedApiClient();
  const options = { request: { client } };
  const accounts = useGetAccount(options),
    clients = useGetClient(options),
    operators = useGetOperator(options),
    types = useGetTransactionType(options),
    products = useGetProduct(options),
    terminals = useGetTerminal(options),
    payCategories = useGetPeoplePayCategory(options);
  return {
    accounts,
    clients,
    operators,
    types,
    refresh: () =>
      Promise.all([
        accounts.refetch(),
        clients.refetch(),
        operators.refetch(),
        types.refetch(),
        products.refetch(),
        terminals.refetch(),
        payCategories.refetch(),
      ]),
    productOptions: (products.data?.data.items ?? []).map((x) => ({ value: x.id, label: x.name })),
    terminalOptions: (terminals.data?.data ?? []).map((x) => ({ value: x.id, label: x.name })),
    payCategoryOptions: (payCategories.data?.data ?? [])
      .filter(
        (x) => x.enabled && !(payCategories.data?.data ?? []).some((y) => y.previousId === x.id),
      )
      .map((x) => ({ value: x.id, label: x.name })),
    failed:
      accounts.isError ||
      clients.isError ||
      operators.isError ||
      types.isError ||
      products.isError ||
      terminals.isError ||
      payCategories.isError,
    pending:
      accounts.isPending ||
      clients.isPending ||
      operators.isPending ||
      types.isPending ||
      products.isPending ||
      terminals.isPending ||
      payCategories.isPending,
    accountOptions: (accounts.data?.data.accounts ?? []).map((x) => ({
      value: x.id,
      label: `${accountTypeLabels[x.type]} · ${x.name}`,
    })),
    clientOptions: (clients.data?.data.clients ?? []).map((x) => ({ value: x.id, label: x.name })),
    operatorOptions: (operators.data?.data.operators ?? []).map((x) => ({
      value: x.id,
      label: x.name,
    })),
    typeOptions: (types.data?.data.items ?? [])
      .filter((x) => !x.systemRole)
      .map((x) => ({ value: x.id, label: `${x.categoryName} · ${x.name}` })),
  };
}
