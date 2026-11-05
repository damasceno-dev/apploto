import { useCallback, useRef, useState, type RefObject } from 'react';
import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import type { View } from 'react-native';
import {
  getBranchUsers,
  getInvitation,
  type InvitationStatus,
  type ResponseBranchUserJson,
  type ResponseInvitationJson,
  type ResponseOperatorJson,
  type Role,
} from '@loto/api';
import {
  useGetBranchUsers,
  useGetInvitation,
  useGetOperator,
  usePostInvitation,
  usePostInvitationIdReissue,
  usePutBranchUsersBranchUserIdRole,
} from '@loto/api/queries';
import { formatInstant, invitationStatusLabels, roleLabels } from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import {
  AppText,
  Badge,
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  FormField,
  Notice,
  Overlay,
  Pagination,
  Radio,
  RadioGroup,
  Select,
  Tabs,
  TextField,
  moveFocusTo,
  useToast,
} from '@/ui';
import {
  LAST_ADMIN_REASON,
  assignableRoles,
  invitationPayload,
  isInvitationInput,
  isLastActiveAdmin,
  memberRoleState,
  reissuable,
  reissueBody,
  unlinkedOperators,
  type InvitationInput,
  type ReissueChoice,
} from './contracts';
import {
  CommandNotices,
  ReadState,
  RetainedNotice,
  errorMessages,
  useAdminCommand,
  useAdminDraft,
  useAdminReads,
  useConfirmedWrite,
  useFreshGate,
} from './common';

export const OFFBOARD_IS_GUIDED =
  'Remover acesso abre o desligamento guiado. O acesso só muda quando esse fluxo for concluído.';
export const NO_INVITATION_WITHDRAWAL =
  'Um convite pendente não pode ser cancelado pela filial: ele vale até a data indicada. Reenviar invalida o link anterior.';
export const SMTP_NOT_INBOX =
  'O servidor de e-mail aceitou a mensagem; isso não confirma a chegada na caixa de entrada.';
const statusFilters = ['Pending', 'Accepted', 'Declined', 'Expired', 'all'] as const;
type StatusFilter = (typeof statusFilters)[number];
const pageSize = 50;
type Section = 'membros' | 'convites';
export interface DeliveryReceipt {
  readonly action: 'issue' | 'reissue';
  readonly invitation: ResponseInvitationJson;
}
/** The HTTP result is confirmed; SMTP acceptance is reported apart and never as inbox arrival. */
export function deliveryNotice({ action, invitation }: DeliveryReceipt) {
  const verb = action === 'issue' ? 'enviado' : 'reenviado';
  if (invitation.deliveryStatus === 'Sent')
    return {
      kind: 'success' as const,
      title: `Convite ${verb} para ${invitation.email}.`,
      body: SMTP_NOT_INBOX,
    };
  if (invitation.deliveryStatus === 'Failed')
    return {
      kind: 'warning' as const,
      title: `O convite para ${invitation.email} foi ${action === 'issue' ? 'criado' : 'renovado'}, mas o envio do e-mail falhou.`,
      body: 'O link não foi entregue. Use Reenviar na lista de convites.',
    };
  return {
    kind: 'info' as const,
    title: `O convite para ${invitation.email} foi ${action === 'issue' ? 'criado' : 'renovado'}; o envio do e-mail ainda não foi confirmado.`,
    body: 'Releia a lista de convites para ver a situação do envio.',
  };
}

