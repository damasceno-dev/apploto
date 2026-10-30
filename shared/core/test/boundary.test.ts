// Shared M3 5.2: `@loto/core` is pure, platform-neutral TS. These checks read the source; they do
// not trust a convention.
import { readdirSync, readFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';
import { describe, expect, it } from 'vitest';
import * as core from '../src/index';

const packageDir = fileURLToPath(new URL('..', import.meta.url));
const srcDir = join(packageDir, 'src');
const files = (function walk(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) =>
    entry.isDirectory()
      ? walk(join(dir, entry.name))
      : entry.name.endsWith('.ts')
        ? [join(dir, entry.name)]
        : [],
  );
})(srcDir);
const rel = (file: string) => relative(srcDir, file).split(sep).join('/');

// Checked on the syntax tree, so comments and copy strings (`'Nager.Date'`) never match.
const forbiddenGlobals = new Set([
  'process',
  'window',
  'document',
  'navigator',
  'localStorage',
  'sessionStorage',
  'indexedDB',
  'globalThis',
  'self',
  'require',
  'eval',
  'Function',
  'performance',
  'crypto',
  'fetch',
  'setTimeout',
  'setInterval',
]);
const forbiddenMembers = new Set([
  'now',
  'random',
  'resolvedOptions',
  'toLocaleString',
  'toLocaleDateString',
  'toLocaleTimeString',
  'toLocaleUpperCase',
  'toLocaleLowerCase',
]);

/** Violations in one source file; `name` is its path under `src/`. */
function scan(name: string, text: string): string[] {
  const violations: string[] = [];
  const source = ts.createSourceFile(name, text, ts.ScriptTarget.ES2022, true);
  source.forEachChild(function visit(node) {
    if (ts.isMetaProperty(node)) violations.push(`${name}: import.meta`);
    if (ts.isIdentifier(node)) {
      const parent = node.parent;
      const isMemberName = ts.isPropertyAccessExpression(parent) && parent.name === node;
      const isDeclarationName =
        (ts.isPropertyAssignment(parent) ||
          ts.isPropertySignature(parent) ||
          ts.isMethodDeclaration(parent) ||
          ts.isPropertyDeclaration(parent)) &&
        parent.name === node;
      if (isMemberName && forbiddenMembers.has(node.text))
        violations.push(`${name}: .${node.text}`);
      if (!isMemberName && !isDeclarationName) {
        if (forbiddenGlobals.has(node.text)) violations.push(`${name}: ${node.text}`);
        // The engine's Intl and Date appear only where the instant conversion needs zone data.
        if ((node.text === 'Intl' || node.text === 'Date') && name !== 'dates/instant.ts') {
          violations.push(`${name}: ${node.text}`);
        }
      }
    }
    if (
      ts.isNewExpression(node) &&
      ts.isIdentifier(node.expression) &&
      node.expression.text === 'Date' &&
      (node.arguments?.length ?? 0) === 0
    ) {
      violations.push(`${name}: new Date() reads the clock`);
    }
    node.forEachChild(visit);
  });
  return violations;
}

describe('purity boundary', () => {
  it('compiles against ES2022 alone: no DOM, Node, React or bundler types in core files', () => {
    const program = ts.createProgram(files, {
      strict: true,
      noUncheckedIndexedAccess: true,
      module: ts.ModuleKind.ESNext,
      moduleResolution: ts.ModuleResolutionKind.Bundler,
      target: ts.ScriptTarget.ES2022,
      lib: ['lib.es2022.d.ts'],
      types: [],
      noEmit: true,
      skipLibCheck: true,
    });
    const coreDiagnostics = ts
      .getPreEmitDiagnostics(program)
      .filter((diagnostic) => diagnostic.file?.fileName.startsWith(srcDir) === true)
      .map(
        (diagnostic) =>
          `${rel(diagnostic.file?.fileName ?? '')}: ${ts.flattenDiagnosticMessageText(diagnostic.messageText, ' ')}`,
      );
    expect(files.length).toBeGreaterThan(5);
    expect(coreDiagnostics).toEqual([]);
  });

  it('imports only relative modules and types from the @loto/api root entry', () => {
    const violations: string[] = [];
    for (const file of files) {
      const source = ts.createSourceFile(
        file,
        readFileSync(file, 'utf8'),
        ts.ScriptTarget.ES2022,
        true,
      );
      source.forEachChild(function visit(node) {
        if (ts.isImportDeclaration(node) || ts.isExportDeclaration(node)) {
          const specifier = node.moduleSpecifier;
          if (specifier !== undefined && ts.isStringLiteral(specifier)) {
            const name = specifier.text;
            const typeOnly = ts.isImportDeclaration(node)
              ? node.importClause?.isTypeOnly === true
              : node.isTypeOnly;
            if (
              name === '@loto/api' ? !typeOnly : !name.startsWith('./') && !name.startsWith('../')
            ) {
              violations.push(`${rel(file)}: ${typeOnly ? 'type ' : ''}import ${name}`);
            }
          }
        }
        if (ts.isCallExpression(node) && node.expression.kind === ts.SyntaxKind.ImportKeyword) {
          violations.push(`${rel(file)}: dynamic import`);
        }
        node.forEachChild(visit);
      });
    }
    expect(violations).toEqual([]);
  });

  it('reads no clock, environment, storage, device locale or global state, and evaluates no code', () => {
    expect(files.flatMap((file) => scan(rel(file), readFileSync(file, 'utf8')))).toEqual([]);
  });

  it('the scan has teeth: seeded violations are found, comments and strings are not', () => {
    const seededSource = [
      'const a = process.env.X;',
      'const b = Date.now();',
      'const c = new Date();',
      'const d = Intl.DateTimeFormat;',
      'const e = window.localStorage;',
      'const f = import.meta;',
      'const g = (1).toLocaleString();',
      "const h = 'Nager.Date'; // Date.now() in a comment",
    ].join('\n');
    expect(scan('format/seed.ts', seededSource)).toEqual([
      'format/seed.ts: process',
      'format/seed.ts: Date',
      'format/seed.ts: .now',
      'format/seed.ts: new Date() reads the clock',
      'format/seed.ts: Date',
      'format/seed.ts: Intl',
      'format/seed.ts: window',
      'format/seed.ts: import.meta',
      'format/seed.ts: .toLocaleString',
    ]);
    expect(
      scan('dates/instant.ts', 'const x = new Date(0); const y = Intl.DateTimeFormat;'),
    ).toEqual([]);
  });

  it('exports only the root entry from package.json, with no build output', () => {
    const manifest = JSON.parse(readFileSync(join(packageDir, 'package.json'), 'utf8')) as Record<
      string,
      unknown
    >;
    expect(manifest.exports).toEqual({ '.': './src/index.ts' });
    expect(manifest.main).toBeUndefined();
    expect(readdirSync(packageDir)).not.toContain('dist');
  });
});

