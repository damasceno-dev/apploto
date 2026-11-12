'use client';

import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import {
  isApiError,
  postAccountSafe,
  postAccountTerminal,
  postClient,
  postOperator,
  postHoliday,
  postPeoplePayCategory,
  type RequestPayCategoryJson,
  type ResponsePayCategoryJson,
} from '@loto/api';
import { parseCalendarDate, payItemKindLabels } from '@loto/core';
import { useGetAccount } from '@loto/api/queries';
import { GoLiveAccountAccess } from './go-live-account-access';
import { useGoLiveDraft } from './use-go-live-draft';
import { useIdempotentOperation } from '@/client/mutations/use-idempotent-operation';
import { useScopedApiClient } from '@/client/query/scoped-query-provider';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { DateField } from '@/components/ui/date-picker';
import { FormField } from '@/components/ui/form-field';
import { IdentifierInput, Input, Textarea } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/select';
import { Notice } from '@/components/ui/notice';

/** Only metadata needed to resolve this cutover; existing commands keep their own authority. */
export function GoLiveSetup({ today }: { today: string }) {
  const payOperation = useIdempotentOperation<RequestPayCategoryJson, ResponsePayCategoryJson>({
    context: 'go-live-pay-category',
    operation: 'pay-category',
    send: async (payload, key, options) =>
      (await postPeoplePayCategory(payload, { 'Idempotency-Key': key }, options)).data,
  });
  const client = useScopedApiClient();
  const queries = useQueryClient();
  const [kind, setKind] = useState('client');
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [postedOn, setPosted] = useState(today);
  const [invalidDate, setInvalidDate] = useState(false);
  const [payKind, setPayKind] = useState<'Addition' | 'Deduction'>('Addition');
  const [evidence, setEvidence] = useState('');
  const [busy, setBusy] = useState(false);
  const [unknown, setUnknown] = useState(false);
  const [messages, setMessages] = useState<readonly string[]>([]);
  const authority = useGetAccount({ request: { client } });
  const retained = useGoLiveDraft(
    'go-live-setup-input',
    { kind, name, phone, postedOn, payKind, evidence },
    authority.isSuccess,
  );
  const [discardConfirmed, setDiscardConfirmed] = useState(false);
  const save = async () => {
    setBusy(true);
    setMessages([]);
    try {
      switch (kind) {
        case 'client':
          await postClient({ name, phone }, { client });
          break;
        case 'operator':
          await postOperator({ name, userId: null }, { client });
          break;
        case 'cashbox':
          await postAccountTerminal({ name, createTabAccount: true }, { client });
          break;
        case 'holiday':
          await postHoliday({ holidays: [{ date: postedOn, description: name }] }, { client });
          break;
        case 'safe':
          await postAccountSafe({ name }, { client });
          break;
        case 'pay': {
          const result = await payOperation.submit({
            name,
            kind: payKind,
            enabled: true,
            postedOn,
            reason: evidence,
            evidence,
          });
          if (result.kind !== 'confirmed') {
            setMessages(
              result.kind === 'failed' && 'messages' in result.failure
                ? result.failure.messages
                : ['Resultado não confirmado. Verifique a tentativa preservada.'],
            );
            return;
          }
          payOperation.discard();
          break;
        }
      }
      retained.clear();
      setName('');
      setPhone('');
      setEvidence('');
      setMessages(['Cadastro confirmado. Selecione o destino no mapeamento.']);
      await queries.invalidateQueries();
    } catch (error) {
      if (isApiError(error) && error.kind === 'http') setMessages(error.errorMessages);
      else {
        setUnknown(true);
        setMessages([
          'Resultado não confirmado. Atualize e confira os cadastros antes de criar novamente.',
        ]);
      }
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card title="Cadastros para a implantação">
      <GoLiveAccountAccess />
      {retained.storageFailed && (
        <Notice kind="warning" title="O rascunho do cadastro não foi salvo no dispositivo." />
      )}
      {retained.state.status === 'loaded' && retained.state.result.status === 'found' && (
        <Button
          disabled={busy}
          onClick={() => {
            if (retained.state.status === 'loaded' && retained.state.result.status === 'found') {
              const r = retained.state.result.data;
              setKind(r.kind);
              setName(r.name);
              setPhone(r.phone);
              setPosted(r.postedOn);
              setPayKind(r.payKind);
              setEvidence(r.evidence);
              setInvalidDate(false);
            }
          }}
        >
          Restaurar rascunho de cadastro
        </Button>
      )}
      <details>
        <summary>Adicionar um destino que ainda não existe</summary>
        <p>
          Use apenas dados comprovados. Uma pessoa cadastrada aqui não recebe acesso ao aplicativo;
          não vinculamos usuários legados. Cada caixa novo recebe seu terminal e sua conta fiado
          pelos comandos existentes.
        </p>
        <fieldset
          disabled={
            busy ||
            unknown ||
            (kind === 'pay' && (!payOperation.ready || payOperation.record !== null))
          }
          onChangeCapture={retained.markDirty}
          onClickCapture={retained.markDirty}
          className="flex flex-col gap-stack-md"
        >
          <FormField label="Cadastro necessário">
            <NativeSelect
              value={kind}
              options={[
                { value: 'client', label: 'Cliente' },
                { value: 'operator', label: 'Operador sem login' },
                { value: 'cashbox', label: 'Caixa com terminal e conta fiado' },
                { value: 'safe', label: 'Cofre' },
                { value: 'pay', label: 'Categoria de remuneração' },
                { value: 'holiday', label: 'Feriado comprovado' },
              ]}
              onChange={(e) => setKind(e.target.value)}
            />
          </FormField>
          <FormField label="Nome do novo cadastro">
            <Input value={name} onChange={(e) => setName(e.target.value)} />
          </FormField>
          {kind === 'client' && (
            <FormField label="Telefone comprovado">
              <IdentifierInput kind="phone" value={phone} onValueChange={setPhone} />
            </FormField>
          )}
          {kind === 'holiday' && (
            <FormField label="Data do feriado comprovado">
              <DateField
                onInvalidChange={setInvalidDate}
                value={parseCalendarDate(postedOn)}
                today={parseCalendarDate(today)!}
                onValueChange={(value) => setPosted(value ?? '')}
              />
            </FormField>
          )}
          {kind === 'pay' && (
            <>
              <FormField label="Natureza da categoria">
                <NativeSelect
                  value={payKind}
                  options={Object.entries(payItemKindLabels).map(([value, label]) => ({
                    value,
                    label,
                  }))}
                  onChange={(e) => setPayKind(e.target.value as 'Addition' | 'Deduction')}
                />
              </FormField>
              <FormField label="Data do novo cadastro">
                <DateField
                  onInvalidChange={setInvalidDate}
                  value={parseCalendarDate(postedOn)}
                  today={parseCalendarDate(today)!}
                  onValueChange={(value) => setPosted(value ?? '')}
                />
              </FormField>
              <FormField label="Motivo e evidência do cadastro">
                <Textarea value={evidence} onChange={(e) => setEvidence(e.target.value)} />
              </FormField>
            </>
          )}
          <Button
            disabled={!name.trim() || ((kind === 'pay' || kind === 'holiday') && invalidDate)}
            onClick={() => void save()}
          >
            Criar destino para implantação
          </Button>
        </fieldset>
        {payOperation.storageProblem && (
          <Notice
            kind="warning"
            title="O registro da categoria não está disponível neste dispositivo."
          >
            <Button onClick={payOperation.retryStorage}>
              Verificar armazenamento da categoria
            </Button>
            <Button onClick={payOperation.continueWithoutDeviceRecord}>
              Continuar sem registro da categoria neste dispositivo
            </Button>
          </Notice>
        )}
        {payOperation.record && (
          <Notice kind="warning" title="Tentativa de cadastro preservada">
            <details>
              <summary>Conferir dados enviados</summary>
              <pre className="whitespace-pre-wrap break-all">{payOperation.record.payloadJson}</pre>
            </details>
            <label>
              <input
                type="checkbox"
                checked={discardConfirmed}
                onChange={(e) => setDiscardConfirmed(e.target.checked)}
              />{' '}
              Consultei as categorias existentes e posso descartar esta tentativa.
            </label>
            <Button
              disabled={!discardConfirmed || busy}
              onClick={() => {
                payOperation.discard();
                setDiscardConfirmed(false);
              }}
            >
              Descartar tentativa de categoria
            </Button>
          </Notice>
        )}
        {payOperation.record && (
          <Button
            disabled={busy}
            onClick={async () => {
              const result = await payOperation.verifyAndRetry();
              if (result.kind === 'confirmed') {
                payOperation.discard();
                setMessages(['Categoria confirmada.']);
                await queries.invalidateQueries();
              } else
                setMessages(
                  result.kind === 'failed' && 'messages' in result.failure
                    ? result.failure.messages
                    : ['Resultado ainda não confirmado.'],
                );
            }}
          >
            Verificar criação da categoria
          </Button>
        )}
        {unknown && (
          <Button
            onClick={async () => {
              await queries.invalidateQueries();
              setUnknown(false);
            }}
          >
            Atualizei e conferi os cadastros
          </Button>
        )}
        {messages.map((message, index) => (
          <Notice key={index} kind="info" title={message} />
        ))}
      </details>
    </Card>
  );
}
