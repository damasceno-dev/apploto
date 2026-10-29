import type { Role } from '@loto/api';
import { parseBranchPath, branchPath } from './branch-route';

export type WorkMode = 'management' | 'shift';
export type DestinationOwner = WorkMode | 'shared';
export interface Destination {
  readonly key: string;
  readonly path: string;
  readonly title: string;
  readonly owner: DestinationOwner;
  readonly milestone: number;
  readonly group?: string;
  readonly memberOnly?: boolean;
  readonly fallback?: string;
}
const item = (
  key: string,
  path: string,
  title: string,
  owner: DestinationOwner,
  milestone: number,
  group?: string,
  fallback?: string,
): Destination => ({ key, path, title, owner, milestone, group, fallback });
/** Exhaustive catalog map. Templates match only validated ids/calendar segments, never prefixes. */
export const destinations: readonly Destination[] = [
  item('queue', '/gestao/fila', 'Fila de trabalho', 'management', 7, 'Gestão'),
  item(
    'approval',
    '/gestao/conferencia/:id',
    'Conferência do fechamento',
    'management',
    7,
    undefined,
    'queue',
  ),
  item('today', '/turno/hoje', 'Hoje', 'shift', 5, 'Meu turno'),
  item('open', '/fechamentos/abrir', 'Abrir dia', 'shared', 5),
  item('close', '/fechamentos/:id', 'Fechar dia', 'shared', 5),
  item('correction', '/fechamentos/:id/correcao', 'Corrigir e reenviar', 'shared', 5),
  item('transactions', '/transacoes', 'Transações', 'shared', 6, 'Gestão'),
  item('create', '/transacoes/nova', 'Nova transação', 'shared', 6, 'Gestão', 'transactions'),
  item(
    'plan-new',
    '/transacoes/cheque-pre-datado/novo',
    'Plano de cheque pré-datado',
    'shared',
    6,
    undefined,
    'transactions',
  ),
  item(
    'plan',
    '/transacoes/cheque-pre-datado/:id',
    'Plano de cheque pré-datado',
    'shared',
    6,
    undefined,
    'transactions',
  ),
  item('edit', '/transacoes/:id', 'Transação', 'shared', 6, undefined, 'transactions'),
  item('clients', '/clientes', 'Clientes', 'shared', 10, 'Cadastros'),
  item(
    'statement',
    '/clientes/:id/extrato',
    'Extrato do cliente',
    'shared',
    6,
    undefined,
    'clients',
  ),
  item('payment', '/clientes/:id/receber', 'Receber pagamento', 'shared', 6, undefined, 'clients'),
  item('dashboard', '/relatorios/dashboard', 'Dashboard', 'management', 8, 'Relatórios', 'queue'),
  item(
    'ledger',
    '/relatorios/movimento',
    'Movimento da conta',
    'management',
    8,
    'Relatórios',
    'queue',
  ),
  item('fiado', '/relatorios/saldo-fiado', 'Saldo fiado', 'management', 8, 'Relatórios', 'queue'),
  item(
    'aging',
    '/relatorios/fiado-por-idade',
    'Fiado por idade',
    'management',
    8,
    'Relatórios',
    'queue',
  ),
  item(
    'cheques',
    '/relatorios/cheques',
    'Cheques em aberto',
    'management',
    8,
    'Relatórios',
    'queue',
  ),
  item('receivables', '/relatorios/a-receber', 'A receber', 'management', 8, 'Relatórios', 'queue'),
  item(
    'variance',
    '/relatorios/diferencas',
    'Diferenças de caixa',
    'management',
    8,
    'Relatórios',
    'queue',
  ),
  item(
    'month-close',
    '/relatorios/fechamento-mensal/:year/:month',
    'Fechamento mensal',
    'management',
    8,
    'Relatórios',
    'queue',
  ),
  item(
    'month-financial',
    '/relatorios/financeiro-mensal/:year/:month',
    'Relatório financeiro mensal',
    'management',
    8,
    'Relatórios',
    'queue',
  ),
  { ...item('clock', '/turno/ponto', 'Ponto', 'shift', 9, 'Meus números'), memberOnly: true },
  item(
    'my-transactions',
    '/turno/minhas-transacoes',
    'Minhas transações',
    'shift',
    9,
    'Meus números',
  ),
  item('my-hours', '/turno/banco-de-horas', 'Meu banco de horas', 'shift', 9, 'Meus números'),
  item(
    'operator-transactions',
    '/gestao/operadores/:id/transacoes',
    'Transações do operador',
    'management',
    9,
    undefined,
    'operators',
  ),
  item(
    'hours',
    '/gestao/ponto/banco-de-horas',
    'Banco de horas',
    'management',
    9,
    undefined,
    'time',
  ),
  item('time', '/gestao/ponto', 'Gestão de ponto', 'management', 9, 'Ponto'),
  item('operators', '/gestao/operadores', 'Operadores', 'management', 10, 'Cadastros'),
  item(
    'assignments',
    '/gestao/contas-dos-operadores',
    'Contas dos operadores',
    'management',
    10,
    'Cadastros',
  ),
  item('accounts', '/gestao/contas', 'Contas', 'management', 10, 'Cadastros'),
  item('categories', '/gestao/categorias', 'Categorias e tipos', 'management', 10, 'Cadastros'),
  item('products', '/gestao/produtos', 'Produtos', 'management', 10, 'Cadastros'),
  item('holidays', '/gestao/feriados', 'Feriados', 'management', 10, 'Cadastros'),
  item('settings', '/gestao/configuracoes', 'Configurações', 'management', 10, 'Cadastros'),
  item('members', '/gestao/membros', 'Membros da filial', 'management', 10, 'Cadastros'),
  item(
    'employee-new',
    '/gestao/funcionarios/novo',
    'Adicionar funcionário',
    'management',
    11,
    undefined,
    'operators',
  ),
  item(
    'offboard',
    '/gestao/funcionarios/:id/desligar',
    'Desligar funcionário',
    'management',
    11,
    undefined,
    'operators',
  ),
  item(
    'offboard-member',
    '/gestao/membros/:id/desligar',
    'Desligar funcionário',
    'management',
    11,
    undefined,
    'members',
  ),
];
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export function matchDestination(path: string): Destination | null {
  const parts = path.split('/');
  return (
    destinations.find((destination) => {
      const template = destination.path.split('/');
      return (
        parts.length === template.length &&
        template.every((part, index) => {
          const value = parts[index] ?? '';
          if (part === ':id') return uuid.test(value);
          if (part === ':year') return /^\d{4}$/.test(value) && Number(value) >= 1;
          if (part === ':month') return /^(?:[1-9]|0[1-9]|1[012])$/.test(value);
          return part === value;
        })
      );
    }) ?? null
  );
}
export function defaultMode(role: Role): WorkMode {
  return role === 'Member' ? 'shift' : 'management';
}
export function homeKey(mode: WorkMode): string {
  return mode === 'management' ? 'queue' : 'today';
}
export function canEnter(destination: Destination, role: Role): boolean {
  return (
    !(destination.owner === 'management' && role === 'Member') &&
    !(destination.memberOnly && role !== 'Member')
  );
}
export function destinationMode(
  destination: Destination,
  role: Role,
  restored?: WorkMode,
): WorkMode {
  return destination.owner === 'shared'
    ? role === 'Member'
      ? 'shift'
      : (restored ?? defaultMode(role))
    : destination.owner;
}
export function destinationHref(key: string, branchId: string, date: string): string {
  const destination = destinations.find((entry) => entry.key === key);
  if (!destination || destination.path.includes(':id'))
    throw new Error('Destination requires resource context.');
  return branchPath(
    branchId,
    destination.path
      .replace(':year', date.slice(0, 4))
      .replace(':month', String(Number(date.slice(5, 7)))),
  );
}
export function scopedDestination(href: string, branchId: string): Destination | null {
  try {
    const url = new URL(href, 'https://navigation.invalid');
    if (url.origin !== 'https://navigation.invalid' || url.hash) return null;
    const route = parseBranchPath(url.pathname);
    return route?.branchId === branchId ? matchDestination(route.rest) : null;
  } catch {
    return null;
  }
}
