'use client';
import { useState } from 'react';
import { isApiError, postSessionOpeningCount, type ResponseGoLiveOpeningJson } from '@loto/api';
import { useGetGoLiveOpenings, useGetSessionOpeningCount } from '@loto/api/queries';
import {
  centsFromMoneyDigits,
  centsFromWire,
  moneyDigitsFromCents,
  wireFromCents,
  formatSignedBrlWire,
  formatDate,
  parseCalendarDate as asCalendarDate,
} from '@loto/core';
import { useGoLiveDraft } from './use-go-live-draft';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { FormField } from '@/components/ui/form-field';
import { Input, Textarea } from '@/components/ui/input';
import { DateField } from '@/components/ui/date-picker';
import { MoneyInput, blankMoney, type MoneyValue } from '@/components/ui/money-input';
import { Notice } from '@/components/ui/notice';
import { useScopedApiClient } from '@/client/query/scoped-query-provider';
import { Picker, useCommand, useEntities } from './go-live-controls';
export function ManualOpening({ today }: { today: string }) {
  const entities = useEntities();
  const [accountId, setAccount] = useState(''),
    [clientId, setClient] = useState(''),
    [operatorId, setOperator] = useState(''),
    [typeId, setType] = useState('');
  const [kind, setKind] = useState('fiado'),
    [asOf, setAsOf] = useState(today),
    [due, setDue] = useState(today),
    [reference, setReference] = useState(''),
    [evidence, setEvidence] = useState('');
  const [invalidDates, setInvalidDates] = useState<Record<string, boolean>>({});
  const datesInvalid = Object.values(invalidDates).some(Boolean);
  const [amount, setAmount] = useState<MoneyValue>(blankMoney);
  const api = useScopedApiClient();
  const listed = useGetGoLiveOpenings({ request: { client: api } });
  const [extraItems, setExtraItems] = useState<{ value: MoneyValue; dueDate: string }[]>([]);
  const [editing, setEditing] = useState<ResponseGoLiveOpeningJson | null>(null);
  const [reason, setReason] = useState('');
  const [confirmation, setConfirmation] = useState(false);
  const retained = useGoLiveDraft(
    'go-live-opening-input',
    {
      accountId,
      clientId,
      operatorId,
      typeId,
      kind,
      asOf,
      due,
      reference,
      evidence,
      amount,
      extraItems,
    },
    listed.isSuccess,
  );
  const command = useCommand('go-live-opening', () => {
    retained.clear();
    setAmount(blankMoney);
    setExtraItems([]);
    setEditing(null);
    setInvalidDates({});
    setConfirmation(false);
    void listed.refetch();
  });
  const prepare = (opening: ResponseGoLiveOpeningJson) => {
    setEditing(opening);
    setAccount(opening.accountId);
    setClient(opening.clientId ?? '');
    setKind(opening.isCheque ? 'cheque' : 'fiado');
    setAsOf(opening.asOfDate.slice(0, 10));
    setReference(opening.sourceReference);
    setEvidence(opening.sourceEvidence);
    setOperator(opening.recordedByOperatorId);
    const item = opening.items[0];
    setDue(item?.dueDate.slice(0, 10) ?? today);
    const money = (value: number): MoneyValue => ({
      kind: 'amount',
      digits: moneyDigitsFromCents(centsFromWire(value)),
    });
    setAmount(item ? money(item.value) : blankMoney);
    setExtraItems(
      opening.items
        .slice(1)
        .map((x) => ({ value: money(x.value), dueDate: x.dueDate.slice(0, 10) })),
    );
    setReason('');
    setConfirmation(false);
  };
  return (
    <Card title="Obrigações anteriores à implantação">
      {retained.storageFailed && (
        <Notice kind="warning" title="O rascunho desta obrigação não foi salvo no dispositivo." />
      )}
      {retained.state.status === 'loaded' && retained.state.result.status === 'found' && (
        <Button
          disabled={command.busy}
          onClick={() => {
            if (retained.state.status === 'loaded' && retained.state.result.status === 'found') {
              const r = retained.state.result.data;
              setAccount(r.accountId);
              setClient(r.clientId);
              setOperator(r.operatorId);
              setType(r.typeId);
              setKind(r.kind);
              setAsOf(r.asOf);
              setDue(r.due);
              setReference(r.reference);
              setEvidence(r.evidence);
              setAmount(r.amount);
              setExtraItems(r.extraItems);
              setEditing(null);
              setInvalidDates({});
              setConfirmation(false);
            }
          }}
        >
          Restaurar rascunho de obrigação
        </Button>
      )}
      <p>
        A obrigação entra nos recebíveis sem movimentar caixa. Para corrigir uma origem manual, use
        substituição ou cancelamento; o servidor verifica consumo e bloqueios.
      </p>
      {editing && (
        <Notice kind="info" title="Substituição de obrigação existente">
          <p>
            {editing.sourceReference} · versão {editing.version}
          </p>
          <Button onClick={() => setEditing(null)}>Voltar ao novo registro</Button>
        </Notice>
      )}
      <fieldset
        disabled={command.busy || entities.pending || entities.failed}
        onChangeCapture={(event) => {
          if (event.target instanceof HTMLInputElement && event.target.type === 'checkbox') return;
          retained.markDirty();
          setConfirmation(false);
        }}
        className="flex flex-col gap-stack-md"
      >
        <Picker
          label="Obrigação"
          value={kind}
          options={[
            { value: 'fiado', label: 'Saldo fiado' },
            { value: 'cheque', label: 'Cheque em aberto' },
          ]}
          onChange={setKind}
        />
        <Picker
          label="Conta"
          value={accountId}
          options={(entities.accounts.data?.data.accounts ?? [])
            .filter((x) => x.type === (kind === 'cheque' ? 'Safe' : 'Tab'))
            .map((x) => ({ value: x.id, label: x.name }))}
          onChange={setAccount}
        />
        <Picker
          label="Cliente"
          value={clientId}
          options={entities.clientOptions}
          onChange={setClient}
        />
        <Picker
          label="Operador do registro"
          value={operatorId}
          options={entities.operatorOptions}
          onChange={setOperator}
        />
        <Picker
          label="Tipo da obrigação"
          value={typeId}
          options={(entities.types.data?.data.items ?? [])
            .filter(
              (x) =>
                !x.systemRole &&
                x.financialClass === 'CaixaAndClientMoney' &&
                x.monthlyFinancialRole === 'None' &&
                !x.fiadoPaymentKind &&
                (kind === 'cheque'
                  ? x.settlementInstrument === 'Cheque' && !x.requiresTabAccountAndClient
                  : x.settlementInstrument === 'None' && x.requiresTabAccountAndClient),
            )
            .map((x) => ({ value: x.id, label: `${x.categoryName} · ${x.name}` }))}
          onChange={setType}
        />
        <FormField label="Data da implantação">
          <DateField
            onInvalidChange={(invalid) => setInvalidDates({ ...invalidDates, asOf: invalid })}
            value={asCalendarDate(asOf)}
            today={asCalendarDate(today)!}
            onValueChange={(value) => {
              retained.markDirty();
              setConfirmation(false);
              setAsOf(value ?? '');
            }}
          />
        </FormField>
        <FormField label="Vencimento">
          <DateField
            onInvalidChange={(invalid) => setInvalidDates({ ...invalidDates, due: invalid })}
            value={asCalendarDate(due)}
            today={asCalendarDate(today)!}
            onValueChange={(value) => {
              retained.markDirty();
              setConfirmation(false);
              setDue(value ?? '');
            }}
          />
        </FormField>
        <FormField label="Valor">
          <MoneyInput value={amount} onValueChange={setAmount} />
        </FormField>
        {kind === 'cheque' && (
          <>
            {extraItems.map((item, index) => (
              <div key={index} className="flex flex-col gap-stack-sm">
                <FormField label={`Valor da parcela ${index + 2}`}>
                  <MoneyInput
                    value={item.value}
                    onValueChange={(value) =>
                      setExtraItems(extraItems.map((x, i) => (i === index ? { ...x, value } : x)))
                    }
                  />
                </FormField>
                <FormField label={`Vencimento da parcela ${index + 2}`}>
                  <DateField
                    onInvalidChange={(invalid) =>
                      setInvalidDates({ ...invalidDates, [`extra-${index}`]: invalid })
                    }
                    value={asCalendarDate(item.dueDate)}
                    today={asCalendarDate(today)!}
                    onValueChange={(dueDate) =>
                      setExtraItems(
                        extraItems.map((x, i) =>
                          i === index ? { ...x, dueDate: dueDate ?? '' } : x,
                        ),
                      )
                    }
                  />
                </FormField>
                <Button onClick={() => setExtraItems(extraItems.filter((_, i) => i !== index))}>
                  Remover parcela {index + 2}
                </Button>
              </div>
            ))}
            <Button
              disabled={extraItems.length >= 119}
              onClick={() => {
                retained.markDirty();
                setConfirmation(false);
                setExtraItems([...extraItems, { value: blankMoney, dueDate: today }]);
              }}
            >
              Adicionar parcela de cheque
            </Button>
          </>
        )}
        <FormField label="Referência da origem">
          <Input value={reference} onChange={(e) => setReference(e.target.value)} />
        </FormField>
        <FormField label="Evidência da obrigação">
          <Textarea value={evidence} onChange={(e) => setEvidence(e.target.value)} />
        </FormField>
        {editing && (
          <FormField label="Motivo da substituição ou cancelamento">
            <Textarea value={reason} onChange={(e) => setReason(e.target.value)} />
          </FormField>
        )}
        <label>
          <input
            type="checkbox"
            checked={confirmation}
            onChange={(e) => setConfirmation(e.target.checked)}
          />{' '}
          Conferi os valores, vencimentos, a origem e a evidência desta obrigação.
        </label>
        <Button
          disabled={
            !confirmation ||
            datesInvalid ||
            amount.kind === 'blank' ||
            (kind === 'cheque' && extraItems.some((x) => x.value.kind === 'blank'))
          }
          onClick={() =>
            amount.kind === 'amount' &&
            void command.run({
              ...(editing
                ? { kind: 'amend' as const, id: editing.id, version: editing.version, reason }
                : { kind: 'opening' as const }),
              request: {
                accountId,
                clientId: clientId || null,
                recordedByOperatorId: operatorId,
                transactionTypeId: typeId,
                asOfDate: asOf,
                isCheque: kind === 'cheque',
                sourceReference: reference,
                sourceEvidence: evidence,
                items: [
                  { value: wireFromCents(centsFromMoneyDigits(amount.digits)), dueDate: due },
                  ...(kind === 'cheque'
                    ? extraItems.flatMap((x) =>
                        x.value.kind === 'amount'
                          ? [
                              {
                                value: wireFromCents(centsFromMoneyDigits(x.value.digits)),
                                dueDate: x.dueDate,
                              },
                            ]
                          : [],
                      )
                    : []),
                ],
              },
            })
          }
        >
          {editing ? 'Confirmar substituição da obrigação' : 'Registrar obrigação'}
        </Button>
        {editing && (
          <Button
            disabled={!confirmation || !reason.trim()}
            onClick={() =>
              void command.run({ kind: 'cancel', id: editing.id, version: editing.version, reason })
            }
          >
            Confirmar cancelamento da obrigação
          </Button>
        )}
      </fieldset>
      {entities.failed && (
        <Notice
          kind="warning"
          title="Não foi possível carregar os cadastros. Atualize antes de registrar."
        />
      )}
      <details>
        <summary>Consultar obrigações registradas</summary>
        {listed.isError && (
          <Notice kind="warning" title="Não foi possível carregar as obrigações." />
        )}
        <Button onClick={() => void listed.refetch()}>Atualizar obrigações</Button>
        {(listed.data?.data ?? []).map((x) => (
          <div key={x.id} className="flex flex-col gap-stack-sm">
            <p>
              {x.sourceReference} · {x.isCheque ? 'Cheque' : 'Fiado'} ·{' '}
              {x.active ? 'Ativa' : 'Cancelada'} · {x.items.length} parcelas
            </p>
            {x.items.map((item) => (
              <p key={item.id}>
                {formatSignedBrlWire(item.value)} ·{' '}
                {formatDate(asCalendarDate(item.dueDate.slice(0, 10))!)} · {item.status}
              </p>
            ))}
            {x.sourceImportId && (
              <p>
                Obrigação importada: revisão manual indisponível. Consulte a importação de origem.
              </p>
            )}
            <Button
              disabled={!x.active || !!x.sourceImportId || command.busy}
              onClick={() => prepare(x)}
            >
              Revisar obrigação {x.sourceReference}
            </Button>
          </div>
        ))}
      </details>
      {command.feedback}
    </Card>
  );
}

