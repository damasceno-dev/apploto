import type {
  ResponseAccountJson,
  ResponseBranchUserJson,
  ResponseTransactionTypeJson,
} from '@loto/api';
import {
  accountCreateRequest,
  accountPairing,
  accountUpdatePayload,
  assignableAccounts,
  assignableRoles,
  emptyAccount,
  emptyType,
  fiadoKindAllowed,
  hostsSystemType,
  invitationPayload,
  isAccountInput,
  isLastActiveAdmin,
  isSystemCategory,
  isTypeInput,
  memberRoleState,
  monthlyRoleAllowed,
  reclassifies,
  reissueBody,
  typeCreatePayload,
  typeInput,
  typePayload,
  typeProblems,
  unlinkedOperators,
  unpairedTabs,
} from '../../src/admin/contracts';

const branchId = '20000000-0000-4000-8000-000000000001';
const uid = (n: number) => `60000000-0000-4000-8000-${String(n).padStart(12, '0')}`;
const account = (
  n: number,
  type: ResponseAccountJson['type'],
  extra: Partial<ResponseAccountJson> = {},
): ResponseAccountJson => ({ id: uid(n), type, name: `Conta ${n}`, branchId, ...extra });
const type = (extra: Partial<ResponseTransactionTypeJson> = {}): ResponseTransactionTypeJson => ({
  id: uid(50),
  name: 'PIX',
  settlementRule: 'SameDay',
  requiresTabAccountAndClient: true,
  fiadoPaymentKind: 'NonCash',
  settlementInstrument: 'None',
  monthlyFinancialRole: 'None',
  categoryId: uid(40),
  categoryName: 'Recebimentos',
  createdAt: '2026-10-01T00:00:00Z',
  active: true,
  ...extra,
});

describe('account creation composes exactly one pairing choice', () => {
  const base = { ...emptyAccount('Terminal'), name: 'Caixa 2', institution: ' ', number: '12' };
  it('a new caixa pairs a new Tab explicitly by default', () => {
    expect(accountCreateRequest(base)).toEqual({
      ok: true,
      request: {
        route: 'terminal',
        data: { name: 'Caixa 2', institution: null, number: '12', createTabAccount: true },
      },
    });
  });
  it('Sem conta fiado sends an explicit false, never an omitted flag', () => {
    const result = accountCreateRequest({ ...base, pairing: 'none' });
    expect(result.ok && result.request.data).toEqual({
      name: 'Caixa 2',
      institution: null,
      number: '12',
      createTabAccount: false,
    });
  });
  it('an existing Tab sends only its id and requires a choice', () => {
    expect(accountCreateRequest({ ...base, pairing: 'existing' })).toEqual({
      ok: false,
      reason: 'Escolha a conta fiado existente.',
    });
    const result = accountCreateRequest({
      ...base,
      pairing: 'existing',
      tabAccountId: uid(9),
      terminalId: uid(8),
    });
    expect(result.ok && result.request.data).toEqual({
      name: 'Caixa 2',
      institution: null,
      number: '12',
      existingTabAccountId: uid(9),
      terminalId: uid(8),
    });
    expect(result.ok && 'createTabAccount' in result.request.data).toBe(false);
  });
  it('bank and Safe carry no pairing or terminal fields', () => {
    for (const kind of ['BankAccount', 'Safe'] as const) {
      const result = accountCreateRequest({
        ...emptyAccount(kind),
        name: 'Cofre',
        pairing: 'none',
        terminalId: uid(1),
      });
      expect(result).toEqual({
        ok: true,
        request: {
          route: kind === 'Safe' ? 'safe' : 'bank',
          data: { name: 'Cofre', institution: null, number: null },
        },
      });
    }
  });
  it('validates retained input structurally', () => {
    expect(isAccountInput(base)).toBe(true);
    expect(isAccountInput({ ...base, kind: 'Tab' })).toBe(false);
    expect(isAccountInput({ ...base, extra: 1 })).toBe(false);
  });
  it('update carries descriptive fields only (type is immutable)', () => {
    expect(accountUpdatePayload({ name: 'Banco', institution: '', number: '9' })).toEqual({
      name: 'Banco',
      institution: null,
      number: '9',
    });
  });
});