export function MembersScreen() {
  const params = useLocalSearchParams<{
    section?: string;
    status?: string;
    page?: string;
    focus?: string;
  }>();
  const router = useRouter();
  const shell = useShell();
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const reads = useAdminReads();
  const section: Section = params.section === 'convites' ? 'convites' : 'membros';
  const status: StatusFilter = (statusFilters as readonly string[]).includes(params.status ?? '')
    ? (params.status as StatusFilter)
    : 'Pending';
  const page = /^\d+$/.test(params.page ?? '') && Number(params.page) > 0 ? Number(params.page) : 1;
  const members = useGetBranchUsers({
    request: { client },
    query: { staleTime: 0, enabled: reads },
  });
  const operators = useGetOperator({
    request: { client },
    query: { staleTime: 0, enabled: reads },
  });
  const invitations = useGetInvitation(
    {
      ...(status === 'all' ? {} : { Status: status as InvitationStatus }),
      Page: page,
      PageSize: pageSize,
    },
    { request: { client }, query: { staleTime: 0, enabled: reads && section === 'convites' } },
  );
  const [roleTarget, setRoleTarget] = useState<ResponseBranchUserJson | null>(null);
  const [inviting, setInviting] = useState(false);
  const [reissuing, setReissuing] = useState<ResponseInvitationJson | null>(null);
  const [receipt, setReceipt] = useState<DeliveryReceipt | null>(null);
  const toast = useToast();
  /** The notice stays at the top; a toast also reaches a person scrolled down at the row. */
  const report = (next: DeliveryReceipt) => {
    setReceipt(next);
    toast.show(deliveryNotice(next).title);
  };
  const trigger = useRef<View>(null);
  const rows = useRef(new Map<string, View>());
  useFocusEffect(
    useCallback(() => {
      if (params.focus && members.data) moveFocusTo(rows.current.get(params.focus));
    }, [params.focus, members.data]),
  );
  const actor = snapshot.branch?.role ?? 'Member';
  const me = snapshot.user?.userId;
  const memberList = members.data?.data.branchUsers ?? [];
  const operatorList = operators.data?.data.operators ?? [];
  const refetchMembers = async () => {
    const result = await members.refetch();
    if (result.error) throw result.error;
  };
  const refetchInvitations = async () => {
    const [a, b] = await Promise.all([invitations.refetch(), operators.refetch()]);
    if (a.error || b.error) throw a.error ?? b.error;
  };
  const [auditZone] = useState(() => Intl.DateTimeFormat().resolvedOptions().timeZone);
  const envelope = invitations.data?.data;
  const notice = receipt ? deliveryNotice(receipt) : null;
  return (
    <>
      <Tabs
        testID="members-tabs"
        accessibilityLabel="Seções"
        value={section}
        onValueChange={(next) => router.setParams({ section: next, focus: '' })}
        tabs={[
          { key: 'membros', label: 'Membros' },
          { key: 'convites', label: 'Convites' },
        ]}
      />
      <Button
        ref={trigger}
        label="Convidar"
        testID="invitation-new"
        disabled={!reads || !operators.data}
        onPress={() => setInviting(true)}
      />
      <Button
        label="Adicionar funcionário"
        variant="secondary"
        onPress={() => void shell.navigate('/gestao/funcionarios/novo', true)}
      />
      {notice ? (
        <Notice
          kind={notice.kind}
          title={notice.title}
          body={notice.body}
          testID="invitation-receipt"
          onDismiss={() => setReceipt(null)}
        />
      ) : null}
      {section === 'membros' ? (
        <>
          <ReadState
            pending={members.isPending}
            error={members.error}
            retry={() => void members.refetch()}
          />
          {members.data && !members.error ? (
            <>
              <AppText variant="helper">{OFFBOARD_IS_GUIDED}</AppText>
              {memberList.filter((m) => m.userId !== me).length === 0 ? (
                <EmptyState
                  kind="no-records"
                  statement="Nenhum outro membro nesta filial."
                  explanation="Convide uma pessoa por e-mail."
                />
              ) : null}
              {memberList.map((member) => {
                const state = memberRoleState(actor, me, member);
                const lastAdmin = isLastActiveAdmin(member, memberList);
                const reason =
                  state.kind !== 'allowed' ? state.reason : lastAdmin ? LAST_ADMIN_REASON : null;
                return (
                  <Card key={member.id} title={member.name} testID={`member-row-${member.id}`}>
                    {member.userId === me ? (
                      <Badge kind="feedback" tone="info" label="você" />
                    ) : null}
                    <AppText>{`E-mail: ${member.email}`}</AppText>
                    <AppText
                      testID={`member-role-${member.id}`}
                    >{`Função: ${roleLabels[member.role]}`}</AppText>
                    <AppText>{`Ativo: ${member.active ? 'Sim' : 'Não'}`}</AppText>
                    <Button
                      label={`Alterar função de ${member.name}`}
                      testID={`member-role-change-${member.id}`}
                      ref={(node) => {
                        if (node) rows.current.set(member.id, node);
                      }}
                      // The last active Admin can still be reviewed; only demotion is refused.
                      disabled={!reads || state.kind !== 'allowed'}
                      onPress={() => setRoleTarget(member)}
                    />
                    <Button
                      label={`Remover acesso de ${member.name}`}
                      testID={`member-remove-${member.id}`}
                      variant="secondary"
                      disabled={state.kind !== 'allowed' || lastAdmin}
                      onPress={() =>
                        void shell.navigate(
                          `/gestao/membros/${member.id}/desligar`,
                          true,
                          false,
                          member.id,
                        )
                      }
                    />
                    {reason ? (
                      <AppText variant="helper" testID={`member-reason-${member.id}`}>
                        {reason}
                      </AppText>
                    ) : null}
                  </Card>
                );
              })}
            </>
          ) : null}
        </>
      ) : (
        <>
          <AppText variant="helper">{NO_INVITATION_WITHDRAWAL}</AppText>
          <FormField label="Situação">
            <Select
              testID="invitation-status-filter"
              value={status}
              onValueChange={(next) => router.setParams({ status: next, page: '1' })}
              options={statusFilters.map((value) => ({
                value,
                label: value === 'all' ? 'Todas as situações' : invitationStatusLabels[value],
              }))}
            />
          </FormField>
          <ReadState
            pending={invitations.isPending}
            error={invitations.error}
            retry={() => void invitations.refetch()}
          />
          <ReadState
            pending={operators.isPending}
            error={operators.error}
            retry={() => void operators.refetch()}
          />
          {envelope && !invitations.error && operators.data ? (
            <>
              {envelope.items.length === 0 ? (
                <EmptyState
                  kind="no-matches"
                  statement={
                    status === 'all'
                      ? 'Nenhum convite.'
                      : `Nenhum convite ${invitationStatusLabels[status].toLowerCase()}.`
                  }
                />
              ) : null}
              {envelope.items.map((invitation) => (
                <InvitationRow
                  key={invitation.id}
                  invitation={invitation}
                  operators={operatorList}
                  zone={auditZone}
                  disabled={!reads}
                  onReissue={() => setReissuing(invitation)}
                />
              ))}
              {envelope.totalCount > envelope.pageSize ? (
                <Pagination
                  testID="invitation-pages"
                  listName="convites"
                  envelope={{
                    page: envelope.page,
                    pageSize: envelope.pageSize,
                    totalCount: envelope.totalCount,
                    totalPages: Math.ceil(envelope.totalCount / envelope.pageSize),
                    hasNext: envelope.page * envelope.pageSize < envelope.totalCount,
                    hasPrevious: envelope.page > 1,
                  }}
                  loading={invitations.isFetching}
                  onPageChange={(next) => router.setParams({ page: String(next) })}
                />
              ) : null}
            </>
          ) : null}
        </>
      )}
      {roleTarget && members.data ? (
        <RoleDialog
          member={roleTarget}
          members={memberList}
          visible={reads}
          onClose={() => setRoleTarget(null)}
          refetch={refetchMembers}
        />
      ) : null}
      {inviting && operators.data ? (
        <InvitationEditor
          visible={reads}
          operators={operatorList}
          returnFocus={trigger}
          onClose={() => setInviting(false)}
          onReceipt={(invitation) => report({ action: 'issue', invitation })}
          refetch={async () => {
            await refetchInvitations();
          }}
        />
      ) : null}
      {reissuing && operators.data ? (
        <ReissueDialog
          invitation={reissuing}
          operators={operatorList}
          visible={reads}
          onClose={() => setReissuing(null)}
          onReceipt={(invitation) => report({ action: 'reissue', invitation })}
          refetch={refetchInvitations}
        />
      ) : null}
    </>
  );
}