describe('public API', () => {
  it('is exactly this list (adding or removing an export is a deliberate change)', () => {
    expect(Object.keys(core).sort()).toEqual([
      'EM_DASH',
      'MAX_CALENDAR_YEAR',
      'MAX_CENTS',
      'MAX_EXPRESSION_DEPTH',
      'MAX_EXPRESSION_LENGTH',
      'MAX_EXPRESSION_TOKENS',
      'MAX_MONEY_DIGITS',
      'MINUS_SIGN',
      'MIN_CALENDAR_YEAR',
      'NO_BREAK_SPACE',
      'accountTypeLabels',
      'addDays',
      'addMonths',
      'agingBucketLabels',
      'asCents',
      'brazilianHolidayCalendarSourceLabels',
      'brazilianHolidayImportStatusLabels',
      'brazilianHolidayTypeLabels',
      'calendarDateFromParts',
      'calendarDateParts',
      'centsFromMoneyDigits',
      'centsFromWire',
      'compareCalendarDates',
      'custodyExceptionActionLabels',
      'dailyCloseReviewEventTypeLabels',
      'dailyCloseStatusLabels',
      'daysBetween',
      'daysInMonth',
      'directionLabels',
      'enumLabel',
      'enumLabels',
      'evaluateMoneyExpression',
      'fiadoPaymentKindLabels',
      'formatBrl',
      'formatBrlAmount',
      'formatBrlWire',
      'formatCep',
      'formatCnpj',
      'formatCount',
      'formatCpf',
      'formatDate',
      'formatDateWithWeekday',
      'formatDecimal',
      'formatDuration',
      'formatIdentifier',
      'formatInstallment',
      'formatInstant',
      'formatInstantDate',
      'formatInstantTime',
      'formatLongDate',
      'formatMoneyDigits',
      'formatMonthField',
      'formatMonthYear',
      'formatOptional',
      'formatPercent',
      'formatPhone',
      'formatSignedBrl',
      'formatSignedBrlWire',
      'formatSignedDuration',
      'formatWallDateTime',
      'formatWallTime',
      'generatedMovementMarker',
      'holidaySourceLabels',
      'identifierDigits',
      'installmentPlanStatusLabels',
      'invitationStatusLabels',
      'isLeapYear',
      'isValidCnpj',
      'isValidCpf',
      'isWireMoney',
      'mailDeliveryStatusLabels',
      'maskIdentifierInput',
      'moneyDigitsFromCents',
      'monthName',
      'monthlyFinancialRoleLabels',
      'monthlyReconciliationBlockerTypeLabels',
      'normalizeMoneyDigits',
      'openingDiscrepancyAttributionLabels',
      'operatorLinkRequestStatusLabels',
      'overviewSectionStatusLabels',
      'parseBrl',
      'parseCalendarDate',
      'parseDateInput',
      'parseInstant',
      'parseWallTime',
      'presentFiadoBalance',
      'presentFiadoBalanceWire',
      'roleLabels',
      'sessionCustodyStateLabels',
      'sessionEventKindLabels',
      'sessionHandoverStateLabels',
      'sessionMappingStateLabels',
      'sessionOpeningSourceKindLabels',
      'sessionOriginLabels',
      'settlementInstrumentLabels',
      'settlementRuleLabels',
      'spellBrl',
      'spellSignedBrl',
      'timeEntryStatusLabels',
      'timeEntryTapActionLabels',
      'transactionGenerationKindLabels',
      'transactionStatusLabels',
      'transactionTypeSystemRoleLabels',
      'weekdayName',
      'weekdayOf',
      'weekdayShortName',
      'wireFromCents',
    ]);
  });

  it('has no side effects on import and freezes nothing shared by accident', async () => {
    const before = Object.keys(globalThis).sort();
    await import('../src/index');
    expect(Object.keys(globalThis).sort()).toEqual(before);
  });
});
