'use client';

import { accountTypeLabels } from '@loto/core';
import { useState } from 'react';
import { isApiError, postOperatorOperatorIdAccounts } from '@loto/api';
import { useGetOperatorOperatorIdAccounts } from '@loto/api/queries';
import { useScopedApiClient } from '@/client/query/scoped-query-provider';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { Picker, useEntities } from './go-live-controls';

/** An explicit grant through the existing account-assignment command, never inferred from pairing. */
export function GoLiveAccountAccess() {
  const client = useScopedApiClient();
  const entities = useEntities();
  const [operatorId, setOperator] = useState('');
  const [accountId, setAccount] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [messages, setMessages] = useState<readonly string[]>([]);
  const current = useGetOperatorOperatorIdAccounts(operatorId, {
    request: { client },
    query: { enabled: !!operatorId },
  });
  const assign = async () => {
    setBusy(true);
    try {
      await postOperatorOperatorIdAccounts(operatorId, { accountId }, { client });
      setConfirmed(false);
      setMessages(['Liberação de conta confirmada.']);
      await current.refetch();
    } catch (error) {
      setMessages(
        isApiError(error) && error.kind === 'http'
          ? error.errorMessages
          : ['Resultado não confirmado. Consulte as contas liberadas antes de repetir.'],
      );
      setConfirmed(false);
    } finally {
      setBusy(false);
    }
  };
  return (
    <details>
      <summary>Conferir e liberar contas para um operador</summary>
      <p>
        A conta fiado e o caixa exigem liberações separadas. Esta ação não cria login nem atribui
        custódia histórica. A primeira conta liberada segue a regra de conta principal do servidor.
      </p>
      <fieldset
        disabled={busy || entities.pending || entities.failed}
        className="flex flex-col gap-stack-md"
      >
        <Picker
          label="Operador da liberação"
          value={operatorId}
          options={entities.operatorOptions}
          onChange={(value) => {
            setOperator(value);
            setConfirmed(false);
          }}
        />
        {operatorId && current.isPending && <p role="status">Carregando contas liberadas…</p>}
        {current.isSuccess && current.data.data.operatorAccounts.length === 0 && (
          <p>Nenhuma conta liberada para este operador.</p>
        )}
        {current.isError && (
          <Notice kind="warning" title="Não foi possível consultar as contas liberadas." />
        )}
        {current.data && (
          <ul>
            {current.data.data.operatorAccounts.map((x) => (
              <li key={x.id}>
                {x.accountName} · {accountTypeLabels[x.accountType]}
                {x.isPrimary ? ' · Principal' : ''}
              </li>
            ))}
          </ul>
        )}
        <Button disabled={!operatorId} onClick={() => void current.refetch()}>
          Consultar liberações atuais
        </Button>
        <Picker
          label="Conta a liberar"
          value={accountId}
          options={entities.accountOptions}
          onChange={(value) => {
            setAccount(value);
            setConfirmed(false);
          }}
        />
        <label>
          <input
            type="checkbox"
            checked={confirmed}
            onChange={(e) => setConfirmed(e.target.checked)}
          />{' '}
          Conferi o operador, a conta e a liberação de acesso.
        </label>
        <Button
          disabled={!confirmed || !operatorId || !accountId || !current.isSuccess}
          onClick={() => void assign()}
        >
          Confirmar liberação desta conta
        </Button>
      </fieldset>
      {messages.map((message, index) => (
        <Notice key={index} kind="info" title={message} />
      ))}
    </details>
  );
}
