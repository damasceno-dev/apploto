// Mobile M6 (Transactions ledger) fixture on Mobile's owned disposable stack only, on top of the
// M5 operator-day seed: a received pre-dated cheque type (cheque settlement rule, Cheque instrument)
// created through the ordinary admin API. IDs stay in the private state file.
import { readFile, writeFile } from 'node:fs/promises';
import { apiCall } from './native-stack.mjs';
const [statePath] = process.argv.slice(2);
const state = JSON.parse(await readFile(statePath, 'utf8'));
if (!state.owned || !state.day || new URL(state.apiUrl).hostname !== '127.0.0.1')
  throw new Error('Owned loopback fixture with the M5 operator-day seed required.');
if (state.ledger) throw new Error('Ledger fixture already seeded; reuse its private state.');
const call = apiCall(state.apiUrl);
const identity = (await call('POST', '/user/login', state.users.ana)).responseToken.token;
const admin = (await call('POST', '/branch/session', { branchId: state.branches.alfa }, identity))
  .token;
const categories = (await call('GET', '/category', undefined, admin)).items;
const entradas = categories.find((c) => c.name === 'Entradas' && c.defaultDirection === 'In');
if (!entradas) throw new Error('Seeded Entradas category missing.');
const cheque = await call(
  'POST',
  '/transaction-type',
  {
    categoryId: entradas.id,
    name: 'Cheque pré-datado E2E',
    settlementRule: 'OperatorEnteredCheque',
    requiresTabAccountAndClient: false,
    settlementInstrument: 'Cheque',
    financialClass: 'CaixaAndClientMoney',
  },
  admin,
);
const accounts = (await call('GET', '/account', undefined, admin)).accounts;
const bank = accounts.find((a) => a.type === 'BankAccount');
if (!bank) throw new Error('Owned bank account missing.');
state.ledger = { chequeType: cheque.id, bank: bank.id };
await writeFile(statePath, JSON.stringify(state), { mode: 0o600 });
console.log('Ledger fixtures seeded; IDs remain in private state.');
