// Compiled by typecheck, never imported into the app. Missing/stale route generation must fail.
import type { Href } from 'expo-router';

const home: Href = '/';
void home;

// @ts-expect-error This route does not exist: generated types must reject it on a clean install.
const nonexistent: Href = '/__loto_route_that_must_not_exist__';
void nonexistent;

// Every catalog destination also exists in Expo Router, including protected future pages.
const catalogRoutes: readonly Href[] = [
  { pathname: '/clientes/[id]/extrato', params: { id: '20000000-0000-4000-8000-000000000001' } },
  { pathname: '/clientes/[id]/receber', params: { id: '20000000-0000-4000-8000-000000000001' } },
  '/clientes',
  '/conta/senha',
  '/conta',
  '/entrar/esqueci-senha',
  '/entrar',
  {
    pathname: '/fechamentos/[id]/correcao',
    params: { id: '20000000-0000-4000-8000-000000000001' },
  },
  { pathname: '/fechamentos/[id]', params: { id: '20000000-0000-4000-8000-000000000001' } },
  '/fechamentos/abrir',
  '/filiais',
  '/gestao/categorias',
  { pathname: '/gestao/conferencia/[id]', params: { id: '20000000-0000-4000-8000-000000000001' } },
  '/gestao/configuracoes',
  '/gestao/contas-dos-operadores',
  '/gestao/contas',
  '/gestao/feriados',
  '/gestao/fila',
  {
    pathname: '/gestao/funcionarios/[id]/desligar',
    params: { id: '20000000-0000-4000-8000-000000000001' },
  },
  '/gestao/funcionarios/novo',
  {
    pathname: '/gestao/membros/[id]/desligar',
    params: { id: '20000000-0000-4000-8000-000000000001' },
  },
  '/gestao/membros',
  {
    pathname: '/gestao/operadores/[id]/transacoes',
    params: { id: '20000000-0000-4000-8000-000000000001' },
  },
  '/gestao/operadores',
  '/gestao/ponto/banco-de-horas',
  '/gestao/ponto',
  '/gestao/produtos',
  '/invitation',
  '/plataforma',
  '/relatorios/a-receber',
  '/relatorios/cheques',
  '/relatorios/dashboard',
  '/relatorios/diferencas',
  {
    pathname: '/relatorios/fechamento-mensal/[year]/[month]',
    params: { year: '2026', month: '10' },
  },
  '/relatorios/fiado-por-idade',
  {
    pathname: '/relatorios/financeiro-mensal/[year]/[month]',
    params: { year: '2026', month: '10' },
  },
  '/relatorios/movimento',
  '/relatorios/saldo-fiado',
  '/reset-password',
  { pathname: '/transacoes/[id]', params: { id: '20000000-0000-4000-8000-000000000001' } },
  {
    pathname: '/transacoes/cheque-pre-datado/[id]',
    params: { id: '20000000-0000-4000-8000-000000000001' },
  },
  '/transacoes/cheque-pre-datado/novo',
  '/transacoes/nova',
  '/transacoes',
  '/turno/banco-de-horas',
  '/turno/hoje',
  '/turno/minhas-transacoes',
  '/turno/ponto',
];
void catalogRoutes;
