import { useEffect, useRef, useState, type RefObject } from 'react';
import type { View } from 'react-native';
import {
  getProduct,
  getProductProductId,
  isOutcomeUncertain,
  putProductProductId,
  type ResponseProductJson,
} from '@loto/api';
import { useDeleteProductProductId, useGetProduct, usePostProduct } from '@loto/api/queries';
import { formatCount } from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import {
  AppText,
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  FormField,
  Notice,
  Overlay,
  TextField,
  useToast,
} from '@/ui';
import {
  SYSTEM_PRODUCT_NAME,
  SYSTEM_PRODUCT_REASON,
  countingOrder,
  isProductInput,
  isSystemProduct,
  moveTarget,
  productPayload,
  reorderPlan,
  type ProductInput,
  type ReorderStep,
} from './contracts';
import {
  CommandNotices,
  ReadState,
  RetainedNotice,
  errorMessages,
  useAdminCommand,
  useAdminDraft,
  useAdminReads,
} from './common';

export const LIST_CHANGED =
  'A lista de produtos mudou desde que foi exibida. Nada foi enviado. Releia a lista e escolha a ordem de novo.';
export const PRODUCT_DEACTIVATION =
  'O produto sai da lista de produtos e das próximas contagens do fechamento. Este aplicativo não oferece reativação.';
type StepStatus = 'pending' | 'confirmed' | 'failed' | 'unknown';
interface Reorder {
  readonly target: readonly string[];
  readonly steps: readonly (ReorderStep & { readonly status: StepStatus })[];
}
type Editing =
  { readonly kind: 'new' } | { readonly kind: 'edit'; readonly product: ResponseProductJson };

