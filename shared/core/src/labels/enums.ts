// pt-BR labels for every displayed contract enum (design/specs/copy-guidelines.md §5), keyed by
// the GENERATED enum types of `@loto/api` (type-only: nothing of the API package runs here). Each
// map is a `Record<Enum, string>`, so a backend enum change fails typecheck until it is labelled,
// and an extra key fails too. Labels are sentence case and literal copy; tenant data (names) is
// never passed through these maps.
import type {
  AccountType,
  AgingBucket,
  BrazilianHolidayCalendarSource,
  BrazilianHolidayImportStatus,
  BrazilianHolidayType,
  CustodyExceptionAction,
  DailyCloseReviewEventType,
  DailyCloseStatus,
  Direction,
  FiadoPaymentKind,
  HolidaySource,
  InstallmentPlanStatus,
  InvitationStatus,
  MailDeliveryStatus,
  MonthlyFinancialRole,
  MonthlyReconciliationBlockerType,
  OpeningDiscrepancyAttribution,
  OperatorLinkRequestStatus,
  OverviewSectionStatus,
  Role,
  SessionApprovalMode,
  SessionCandidateState,
  SessionCountAttemptKind,
  SessionCustodyState,
  SessionEventKind,
  SessionHandoverState,
  SessionMappingState,
  SessionOpeningSourceKind,
  SessionOrigin,
  SettlementInstrument,
  SettlementRule,
  TimeEntryStatus,
  TimeEntryTapAction,
  TransactionGenerationKind,
  TransactionStatus,
  TransactionTypeSystemRole,
} from '@loto/api';

type Labels<E extends string> = Readonly<Record<E, string>>;

export const roleLabels: Labels<Role> = {
  Admin: 'Administrador',
  Manager: 'Gerente',
  Member: 'Membro',
};

export const directionLabels: Labels<Direction> = { In: 'Entrada', Out: 'Saída' };

export const accountTypeLabels: Labels<AccountType> = {
  Terminal: 'Terminal',
  BankAccount: 'Conta bancária',
  Tab: 'Conta fiado',
};

/** Feminine — agrees with *transação*. */
export const transactionStatusLabels: Labels<TransactionStatus> = {
  Draft: 'Rascunho',
  Active: 'Ativa',
  Cancelled: 'Cancelada',
};

/** Masculine — agrees with *fechamento*. */
export const dailyCloseStatusLabels: Labels<DailyCloseStatus> = {
  Draft: 'Rascunho',
  Submitted: 'Enviado',
  Approved: 'Aprovado',
  Rejected: 'Rejeitado',
};

/** En dash in ranges, no spaces; `Current` includes future-due rows. */
export const agingBucketLabels: Labels<AgingBucket> = {
  Current: 'Em dia',
  Days0To30: '0–30 dias',
  Days31To60: '31–60 dias',
  Days61To90: '61–90 dias',
  Days91Plus: 'Mais de 90 dias',
};

export const timeEntryStatusLabels: Labels<TimeEntryStatus> = {
  Present: 'Presente',
  DayOff: 'Folga',
  Sunday: 'Domingo',
  Holiday: 'Feriado',
  Vacation: 'Férias',
  JustifiedAbsence: 'Falta justificada',
  UnjustifiedAbsence: 'Falta injustificada',
};

export const settlementRuleLabels: Labels<SettlementRule> = {
  SameDay: 'Mesmo dia',
  NextCalendarDay: 'Dia seguinte',
  NextBusinessDay: 'Próximo dia útil',
  TwoBusinessDays: '2 dias úteis',
  OperatorEnteredCheque: 'Cheque pré-datado (data informada)',
};

export const holidaySourceLabels: Labels<HolidaySource> = {
  Manual: 'Manual',
  Canonical: 'Calendário nacional',
  BrasilApi: 'BrasilAPI',
  Nager: 'Nager.Date',
};

/** The import-source picker: `HolidaySource` plus the composite option. */
export const brazilianHolidayCalendarSourceLabels: Labels<BrazilianHolidayCalendarSource> = {
  Composite: 'Todas as fontes',
  Canonical: 'Calendário nacional',
  BrasilApi: 'BrasilAPI',
  Nager: 'Nager.Date',
};

export const brazilianHolidayTypeLabels: Labels<BrazilianHolidayType> = {
  National: 'Nacional',
  OptionalFederal: 'Ponto facultativo',
};

export const brazilianHolidayImportStatusLabels: Labels<BrazilianHolidayImportStatus> = {
  Imported: 'Importado',
  Skipped: 'Ignorado',
};

/** Request-only (the clock-screen buttons); same words as `Direction`, contexts never mix. */
export const timeEntryTapActionLabels: Labels<TimeEntryTapAction> = {
  Open: 'Entrada',
  Close: 'Saída',
};

export const monthlyReconciliationBlockerTypeLabels: Labels<MonthlyReconciliationBlockerType> = {
  UnapprovedClose: 'Fechamento não aprovado',
  DraftTransactions: 'Transações em rascunho',
  MissingExpectedClose: 'Fechamento ausente',
};