function operatorName(id: string | null | undefined, operators: readonly ResponseOperatorJson[]) {
  if (!id) return 'Nenhum';
  return operators.find((o) => o.id === id)?.name ?? 'Operador não encontrado entre os ativos';
}

function InvitationRow({
  invitation,
  operators,
  zone,
  disabled,
  onReissue,
}: {
  invitation: ResponseInvitationJson;
  operators: readonly ResponseOperatorJson[];
  zone: string;
  disabled: boolean;
  onReissue: () => void;
}) {
  const until = formatInstant(invitation.expiresAt, zone);
  return (
    <Card title={invitation.email} testID={`invitation-row-${invitation.id}`}>
      <AppText>{`Função: ${roleLabels[invitation.role]}`}</AppText>
      {invitation.role === 'Member' ? (
        <AppText testID={`invitation-operator-${invitation.id}`}>
          {`Operador: ${operatorName(invitation.operatorId, operators)}`}
        </AppText>
      ) : null}
      <Badge
        kind="status"
        domain="invitation-status"
        value={invitation.status}
        context="Situação:"
      />
      <AppText testID={`invitation-expiry-${invitation.id}`}>
        {invitation.status === 'Expired' ? `Expirou em ${until}` : `Válido até ${until}`}
      </AppText>
      <Badge
        kind="status"
        domain="mail-delivery-status"
        value={invitation.deliveryStatus}
        context="Envio:"
      />
      {invitation.deliveryStatus === 'Failed' ? (
        <AppText variant="helper">
          O e-mail não foi entregue ao servidor de envio. Use Reenviar.
        </AppText>
      ) : null}
      {invitation.status === 'Pending' ? (
        <AppText variant="helper">{`Não pode ser cancelado; vale até ${until}.`}</AppText>
      ) : null}
      {reissuable(invitation) ? (
        <Button
          label={`Reenviar convite para ${invitation.email}`}
          testID={`invitation-reissue-${invitation.id}`}
          variant="secondary"
          disabled={disabled}
          onPress={onReissue}
        />
      ) : null}
    </Card>
  );
}