export function ProductsScreen() {
  const client = useScopedApiClient();
  const reads = useAdminReads();
  const toast = useToast();
  const list = useGetProduct({ request: { client }, query: { staleTime: 0, enabled: reads } });
  const [editing, setEditing] = useState<Editing | null>(null);
  const [deactivating, setDeactivating] = useState<ResponseProductJson | null>(null);
  const [planned, setPlanned] = useState<{
    target: readonly string[];
    steps: readonly ReorderStep[];
  } | null>(null);
  const [reorder, setReorder] = useState<Reorder | null>(null);
  const [listChanged, setListChanged] = useState(false);
  const command = useAdminCommand();
  const trigger = useRef<View>(null);
  const items = countingOrder(list.data?.data.items ?? []);
  const refetch = async () => {
    const result = await list.refetch();
    if (result.error || !result.data) throw result.error ?? new Error('Lista indisponível.');
    return countingOrder(result.data.data.items);
  };
  /** Each step is an ordinary single-product update; nothing makes the whole list atomic. */
  const runReorder = (target: readonly string[]) => {
    void command.run(
      async () => {
        const fresh = countingOrder((await getProduct({ client })).data.items);
        const plan = reorderPlan(fresh, target);
        if (!plan) {
          // Nothing was sent: the shown list is outdated, so the person must look again.
          setReorder(null);
          setListChanged(true);
          throw new Error(LIST_CHANGED);
        }
        const steps = plan.map((step) => ({
          ...step,
          status: 'pending' as StepStatus,
        }));
        let progress: Reorder = { target, steps };
        setReorder(progress);
        for (const [index, step] of steps.entries()) {
          const current = fresh.find((p) => p.id === step.id)!;
          try {
            await putProductProductId(
              step.id,
              { name: current.name, displayOrder: step.to },
              { client },
            );
          } catch (error) {
            progress = {
              target,
              steps: progress.steps.map((s, i) =>
                i === index
                  ? { ...s, status: isOutcomeUncertain(error) ? 'unknown' : 'failed' }
                  : s,
              ),
            };
            setReorder(progress);
            throw error;
          }
          progress = {
            target,
            steps: progress.steps.map((s, i) => (i === index ? { ...s, status: 'confirmed' } : s)),
          };
          setReorder(progress);
        }
        return progress;
      },
      async (progress) => {
        setReorder(null);
        toast.show(
          progress.steps.length
            ? 'Ordem de contagem salva no servidor.'
            : 'A lista do servidor já está nesta ordem. Nada foi alterado.',
        );
        await refetch();
      },
    );
  };
  const move = (product: ResponseProductJson, direction: -1 | 1) => {
    const target = moveTarget(items, product.id, direction);
    if (!target) return;
    const steps = reorderPlan(items, target);
    if (steps && steps.length > 2) setPlanned({ target, steps });
    else runReorder(target);
  };
  const reconcile = () =>
    void refetch()
      .then(() => {
        setListChanged(false);
        command.clear();
      })
      .catch(() => undefined);
  const busy = command.blocked || !!reorder || listChanged || !reads;
  return (
    <>
      <Button
        ref={trigger}
        label="Adicionar produto"
        testID="product-new"
        disabled={!reads}
        onPress={() => setEditing({ kind: 'new' })}
      />
      <AppText>
        A lista segue a ordem de contagem do fechamento. Mudar a ordem atualiza cada produto
        separadamente.
      </AppText>
      <ReadState pending={list.isPending} error={list.error} retry={() => void list.refetch()} />
      {reorder ? (
        <Notice
          kind={command.uncertain ? 'warning' : 'danger'}
          title={
            command.pending
              ? 'Salvando a nova ordem, um produto de cada vez…'
              : 'A nova ordem não foi concluída.'
          }
          body={
            command.pending
              ? undefined
              : 'Cada produto é atualizado separadamente; os já confirmados continuam salvos. Nada é desfeito ou reenviado automaticamente. Releia a lista e escolha como continuar.'
          }
          messages={errorMessages(command.error)}
          actions={
            command.pending ? undefined : (
              <>
                <Button label="Reler lista de produtos" onPress={reconcile} />
                <Button
                  label="Concluir esta ordem"
                  variant="secondary"
                  disabled={command.blocked}
                  onPress={() => runReorder(reorder.target)}
                />
                <Button
                  label="Manter a ordem atual"
                  variant="secondary"
                  disabled={command.blocked}
                  onPress={() => {
                    setReorder(null);
                    command.clear();
                  }}
                />
              </>
            )
          }
        />
      ) : listChanged ? (
        <Notice
          kind="warning"
          title="A ordem não foi alterada."
          body={LIST_CHANGED}
          testID="product-list-changed"
          actions={<Button label="Reler lista de produtos" onPress={reconcile} />}
        />
      ) : (
        <CommandNotices
          command={command}
          receipt="Ordem salva no servidor."
          reconcile={reconcile}
        />
      )}
      {reorder ? (
        <Card title="Produtos desta mudança de ordem" testID="product-reorder-steps">
          {reorder.steps.map((step) => (
            <AppText key={step.id}>
              {`${step.name}: ordem ${step.from} → ${step.to} · ${
                step.status === 'confirmed'
                  ? 'salvo'
                  : step.status === 'unknown'
                    ? 'resultado não confirmado'
                    : step.status === 'failed'
                      ? 'recusado'
                      : 'não enviado'
              }`}
            </AppText>
          ))}
        </Card>
      ) : null}
      {list.data && !list.error ? (
        <>
          {items.length === 0 ? (
            <EmptyState kind="no-records" statement="Nenhum produto cadastrado." />
          ) : null}
          {items.map((product, index) => {
            const system = isSystemProduct(product);
            return (
              <Card
                key={product.id}
                title={`${formatCount(index + 1)}. ${product.name}`}
                testID={`product-row-${product.id}`}
              >
                <AppText>{`Ordem: ${product.displayOrder}`}</AppText>
                {system ? (
                  <Notice kind="info" title="Produto do sistema" body={SYSTEM_PRODUCT_REASON} />
                ) : null}
                <Button
                  label={`Mover ${product.name} para cima`}
                  testID={`product-up-${product.id}`}
                  variant="secondary"
                  disabled={busy || index === 0}
                  onPress={() => move(product, -1)}
                />
                <Button
                  label={`Mover ${product.name} para baixo`}
                  testID={`product-down-${product.id}`}
                  variant="secondary"
                  disabled={busy || index === items.length - 1}
                  onPress={() => move(product, 1)}
                />
                <Button
                  label={`Editar ${product.name}`}
                  testID={`product-edit-${product.id}`}
                  disabled={busy}
                  onPress={() => setEditing({ kind: 'edit', product })}
                />
                <Button
                  label={`Desativar ${product.name}`}
                  testID={`product-deactivate-${product.id}`}
                  variant="danger"
                  disabled={busy || system}
                  onPress={() => setDeactivating(product)}
                />
              </Card>
            );
          })}
        </>
      ) : null}
      <ConfirmDialog
        visible={!!planned && reads}
        title="Atualizar a ordem de vários produtos?"
        consequence={`Há produtos com a mesma ordem. Para mover este produto, ${planned?.steps.length ?? 0} produtos são renumerados, um de cada vez. Se um deles falhar, os anteriores continuam salvos e você decide como continuar.`}
        confirmLabel="Renumerar e salvar"
        onDismiss={() => setPlanned(null)}
        onConfirm={() => {
          const target = planned?.target;
          setPlanned(null);
          if (target) runReorder(target);
        }}
      >
        {planned?.steps.map((step) => (
          <AppText key={step.id}>{`${step.name}: ${step.from} → ${step.to}`}</AppText>
        ))}
      </ConfirmDialog>
      {editing ? (
        <ProductEditor
          key={editing.kind === 'new' ? 'new' : editing.product.id}
          editing={editing}
          visible={reads}
          suggestedOrder={(items.at(-1)?.displayOrder ?? 0) + 10}
          returnFocus={trigger}
          onClose={() => setEditing(null)}
          refetch={async () => {
            await refetch();
          }}
        />
      ) : null}
      {deactivating ? (
        <ProductDeactivation
          product={deactivating}
          visible={reads}
          onClose={() => setDeactivating(null)}
          refetch={async () => {
            await refetch();
          }}
        />
      ) : null}
    </>
  );
}