/** Masculine — agrees with *plano*. */
export const installmentPlanStatusLabels: Labels<InstallmentPlanStatus> = {
  Draft: 'Rascunho',
  Active: 'Ativo',
  Mixed: 'Misto',
  Cancelled: 'Cancelado',
};

export const dailyCloseReviewEventTypeLabels: Labels<DailyCloseReviewEventType> = {
  Rejected: 'Rejeitado na conferência',
  OpeningChanged: 'Abertura alterada',
};

/** The source phrase of a generated movement; the marker itself is `generatedMovementMarker`. */
export const transactionGenerationKindLabels: Labels<TransactionGenerationKind> = {
  FiadoCountedStockSale: 'venda fiado com entrega de mercadoria',
  FiadoCashRepayment: 'recebimento de fiado em dinheiro',
};

export const fiadoPaymentKindLabels: Labels<FiadoPaymentKind> = {
  CountedCash: 'Dinheiro (entra no caixa)',
  NonCash: 'Outro meio (não entra no caixa)',
};

export const transactionTypeSystemRoleLabels: Labels<TransactionTypeSystemRole> = {
  FiadoCountedStockSaleCounterpart: 'Saída automática de mercadoria (venda fiado)',
  FiadoCashRepaymentCounterpart: 'Entrada automática de dinheiro (recebimento de fiado)',
};

/** Masculine — agrees with *convite*. */
export const invitationStatusLabels: Labels<InvitationStatus> = {
  Pending: 'Pendente',
  Accepted: 'Aceito',
  Declined: 'Recusado',
  Expired: 'Expirado',
};

/** SMTP acceptance, never inbox arrival. */
export const mailDeliveryStatusLabels: Labels<MailDeliveryStatus> = {
  Pending: 'Envio pendente',
  Sent: 'Enviado ao servidor de e-mail',
  Failed: 'Falha no envio',
};

export const settlementInstrumentLabels: Labels<SettlementInstrument> = {
  None: 'Sem classificação',
  Cheque: 'Cheque',
  CreditCard: 'Cartão de crédito',
  DebitCard: 'Cartão de débito',
};

/** Feminine — agrees with *classificação*. */
export const monthlyFinancialRoleLabels: Labels<MonthlyFinancialRole> = {
  None: 'Nenhuma',
  BolaoServiceFee: 'Tarifa de bolão',
  CommercialLeftover: 'Sobra (perda comercial)',
};

/** A section's state, never a figure: `NotConfigured` is never rendered as zero. */
export const overviewSectionStatusLabels: Labels<OverviewSectionStatus> = {
  Ready: 'Atualizado',
  Empty: 'Nada no período',
  NotConfigured: 'Não configurado',
};

/** Feminine — agrees with *solicitação*. */
export const operatorLinkRequestStatusLabels: Labels<OperatorLinkRequestStatus> = {
  Pending: 'Pendente',
  Resolved: 'Resolvida',
};

/** Custody of a turno — never its review status. Masculine — agrees with *turno*. */
export const sessionCustodyStateLabels: Labels<SessionCustodyState> = {
  Active: 'Em custódia',
  HandedOver: 'Passado adiante',
  Finished: 'Encerrado',
};

/** An Unresolved turno is read-only history: never offer an action on it. */
export const sessionMappingStateLabels: Labels<SessionMappingState> = {
  Ready: 'Resolvido',
  Unresolved: 'Histórico não resolvido',
};

export const sessionOriginLabels: Labels<SessionOrigin> = {
  Started: 'Iniciado no caixa',
  HandoverAccepted: 'Recebido em passagem',
  ManagerException: 'Iniciado pelo gerente (exceção)',
  ManagerDeclared: 'Declarado pelo gerente',
  Migrated: 'Migrado do sistema anterior',
};

/** A missing source is shown as missing, never as a zero. */
export const sessionOpeningSourceKindLabels: Labels<SessionOpeningSourceKind> = {
  ExplicitZero: 'Abertura zerada confirmada',
  OpeningCount: 'Saldo de implantação',
  PreviousSession: 'Turno anterior',
  ImportedSession: 'Turno importado',
};

/** Feminine — agrees with *passagem*. */
export const sessionHandoverStateLabels: Labels<SessionHandoverState> = {
  Offered: 'Oferecida',
  Accepted: 'Aceita',
  Disputed: 'Contestada',
  Withdrawn: 'Retirada',
};

/** Only an explicit Outgoing/Incoming attribution of the current revision counts toward a person. */
export const openingDiscrepancyAttributionLabels: Labels<OpeningDiscrepancyAttribution> = {
  Unattributed: 'Não atribuída',
  Outgoing: 'Operador que entregou',
  Incoming: 'Operador que recebeu',
  Neither: 'Nenhum dos dois',
};

export const custodyExceptionActionLabels: Labels<CustodyExceptionAction> = {
  Finish: 'Encerrar o turno',
  AcceptAndStart: 'Passar o caixa ao operador',
};

