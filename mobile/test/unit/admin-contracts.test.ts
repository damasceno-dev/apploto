import type { ResponseProductJson } from '@loto/api';
import { parseCalendarDate } from '@loto/core';
import {
  clientPayload,
  durationValue,
  emptyClient,
  holidayPayload,
  holidayYear,
  isClientInput,
  isHolidayBatch,
  isOperatorInput,
  isSettingsInput,
  isSystemProduct,
  linkableMembers,
  moveTarget,
  operatorLogin,
  operatorPayload,
  productPayload,
  reorderPlan,
  searchClients,
  clientCountLabel,
} from '../../src/admin/contracts';

const branchId = '20000000-0000-4000-8000-000000000001';
const product = (id: string, name: string, displayOrder: number): ResponseProductJson => ({
  id: `00000000-0000-4000-8000-00000000000${id}`,
  name,
  displayOrder,
  branchId,
  createdAt: '2026-10-01T00:00:00Z',
  active: true,
});
const member = (n: number, active = true) => ({
  id: `b${n}`,
  userId: `10000000-0000-4000-8000-00000000000${n}`,
  name: `Pessoa ${n}`,
  email: `p${n}@example.invalid`,
  role: 'Member' as const,
  active,
});

describe('clients', () => {
  it('sends CPF as digits, blank optionals as null and keeps other text as typed', () => {
    expect(
      clientPayload({
        ...emptyClient,
        name: ' Ana ',
        phone: '(61) 91234-5678',
        cpf: '529.982.247-25',
        cep: '   ',
        email: 'ana@example.invalid',
      }),
    ).toEqual({
      name: ' Ana ',
      phone: '(61) 91234-5678',
      cpf: '52998224725',
      cep: null,
      address: null,
      phoneSecondary: null,
      email: 'ana@example.invalid',
      notes: null,
    });
    // Text the mask could not hold reaches the server's verbatim validation unchanged.
    expect(clientPayload({ ...emptyClient, cpf: '529.98a' }).cpf).toBe('52998a');
    expect(clientPayload({ ...emptyClient, cpf: '' }).cpf).toBeNull();
  });
  it('searches the complete list by name, ignoring case and accents', () => {
    const list = ['José Álvares', 'Maria', 'joselito'].map((name, i) => ({
      id: String(i),
      name,
      phone: '1',
      branchId,
    }));
    expect(searchClients(list, 'JOSE').map((c) => c.name)).toEqual(['José Álvares', 'joselito']);
    expect(searchClients(list, '  ')).toHaveLength(3);
    expect(clientCountLabel(1)).toBe('1 cliente encontrado');
    expect(clientCountLabel(0)).toBe('0 clientes encontrados');
  });
  it('retained client input is structurally exact', () => {
    expect(isClientInput(emptyClient)).toBe(true);
    expect(isClientInput({ ...emptyClient, token: 'x' })).toBe(false);
    expect(isClientInput({ ...emptyClient, cpf: 1 })).toBe(false);
  });
});

describe('operators', () => {
  it('composes only contracted fields and resolves logins through active memberships', () => {
    expect(operatorPayload({ name: 'Ana', userId: null })).toEqual({ name: 'Ana', userId: null });
    const op = { id: 'o1', name: 'Ana', branchId, userId: member(1).userId };
    expect(operatorLogin(op, [member(1)])).toMatchObject({ kind: 'member' });
    expect(operatorLogin(op, [member(1, false)])).toEqual({ kind: 'unresolved' });
    expect(operatorLogin({ ...op, userId: null }, [member(1)])).toEqual({ kind: 'none' });
  });
  it('offers active members not linked elsewhere, keeping the edited row link', () => {
    const operators = [
      { id: 'o1', name: 'A', branchId, userId: member(1).userId },
      { id: 'o2', name: 'B', branchId, userId: member(2).userId },
    ];
    const members = [member(1), member(2), member(3), member(4, false)];
    expect(linkableMembers(members, operators, null).map((m) => m.id)).toEqual(['b3']);
    expect(linkableMembers(members, operators, 'o1').map((m) => m.id)).toEqual(['b1', 'b3']);
  });
  it('retained operator input rejects non-uuid logins', () => {
    expect(isOperatorInput({ name: 'A', userId: null })).toBe(true);
    expect(isOperatorInput({ name: 'A', userId: 'nope' })).toBe(false);
  });
});