describe('pairing and assignment presentation', () => {
  const tab = account(2, 'Tab', { terminalAccountId: uid(1) });
  const caixa = account(1, 'Terminal', { tabAccountId: uid(2) });
  const free = account(3, 'Tab', { terminalAccountId: null });
  const lone = account(4, 'Terminal', { tabAccountId: null });
  const safe = account(5, 'Safe');
  const all = [caixa, tab, free, lone, safe];
  it('resolves both sides of a pairing from one read', () => {
    expect(accountPairing(caixa, all)).toMatchObject({ kind: 'paired', with: tab });
    expect(accountPairing(tab, all)).toMatchObject({ kind: 'paired', with: caixa });
    expect(accountPairing(lone, all)).toEqual({ kind: 'unpaired' });
    expect(accountPairing(safe, all)).toEqual({ kind: 'none' });
    expect(unpairedTabs(all)).toEqual([free]);
    expect(accountPairing(account(6, 'Terminal'), all)).toEqual({ kind: 'unpaired' });
  });
  it('assignable accounts exclude the operator’s active grants only', () => {
    const held = [
      {
        id: 'x',
        operatorId: uid(30),
        accountId: caixa.id,
        isPrimary: true,
        accountType: 'Terminal' as const,
        accountName: caixa.name,
        pairedTabAccountId: tab.id,
      },
    ];
    // The paired Tab is a separate grant: holding the caixa does not hide or include it.
    expect(assignableAccounts(all, held).map((a) => a.id)).toEqual([
      tab.id,
      free.id,
      lone.id,
      safe.id,
    ]);
  });
});

describe('transaction type classification', () => {
  it('fiado repayment kind only on an entrada that requires Tab and client', () => {
    expect(fiadoKindAllowed('In', true)).toBe(true);
    expect(fiadoKindAllowed('In', false)).toBe(false);
    expect(fiadoKindAllowed('Out', true)).toBe(false);
    expect(fiadoKindAllowed(undefined, true)).toBe(false);
  });
  it('monthly roles obey the category direction', () => {
    expect(monthlyRoleAllowed('BolaoServiceFee', 'In')).toBe(true);
    expect(monthlyRoleAllowed('SpecialDrawServiceFee', 'Out')).toBe(false);
    expect(monthlyRoleAllowed('CommercialLeftover', 'Out')).toBe(true);
    expect(monthlyRoleAllowed('CommercialLeftover', 'In')).toBe(false);
    expect(monthlyRoleAllowed('None', 'Out')).toBe(true);
  });
  it('a new type has no default settlement rule or repayment answer', () => {
    const input = { ...emptyType(uid(40)), name: 'PIX', requiresTabAccountAndClient: true };
    expect(typeProblems(input, 'In')).toEqual(['settlement', 'fiado-unchosen']);
    expect(
      typeProblems({ ...input, settlementRule: 'SameDay', fiadoPaymentKind: 'NonCash' }, 'In'),
    ).toEqual([]);
    expect(
      typeProblems({ ...input, settlementRule: 'SameDay', fiadoPaymentKind: 'CountedCash' }, 'Out'),
    ).toEqual(['fiado-condition']);
    expect(
      typeProblems(
        {
          ...input,
          settlementRule: 'SameDay',
          fiadoPaymentKind: 'none',
          monthlyFinancialRole: 'CommercialLeftover',
        },
        'In',
      ),
    ).toEqual(['monthly-direction']);
  });
  it('update sends every visible field; explicit None clears; no repayment kind sends null', () => {
    const loaded = type({
      settlementInstrument: 'Cheque',
      monthlyFinancialRole: 'BolaoServiceFee',
    });
    const input = typeInput(loaded);
    expect(typePayload(input)).toEqual({
      name: 'PIX',
      settlementRule: 'SameDay',
      requiresTabAccountAndClient: true,
      fiadoPaymentKind: 'NonCash',
      settlementInstrument: 'Cheque',
      monthlyFinancialRole: 'BolaoServiceFee',
    });
    const cleared = {
      ...input,
      fiadoPaymentKind: 'none' as const,
      settlementInstrument: 'None' as const,
    };
    expect(typePayload(cleared)).toMatchObject({
      fiadoPaymentKind: null,
      settlementInstrument: 'None',
    });
    expect(reclassifies(loaded, cleared)).toBe(true);
    expect(reclassifies(loaded, { ...input, name: 'Pix' })).toBe(false);
    expect(typeCreatePayload({ ...input, categoryId: uid(41) }).categoryId).toBe(uid(41));
  });
  it('a loaded type without a repayment kind maps to the explicit answer, not unchosen', () => {
    expect(typeInput(type({ fiadoPaymentKind: null })).fiadoPaymentKind).toBe('none');
    expect(isTypeInput(typeInput(type()))).toBe(true);
    expect(isTypeInput({ ...typeInput(type()), settlementInstrument: 'Pix' })).toBe(false);
  });
  it('system categories mirror the server predicate; any system type blocks deactivation', () => {
    const types = [
      type({ id: uid(1), categoryId: uid(60), systemRole: 'FiadoSaleCounterpart' }),
      type({ id: uid(2), categoryId: uid(61), systemRole: 'BolaoFee' }),
      type({ id: uid(3), categoryId: uid(62) }),
    ];
    expect(isSystemCategory(uid(60), types)).toBe(true);
    expect(isSystemCategory(uid(61), types)).toBe(false);
    expect(hostsSystemType(uid(61), types)).toBe(true);
    expect(hostsSystemType(uid(62), types)).toBe(false);
  });
});