export function PhysicalOpening({ today }: { today: string }) {
  const client = useScopedApiClient();
  const entities = useEntities();
  const [accountId, setAccount] = useState('');
  const [asOfDate, setDate] = useState(today);
  const [items, setItems] = useState<{ productId: string; value: MoneyValue }[]>([]);
  const [invalidDate, setInvalidDate] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [messages, setMessages] = useState<readonly string[]>([]);
  const [unknown, setUnknown] = useState(false);
  const current = useGetSessionOpeningCount(
    { accountId },
    { request: { client }, query: { enabled: !!accountId, retry: false } },
  );
  const retained = useGoLiveDraft(
    'go-live-physical-input',
    { accountId, asOfDate, items },
    entities.accounts.isSuccess,
  );
  const absent = current.isError && isApiError(current.error) && current.error.status === 404;
  const save = async () => {
    setBusy(true);
    try {
      await postSessionOpeningCount(
        {
          accountId,
          asOfDate,
          expectedRevision: absent ? null : current.data?.data.revision,
          items: items.flatMap((x) =>
            x.value.kind === 'amount'
              ? [
                  {
                    productId: x.productId,
                    value: wireFromCents(centsFromMoneyDigits(x.value.digits)),
                  },
                ]
              : [],
          ),
        },
        { client },
      );
      retained.clear();
      setMessages([
        'Contagem inicial registrada. Ela será proposta na abertura do primeiro turno.',
      ]);
      setConfirmed(false);
      await current.refetch();
    } catch (error) {
      if (isApiError(error) && error.kind === 'http') setMessages(error.errorMessages);
      else {
        setUnknown(true);
        setMessages([
          'Resultado não confirmado. Consulte a contagem atual antes de decidir uma nova tentativa.',
        ]);
      }
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card title="Contagem física inicial">
      {retained.storageFailed && (
        <Notice kind="warning" title="A contagem não está salva neste dispositivo." />
      )}
      {retained.state.status === 'loaded' && retained.state.result.status === 'found' && (
        <Button
          disabled={busy}
          onClick={() => {
            if (retained.state.status === 'loaded' && retained.state.result.status === 'found') {
              const r = retained.state.result.data;
              setAccount(r.accountId);
              setDate(r.asOfDate);
              setItems(r.items);
              setConfirmed(false);
            }
          }}
        >
          Restaurar rascunho de contagem
        </Button>
      )}
      <p>
        Registre cada produto contado. Esta contagem não é receita nem fechamento oficial. Uma lista
        vazia representa zero e exige confirmação; a abertura do turno também pede seu
        reconhecimento.
      </p>
      <Picker
        label="Caixa da contagem inicial"
        value={accountId}
        options={(entities.accounts.data?.data.accounts ?? [])
          .filter((x) => x.type === 'Terminal')
          .map((x) => ({ value: x.id, label: x.name }))}
        onChange={(value) => {
          retained.markDirty();
          setAccount(value);
          setConfirmed(false);
          setUnknown(false);
        }}
      />
      <Button
        disabled={!accountId || busy}
        onClick={async () => {
          await current.refetch();
          setUnknown(false);
          setConfirmed(false);
        }}
      >
        Consultar contagem atual
      </Button>
      {current.data && (
        <p>
          Contagem atual: revisão {current.data.data.revision} ·{' '}
          {formatDate(asCalendarDate(current.data.data.asOfDate.slice(0, 10))!)}
        </p>
      )}
      <fieldset
        disabled={busy || unknown || (!current.isSuccess && !absent)}
        onChangeCapture={(event) => {
          if (event.target instanceof HTMLInputElement && event.target.type === 'checkbox') return;
          retained.markDirty();
        }}
        className="flex flex-col gap-stack-md"
      >
        <FormField label="Data da contagem inicial">
          <DateField
            onInvalidChange={setInvalidDate}
            value={asCalendarDate(asOfDate)}
            today={asCalendarDate(today)!}
            onValueChange={(value) => {
              retained.markDirty();
              setDate(value ?? '');
              setConfirmed(false);
            }}
          />
        </FormField>
        {items.map((item, index) => (
          <div key={index}>
            <Picker
              label={`Produto contado ${index + 1}`}
              value={item.productId}
              options={entities.productOptions}
              onChange={(productId) => {
                setItems(items.map((x, i) => (i === index ? { ...x, productId } : x)));
                setConfirmed(false);
              }}
            />
            <FormField label={`Valor contado ${index + 1}`}>
              <MoneyInput
                value={item.value}
                onValueChange={(value) => {
                  setItems(items.map((x, i) => (i === index ? { ...x, value } : x)));
                  setConfirmed(false);
                }}
              />
            </FormField>
            <Button
              onClick={() => {
                retained.markDirty();
                setItems(items.filter((_, i) => i !== index));
                setConfirmed(false);
              }}
            >
              Remover produto {index + 1}
            </Button>
          </div>
        ))}
        <Button
          onClick={() => {
            retained.markDirty();
            setItems([...items, { productId: '', value: blankMoney }]);
            setConfirmed(false);
          }}
        >
          Adicionar produto contado
        </Button>
        <label>
          <input
            type="checkbox"
            checked={confirmed}
            onChange={(e) => setConfirmed(e.target.checked)}
          />{' '}
          {items.length
            ? 'Conferi todos os produtos e valores desta contagem.'
            : 'Confirmo expressamente que a contagem física inicial é zero.'}
        </label>
        <Button
          disabled={
            invalidDate || !confirmed || items.some((x) => !x.productId || x.value.kind === 'blank')
          }
          onClick={() => void save()}
        >
          Registrar contagem inicial
        </Button>
      </fieldset>
      {messages.map((message, i) => (
        <Notice key={i} kind="info" title={message} />
      ))}
    </Card>
  );
}