function ProductEditor({
  editing,
  visible,
  suggestedOrder,
  onClose,
  refetch,
  returnFocus,
}: {
  editing: Editing;
  visible: boolean;
  suggestedOrder: number;
  onClose: () => void;
  refetch: () => Promise<void>;
  returnFocus: RefObject<View | null>;
}) {
  const { engine } = useSession();
  const client = useScopedApiClient();
  const toast = useToast();
  const id = editing.kind === 'edit' ? editing.product.id : null;
  const system = editing.kind === 'edit' && isSystemProduct(editing.product);
  const base: ProductInput =
    editing.kind === 'edit'
      ? { name: editing.product.name, displayOrder: String(editing.product.displayOrder) }
      : { name: '', displayOrder: String(suggestedOrder) };
  const [input, setInput] = useState(base);
  const inputRef = useRef(input);
  const command = useAdminCommand();
  const create = usePostProduct({ request: { client } });
  const [saved, setSaved] = useState(false);
  const draft = useAdminDraft(`admin/product/${id ?? 'new'}`, isProductInput, async () => {
    if (engine.getSnapshot().branch?.role === 'Member') return false;
    if (!id) return true;
    const fresh = await getProductProductId(id, { client: engine.client() });
    return fresh.data.branchId === engine.getSnapshot().branch?.id && fresh.data.active;
  });
  // Explicit cancel abandons unsent input; an unresolved outcome keeps it for reconciliation.
  const cancel = () => {
    if (!command.uncertain && !command.postWriteError) void draft.discard();
    onClose();
  };
  const change = (next: ProductInput) => {
    inputRef.current = next;
    setInput(next);
    setSaved(false);
    void draft.keep(next, base);
  };
  const reserved = !system && input.name.trim() === SYSTEM_PRODUCT_NAME;
  const payload = productPayload(input);
  const submit = () => {
    const candidate = inputRef.current;
    const data = productPayload(candidate);
    if (!data) return;
    void command.run(
      async () => {
        const kept = await draft.keep(candidate, base);
        const result = id
          ? await putProductProductId(id, data, { client })
          : await create.mutateAsync({ data });
        return { result, kept };
      },
      async ({ kept }) => {
        setSaved(true);
        await draft.acknowledge(kept);
        toast.show('Produto salvo no servidor.');
        await refetch();
        // A new record closes only after the reread; a failure keeps the receipt here.
        if (!id) onClose();
      },
    );
  };
  return (
    <Overlay
      visible={visible}
      title={id ? `Editar ${editing.kind === 'edit' ? editing.product.name : ''}` : 'Novo produto'}
      busy={command.pending}
      returnFocus={returnFocus}
      onDismiss={cancel}
      testID="product-editor"
      footer={
        <>
          <Button
            label="Salvar"
            testID="product-save"
            loading={command.pending}
            loadingLabel="Salvando…"
            disabled={command.blocked || (!id && saved) || !payload || reserved}
            onPress={submit}
            fullWidth
          />
          <Button
            label="Cancelar"
            variant="secondary"
            disabled={command.pending}
            onPress={cancel}
            fullWidth
          />
        </>
      }
    >
      <RetainedNotice
        visible={!!draft.retained}
        onRestore={() => {
          const value = draft.retained!.input;
          inputRef.current = value;
          setInput(value);
          draft.dismissRetained();
        }}
      />
      {draft.problem ? <Notice kind="warning" title={draft.problem} /> : null}
      {saved && !command.postWriteError ? (
        <Notice kind="success" title="Produto salvo no servidor." />
      ) : null}
      <CommandNotices
        command={command}
        receipt="Produto salvo no servidor."
        reconcile={() =>
          void refetch()
            .then(() => command.clear())
            .catch(() => undefined)
        }
      />
      <FormField
        label="Nome"
        required
        readOnly={system}
        helper={system ? SYSTEM_PRODUCT_REASON : undefined}
        error={reserved ? 'Este nome é reservado ao produto do sistema.' : undefined}
      >
        <TextField
          testID="product-name"
          value={input.name}
          maxLength={255}
          onChangeText={(name) => change({ ...input, name })}
        />
      </FormField>
      <FormField
        label="Ordem de contagem"
        helper="Número inteiro; os produtos aparecem do menor para o maior."
        error={
          payload || input.displayOrder.trim() === '' ? undefined : 'Informe um número inteiro.'
        }
      >
        <TextField
          testID="product-order"
          kind="integer"
          value={input.displayOrder}
          maxLength={10}
          onChangeText={(displayOrder) => change({ ...input, displayOrder })}
        />
      </FormField>
    </Overlay>
  );
}