describe('member role matrix and invitations', () => {
  const me = uid(70);
  const member = (n: number, role: ResponseBranchUserJson['role']): ResponseBranchUserJson => ({
    id: uid(n),
    userId: uid(n + 100),
    name: `Pessoa ${n}`,
    email: `p${n}@example.invalid`,
    role,
    active: true,
  });
  it('Admins assign every role; Managers never Admin', () => {
    expect(assignableRoles('Admin')).toEqual(['Admin', 'Manager', 'Member']);
    expect(assignableRoles('Manager')).toEqual(['Manager', 'Member']);
    expect(assignableRoles('Member')).toEqual([]);
  });
  it('own row, Admin target for a Manager and Member actor are explained', () => {
    const own = { ...member(1, 'Admin'), userId: me };
    expect(memberRoleState('Admin', me, own).kind).toBe('self');
    expect(memberRoleState('Manager', me, member(2, 'Admin')).kind).toBe('admin-target');
    expect(memberRoleState('Manager', me, member(3, 'Member'))).toEqual({
      kind: 'allowed',
      roles: ['Manager', 'Member'],
    });
    expect(memberRoleState('Member', me, member(3, 'Member')).kind).toBe('denied');
  });
  it('last active Admin is distinct from self protection', () => {
    const admin = member(1, 'Admin');
    expect(isLastActiveAdmin(admin, [admin, member(2, 'Manager')])).toBe(true);
    expect(isLastActiveAdmin(admin, [admin, member(2, 'Admin')])).toBe(false);
    expect(isLastActiveAdmin(member(3, 'Manager'), [admin])).toBe(false);
  });
  it('operator preselection exists only on a Member invitation', () => {
    expect(
      invitationPayload({ email: ' a@b.invalid ', role: 'Member', operatorId: uid(5) }),
    ).toEqual({
      email: 'a@b.invalid',
      role: 'Member',
      operatorId: uid(5),
    });
    expect(
      invitationPayload({ email: 'a@b.invalid', role: 'Manager', operatorId: uid(5) }).operatorId,
    ).toBeNull();
    expect(
      invitationPayload({ email: 'a@b.invalid', role: 'Member', operatorId: '' }).operatorId,
    ).toBeNull();
  });
  it('reissue omits the body to keep, sends null to clear and an id to replace', () => {
    expect(reissueBody('keep')).toBeUndefined();
    expect(reissueBody('none')).toEqual({ operatorId: null });
    expect(reissueBody(uid(6))).toEqual({ operatorId: uid(6) });
  });
  it('only active operators without a login are offered', () => {
    const ops = [
      { id: uid(1), name: 'A', branchId, userId: null },
      { id: uid(2), name: 'B', branchId, userId: uid(9) },
    ];
    expect(unlinkedOperators(ops as never).map((o) => o.id)).toEqual([uid(1)]);
  });
});