function RoleDialog({
  member,
  members,
  visible,
  onClose,
  refetch,
}: {
  member: ResponseBranchUserJson;
  members: readonly ResponseBranchUserJson[];
  visible: boolean;
  onClose: () => void;
  refetch: () => Promise<void>;
}) {
  const { engine } = useSession();
  const client = useScopedApiClient();
  const toast = useToast();
  const command = useAdminCommand();
  const written = useConfirmedWrite(command, onClose);
  const gate = useFreshGate(refetch, command, written);
  const update = usePutBranchUsersBranchUserIdRole({ request: { client } });
  const [role, setRole] = useState<Role>(member.role);
  const [changed, setChanged] = useState<string | null>(null);
  const [checking, setChecking] = useState(false);
  const actor = engine.getSnapshot().branch?.role ?? 'Member';
  const roles = assignableRoles(actor);
  const lastAdmin = isLastActiveAdmin(member, members);
  /**
   * Fresh authority and membership before dispatch: the caller's role is re-verified and the
   * target must still be active with the role shown. Local selection never grants permission.
   */
  const confirm = async () => {
    if (gate.stale) return void gate.reread();
    if (command.blocked || written.confirmed || role === member.role) return;
    setChecking(true);
    setChanged(null);
    try {
      await engine.verifyEntry();
      const current = engine.getSnapshot();
      const fresh = (await getBranchUsers({ client: engine.client() })).data.branchUsers;
      const target = fresh.find((m) => m.id === member.id);
      const state = current.branch
        ? memberRoleState(current.branch.role, current.user?.userId, member)
        : null;
      if (!target || target.role !== member.role || !state || state.kind !== 'allowed') {
        await refetch().catch(() => undefined);
        return setChanged(
          !target
            ? 'Este acesso não está mais ativo nesta filial.'
            : target.role !== member.role
              ? `A função mudou para ${roleLabels[target.role]} desde a última leitura.`
              : 'Seu perfil não permite mais esta alteração.',
        );
      }
    } catch (error) {
      return setChanged(errorMessages(error).join(' ') || 'Não foi possível conferir o acesso.');
    } finally {
      setChecking(false);
    }
    void command.run(
      () => update.mutateAsync({ branchUserId: member.id, data: { role } }),
      async () => {
        written.confirm();
        toast.show(`Função de ${member.name} alterada para ${roleLabels[role]}.`);
        await refetch();
        written.settle();
      },
    );
  };
  return (
    <ConfirmDialog
      visible={visible}
      title={`Alterar função de ${member.name}`}
      busy={command.pending || checking || gate.rereading}
      busyLabel={checking ? 'Conferindo…' : gate.rereading ? 'Relendo…' : 'Salvando…'}
      confirmLabel={gate.stale ? 'Reler membros e conferir' : 'Salvar função'}
      consequence={`Função atual: ${roleLabels[member.role]}. A nova função vale no próximo acesso de ${member.name} a esta filial.`}
      onDismiss={onClose}
      onConfirm={() => void confirm()}
      testID="member-role-dialog"
    >
      {changed ? (
        <Notice
          kind="warning"
          testID="member-role-changed"
          title={changed}
          body="Nada foi enviado."
        />
      ) : null}
      {gate.stale && !command.uncertain && !command.postWriteError ? (
        <Notice
          kind="warning"
          title="Não foi possível salvar agora."
          body="Releia os membros antes de tentar de novo. Nada foi reenviado."
          messages={errorMessages(command.error)}
        />
      ) : (
        <CommandNotices
          command={command}
          receipt="Função alterada no servidor."
          reconcile={() => void gate.reread()}
          reconcileLabel="Reler membros para conferir"
        />
      )}
      <FormField label="Nova função" required>
        <RadioGroup
          testID="member-role-choice"
          value={role}
          onValueChange={(v) => setRole(v as Role)}
        >
          {roles.map((value) => (
            <Radio
              key={value}
              value={value}
              label={roleLabels[value]}
              testID={`member-role-${value}`}
              disabled={lastAdmin && value !== 'Admin'}
              description={lastAdmin && value !== 'Admin' ? LAST_ADMIN_REASON : undefined}
            />
          ))}
        </RadioGroup>
      </FormField>
    </ConfirmDialog>
  );
}