function ProductDeactivation({
  product,
  visible,
  onClose,
  refetch,
}: {
  product: ResponseProductJson;
  visible: boolean;
  onClose: () => void;
  refetch: () => Promise<void>;
}) {
  const client = useScopedApiClient();
  const toast = useToast();
  const command = useAdminCommand();
  const remove = useDeleteProductProductId({ request: { client } });
  const [done, setDone] = useState(false);
  useEffect(() => {
    if (done && !command.postWriteError) onClose();
  }, [done, command.postWriteError, onClose]);
  return (
    <ConfirmDialog
      visible={visible}
      title={`Desativar ${product.name}`}
      destructive
      busy={command.pending}
      busyLabel="Desativando…"
      confirmLabel="Desativar produto"
      consequence={PRODUCT_DEACTIVATION}
      onDismiss={onClose}
      onConfirm={() => {
        if (command.blocked || done || isSystemProduct(product)) return;
        void command.run(
          () => remove.mutateAsync({ productId: product.id }),
          async () => {
            toast.show('Produto desativado.');
            // Close only after the reread: a failure keeps the confirmed receipt and its recovery.
            await refetch();
            setDone(true);
          },
        );
      }}
    >
      <CommandNotices
        command={command}
        receipt="Produto desativado no servidor."
        reconcile={() =>
          void refetch()
            .then(() => {
              command.clear();
              onClose();
            })
            .catch(() => undefined)
        }
      />
    </ConfirmDialog>
  );
}