describe('products', () => {
  const a = product('1', 'Telesena', 10);
  const b = product('2', 'Raspadinha', 20);
  const c = product('3', 'Diferença Caixa', 30);
  it('swaps distinct neighbours with two individual updates', () => {
    const target = moveTarget([a, b, c], b.id, -1)!;
    expect(target).toEqual([b.id, a.id, c.id]);
    expect(reorderPlan([a, b, c], target)).toEqual([
      { id: b.id, name: 'Raspadinha', from: 20, to: 10 },
      { id: a.id, name: 'Telesena', from: 10, to: 20 },
    ]);
    expect(moveTarget([a, b, c], a.id, -1)).toBeNull();
    expect(moveTarget([a, b, c], c.id, 1)).toBeNull();
  });
  it('distinguishes a changed product set from an already-satisfied order', () => {
    const shown = [a, b, c];
    expect(reorderPlan(shown, [a.id, b.id, c.id])).toEqual([]);
    const target = moveTarget(shown, b.id, -1)!;
    expect(reorderPlan([...shown, product('4', 'Novo', 15)], target)).toBeNull();
    expect(reorderPlan([a, c], target)).toBeNull();
    expect(reorderPlan(shown, [a.id, a.id, c.id])).toBeNull();
  });
  it('renumbers ties explicitly instead of claiming an atomic reorder', () => {
    const tied = [product('1', 'A', 0), product('2', 'B', 0), product('3', 'C', 0)];
    const target = moveTarget(tied, tied[2]!.id, -1)!;
    expect(reorderPlan(tied, target)).toEqual([
      { id: tied[2]!.id, name: 'C', from: 0, to: 10 },
      { id: tied[1]!.id, name: 'B', from: 0, to: 20 },
    ]);
  });
  it('protects only the reserved name and keeps integer parsing structural', () => {
    expect(isSystemProduct(c)).toBe(true);
    expect(isSystemProduct(a)).toBe(false);
    expect(productPayload({ name: 'X', displayOrder: '12' })).toEqual({
      name: 'X',
      displayOrder: 12,
    });
    expect(productPayload({ name: 'X', displayOrder: '1.5' })).toBeNull();
    expect(productPayload({ name: 'X', displayOrder: '-1' })).toEqual({
      name: 'X',
      displayOrder: -1,
    });
  });
});

describe('holidays', () => {
  const today = parseCalendarDate('2026-10-31')!;
  it('defaults to the authoritative branch year and bounds the contract range', () => {
    expect(holidayYear(undefined, today)).toBe(2026);
    expect(holidayYear('2027', today)).toBe(2027);
    expect(holidayYear('1899', today)).toBe(2026);
    expect(holidayYear('20x7', today)).toBe(2026);
  });
  it('builds the manual batch and names invalid rows without dispatch', () => {
    expect(
      holidayPayload({
        rows: [
          { date: '2026-11-20', description: 'Consciência Negra' },
          { date: '2026-12-08', description: ' ' },
        ],
      }),
    ).toEqual({
      ok: true,
      payload: {
        holidays: [
          { date: '2026-11-20', description: 'Consciência Negra' },
          { date: '2026-12-08', description: null },
        ],
      },
    });
    expect(holidayPayload({ rows: [{ date: '', description: '' }] })).toEqual({
      ok: false,
      rows: [0],
    });
    expect(isHolidayBatch({ rows: [{ date: '', description: '' }] })).toBe(true);
    expect(isHolidayBatch({ rows: [{ date: '', description: '', source: 'Manual' }] })).toBe(false);
  });
});

describe('settings durations', () => {
  it('a display-identical value is unchanged even when the stored decimal is not exact', () => {
    expect(durationValue(7.33, { hours: '7', minutes: '20' })).toEqual({ kind: 'unchanged' });
    expect(durationValue(1, { hours: '01', minutes: '0' })).toEqual({ kind: 'unchanged' });
  });
  it('sends exact hundredths only and never rounds an unsupported precision', () => {
    expect(durationValue(7.33, { hours: '7', minutes: '21' })).toEqual({
      kind: 'changed',
      wire: '7.35',
      display: '7h 21min',
    });
    expect(durationValue(8, { hours: '8', minutes: '30' })).toMatchObject({ wire: '8.50' });
    expect(durationValue(8, { hours: '0', minutes: '45' })).toMatchObject({ wire: '0.75' });
    expect(durationValue(8, { hours: '7', minutes: '20' })).toEqual({ kind: 'imprecise' });
    expect(durationValue(8, { hours: '7', minutes: '60' })).toEqual({ kind: 'invalid' });
    expect(durationValue(8, { hours: '', minutes: '0' })).toEqual({ kind: 'invalid' });
  });
  it('retained settings hold display text only', () => {
    const value = { hours: '8', minutes: '00' };
    expect(
      isSettingsInput({
        dailyTargetHours: value,
        lunchDeductionOver6H: value,
        lunchDeductionOver4H: value,
      }),
    ).toBe(true);
    expect(
      isSettingsInput({
        dailyTargetHours: value,
        lunchDeductionOver6H: value,
        lunchDeductionOver4H: value,
        etag: '"1"',
      }),
    ).toBe(false);
  });
});