function InvitationEditor({
  visible,
  operators,
  returnFocus,
  onClose,
  onReceipt,
  refetch,
}: {
  visible: boolean;
  operators: readonly ResponseOperatorJson[];
  returnFocus: RefObject<View | null>;
  onClose: () => void;
  onReceipt: (invitation: ResponseInvitationJson) => void;
  refetch: () => Promise<void>;
}) {
  const { engine, snapshot } = useSession();
  const client = useScopedApiClient();
  const command = useAdminCommand();
  const issue = usePostInvitation({ request: { client } });
  const base: InvitationInput = { email: '', role: '', operatorId: '' };
  const [input, setInput] = useState(base);
  const inputRef = useRef(input);
  const written = useConfirmedWrite(command, onClose);
  const [found, setFound] = useState<readonly ResponseInvitationJson[] | null>(null);
  const draft = useAdminDraft('admin/invitation/new', isInvitationInput, async () => {
    const role = engine.getSnapshot().branch?.role;
    return role === 'Admin' || role === 'Manager';
  });
  const roles = assignableRoles(snapshot.branch?.role ?? 'Member');
  const available = unlinkedOperators(operators);
  const change = (next: InvitationInput) => {
    inputRef.current = next;
    setInput(next);
    setFound(null);
    void draft.keep(next, base);
  };
  const cancel = () => {
    if (!command.uncertain && !command.postWriteError) void draft.discard();
    onClose();
  };
  const submit = () => {
    const candidate = inputRef.current;
    void command.run(
      async () => {
        const kept = await draft.keep(candidate, base);
        const result = await issue.mutateAsync({ data: invitationPayload(candidate) });
        // The HTTP result is confirmed before any reread; it is reported even if the reread fails.
        onReceipt(result.data);
        return { kept };
      },
      async ({ kept }) => {
        written.confirm();
        await draft.acknowledge(kept);
        await refetch();
        written.settle();
      },
    );
  };
  /**
   * Unknown outcome: reread pending invitations for this e-mail; never issue a second one. After
   * a confirmed issue the reread only settles the receipt and closes.
   */
  const reconcile = () =>
    void (async () => {
      if (written.confirmed) {
        await refetch();
        command.clear();
        return written.settle();
      }
      const email = inputRef.current.email.trim().toLowerCase();
      const pending = await getInvitation(
        { Status: 'Pending', Page: 1, PageSize: 100 },
        { client: engine.client() },
      );
      await refetch();
      setFound(pending.data.items.filter((i) => i.email.toLowerCase() === email));
      command.clear();
    })().catch(() => undefined);
  return (
    <Overlay
      visible={visible}
      title="Convidar"
      busy={command.pending}
      returnFocus={returnFocus}
      onDismiss={cancel}
      testID="invitation-editor"
      footer={
        <>
          <Button
            label="Enviar convite"
            testID="invitation-send"
            loading={command.pending}
            loadingLabel="Enviando…"
            disabled={
              command.blocked || written.confirmed || input.email.trim() === '' || input.role === ''
            }
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
      {found ? (
        <Notice
          kind="info"
          testID="invitation-reconciled"
          title={
            found.length
              ? `Já existe convite pendente para este e-mail, válido até ${formatInstant(found[0]!.expiresAt, Intl.DateTimeFormat().resolvedOptions().timeZone)}.`
              : 'Nenhum convite pendente para este e-mail na releitura.'
          }
          body={
            found.length
              ? 'O primeiro envio chegou ao servidor. Use Reenviar na lista se precisar de um link novo.'
              : 'Confira antes de enviar de novo; nada é reenviado automaticamente.'
          }
        />
      ) : null}
      <CommandNotices
        command={command}
        receipt="Convite registrado no servidor."
        reconcile={reconcile}
        reconcileLabel="Reler convites para conferir"
      />
      <FormField label="E-mail" required>
        <TextField
          testID="invitation-email"
          kind="email"
          value={input.email}
          onChangeText={(email) => change({ ...input, email })}
        />
      </FormField>
      <FormField
        label="Função"
        required
        helper={
          roles.includes('Admin') ? undefined : 'Gerentes convidam somente Gerente ou Membro.'
        }
      >
        <RadioGroup
          testID="invitation-role"
          value={input.role || null}
          onValueChange={(role) =>
            change({
              ...input,
              role: role as Role,
              operatorId: role === 'Member' ? input.operatorId : '',
            })
          }
        >
          {roles.map((role) => (
            <Radio
              key={role}
              value={role}
              label={roleLabels[role]}
              testID={`invitation-role-${role}`}
            />
          ))}
        </RadioGroup>
      </FormField>
      {input.role === 'Member' ? (
        <FormField
          label="Operador"
          helper="Opcional. Só operadores ativos sem login. A pessoa fica vinculada a ele ao aceitar."
        >
          <Select
            testID="invitation-operator"
            value={input.operatorId}
            onValueChange={(operatorId) => change({ ...input, operatorId })}
            options={[
              { value: '', label: 'Nenhum operador' },
              ...available.map((o) => ({ value: o.id, label: o.name })),
            ]}
          />
        </FormField>
      ) : null}
    </Overlay>
  );
}

function ReissueDialog({
  invitation,
  operators,
  visible,
  onClose,
  onReceipt,
  refetch,
}: {
  invitation: ResponseInvitationJson;
  operators: readonly ResponseOperatorJson[];
  visible: boolean;
  onClose: () => void;
  onReceipt: (invitation: ResponseInvitationJson) => void;
  refetch: () => Promise<void>;
}) {
  const client = useScopedApiClient();
  const command = useAdminCommand();
  const written = useConfirmedWrite(command, onClose);
  const gate = useFreshGate(refetch, command, written);
  const reissue = usePostInvitationIdReissue({ request: { client } });
  const [choice, setChoice] = useState<ReissueChoice>('keep');
  const member = invitation.role === 'Member';
  const confirm = () => {
    if (gate.stale) return void gate.reread();
    if (command.blocked || written.confirmed) return;
    void command.run(
      async () => {
        const result = await reissue.mutateAsync({
          id: invitation.id,
          data: member ? reissueBody(choice) : undefined,
        });
        onReceipt(result.data);
        return result;
      },
      async () => {
        written.confirm();
        await refetch();
        written.settle();
      },
    );
  };
  return (
    <ConfirmDialog
      visible={visible}
      title={`Reenviar convite para ${invitation.email}?`}
      busy={command.pending || gate.rereading}
      busyLabel={gate.rereading ? 'Relendo…' : 'Enviando…'}
      confirmLabel={gate.stale ? 'Reler convites e conferir' : 'Reenviar convite'}
      consequence={`O link anterior deixa de valer e começa um novo prazo. E-mail, filial e função (${roleLabels[invitation.role]}) não mudam.`}
      onDismiss={onClose}
      onConfirm={confirm}
      testID="invitation-reissue-dialog"
    >
      {gate.stale && !command.uncertain && !command.postWriteError ? (
        <Notice
          kind="warning"
          title="Não foi possível reenviar agora."
          body="Releia os convites antes de tentar de novo. Nada foi reenviado."
          messages={errorMessages(command.error)}
        />
      ) : (
        <CommandNotices
          command={command}
          receipt="Convite renovado no servidor."
          reconcile={() => void gate.reread()}
          reconcileLabel="Reler convites para conferir"
        />
      )}
      {member ? (
        <FormField label="Operador">
          <Select
            testID="invitation-reissue-operator"
            value={choice}
            onValueChange={setChoice}
            options={[
              {
                value: 'keep',
                label: `Manter a escolha atual (${operatorName(invitation.operatorId, operators)})`,
              },
              { value: 'none', label: 'Sem operador' },
              ...unlinkedOperators(operators)
                .filter((o) => o.id !== invitation.operatorId)
                .map((o) => ({ value: o.id, label: o.name })),
            ]}
          />
        </FormField>
      ) : null}
    </ConfirmDialog>
  );
}