export const sessionEventKindLabels: Labels<SessionEventKind> = {
  SessionStarted: 'Turno iniciado',
  OpeningAcknowledged: 'Abertura conferida',
  OpeningDiscrepancyOpened: 'Diferença de abertura registrada',
  OpeningDiscrepancyRevised: 'Diferença de abertura revisada',
  HandoverOffered: 'Passagem oferecida',
  HandoverAccepted: 'Passagem aceita',
  HandoverDisputed: 'Passagem contestada',
  HandoverWithdrawn: 'Passagem retirada',
  SessionFinished: 'Turno encerrado',
  CustodyExceptionResolved: 'Exceção de custódia resolvida',
  SessionDeclared: 'Turno declarado',
  SessionRearranged: 'Turno reposicionado',
  AttributionRecorded: 'Diferença atribuída',
  CountConferred: 'Contagem conferida',
  RecountStarted: 'Recontagem iniciada',
  SessionApproved: 'Turno aprovado',
};

/** Feminine — agrees with *contagem*. Server-owned; `Unconfirmed` is never a confirmed difference. */
export const sessionCandidateStateLabels: Labels<SessionCandidateState> = {
  NotConferred: 'Não conferida',
  Current: 'Conferida',
  Outdated: 'Desatualizada',
  Unconfirmed: 'Não confirmada',
  Recounting: 'Em recontagem',
};

export const sessionCountAttemptKindLabels: Labels<SessionCountAttemptKind> = {
  Initial: 'Primeira conferência',
  Correction: 'Após correção',
  Recount: 'Recontagem',
  StillObserved: 'Contagem mantida após mudança no caixa',
};

export const sessionApprovalModeLabels: Labels<SessionApprovalMode> = {
  Independent: 'Aprovação independente',
  SingleActiveManager: 'Aprovar como único gestor',
};

/** The provenance marker of a generated movement (copy §4, §5): never a status. */
export const generatedMovementMarker = 'Automática';

/** Every map, by the generated enum's name (copy §5 order). */
export const enumLabels = {
  Role: roleLabels,
  Direction: directionLabels,
  AccountType: accountTypeLabels,
  TransactionStatus: transactionStatusLabels,
  DailyCloseStatus: dailyCloseStatusLabels,
  AgingBucket: agingBucketLabels,
  TimeEntryStatus: timeEntryStatusLabels,
  SettlementRule: settlementRuleLabels,
  HolidaySource: holidaySourceLabels,
  BrazilianHolidayCalendarSource: brazilianHolidayCalendarSourceLabels,
  BrazilianHolidayType: brazilianHolidayTypeLabels,
  BrazilianHolidayImportStatus: brazilianHolidayImportStatusLabels,
  TimeEntryTapAction: timeEntryTapActionLabels,
  MonthlyReconciliationBlockerType: monthlyReconciliationBlockerTypeLabels,
  InstallmentPlanStatus: installmentPlanStatusLabels,
  DailyCloseReviewEventType: dailyCloseReviewEventTypeLabels,
  TransactionGenerationKind: transactionGenerationKindLabels,
  FiadoPaymentKind: fiadoPaymentKindLabels,
  TransactionTypeSystemRole: transactionTypeSystemRoleLabels,
  InvitationStatus: invitationStatusLabels,
  MailDeliveryStatus: mailDeliveryStatusLabels,
  SettlementInstrument: settlementInstrumentLabels,
  MonthlyFinancialRole: monthlyFinancialRoleLabels,
  OverviewSectionStatus: overviewSectionStatusLabels,
  OperatorLinkRequestStatus: operatorLinkRequestStatusLabels,
  SessionCustodyState: sessionCustodyStateLabels,
  SessionMappingState: sessionMappingStateLabels,
  SessionOrigin: sessionOriginLabels,
  SessionOpeningSourceKind: sessionOpeningSourceKindLabels,
  SessionHandoverState: sessionHandoverStateLabels,
  OpeningDiscrepancyAttribution: openingDiscrepancyAttributionLabels,
  CustodyExceptionAction: custodyExceptionActionLabels,
  SessionEventKind: sessionEventKindLabels,
  SessionCandidateState: sessionCandidateStateLabels,
  SessionCountAttemptKind: sessionCountAttemptKindLabels,
  SessionApprovalMode: sessionApprovalModeLabels,
} as const;

export type EnumLabel =
  | { readonly known: true; readonly label: string }
  /** A value this build does not know (a newer API): its raw text, to render neutrally and report. */
  | { readonly known: false; readonly label: string };

/**
 * The label of `value` in `labels`. An unknown value (the API is newer than this build) is never
 * mapped to a guess: it comes back as its raw text with `known: false`, so the screen renders it
 * neutrally (Badge spec) and can report the contract mismatch.
 */
export function enumLabel<E extends string>(
  labels: Labels<E>,
  value: E | (string & {}),
): EnumLabel {
  return Object.prototype.hasOwnProperty.call(labels, value)
    ? { known: true, label: labels[value as E] }
    : { known: false, label: value };
}
