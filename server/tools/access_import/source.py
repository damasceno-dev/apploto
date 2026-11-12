"""Bounded, read-only Access CSV inspection. Never executes database code."""
import csv
import hashlib
import io
import json
import re
from datetime import datetime
from decimal import Decimal
from pathlib import Path

VERSION = 'm10-source-1'
MAX_FILE_BYTES = 16 * 1024 * 1024
MAX_TOTAL_BYTES = 64 * 1024 * 1024
MAX_ROWS = 100000
MAX_FIELD = 16384
# Column contracts inspected from the two external export schemas, not customer rows.
SCHEMAS = {
    'TblCategoria': 'cat_id cat_nome cat_sinal',
    'TblClientes': 'cli_id cli_nome cli_cpf cli_cep cli_endereco cli_telprincipal cli_telsecundario cli_obs cli_email cli_dataRegistro',
    'TblConfiguracao': 'Código conf_dtfechamento',
    'TblContas': 'con_id con_identificacao con_agencia con_numero con_tipo',
    'TblEstoque': 'est_id est_data prod_id est_valor con_id est_dataRegistro',
    'TblFeriados': 'Código fer_data fer_descricao',
    'TblLancamentos': 'lco_id lco_data lco_valor id_categoria id_tipo lco_descricao id_cliente lco_vencimento lco_data_pagamento id_conta lco_sinal lco_condicao lco_forma_pagamento lco_origem lco_status lco_dataRegistro',
    'TblLogExclusao': 'log_id log_dataexclusao log_IDconta log_valor log_IDcategoria log_IDtipoCategoria log_descricao log_status log_dataLancamento log_dataVencimento log_IDcliente',
    'TblProduto': 'produto_id produto_nome',
    'TblRegistroPonto': 'pon_id con_id pon_data pon_entrada pon_saida pon_status pon_totalHoras pon_saldoHoras',
    'TblTelefones': 'tel_id Telefone id_cliente',
    'TblTipoCategoria': 'tipo_id tipo_nome id_categoria',
    'TblUsuario': 'usu_id usu_nome usu_senha usu_autenticar usu_nivel con_id',
    'TblItemRel': 'id_item item_nome item_tipo item_ordem',
    'TblRelMensal': 'idrel Id_item rel_valor rel_idconta rel_data',
    'TblLoteriaEspecial': 'lot_id lot_nome lot_modalidade lot_data_inicio lot_data_fim lot_premio lot_concurso',
    'TblMovimentacaoBancoHoras': 'mov_id con_id mov_data mov_minutos mov_tipo mov_descricao pon_id',
    'TblVersaoBE': 'Código VersaoBE Data_criacao',
    'TblVersaoFE': 'Código VersaoFE data_criacao data_atualizacao',
}
BASE = tuple(list(SCHEMAS)[:13])
DATES = {'conf_dtfechamento', 'cli_dataRegistro', 'est_data', 'est_dataRegistro', 'fer_data', 'lco_data', 'lco_vencimento', 'lco_data_pagamento', 'lco_dataRegistro', 'log_dataexclusao', 'log_dataLancamento', 'log_dataVencimento', 'pon_data', 'lot_data_inicio', 'lot_data_fim', 'mov_data', 'Data_criacao', 'data_criacao', 'data_atualizacao'}
MONEY = {'est_valor', 'lco_valor', 'log_valor', 'rel_valor', 'lot_premio'}


class SourceError(ValueError):
    """Safe diagnostics contain table/row/column only, never source values."""


def money(value):
    if not re.fullmatch(r'-?\d{1,12}(?:\.\d{1,18})?', value):
        raise SourceError('invalid decimal precision/format')
    return Decimal(value)


def read_source(directory, profile, encoding):
    """Read a tables directory; explicit encoding avoids silently repairing bytes."""
    if profile not in ('windows', 'mestre-1.5.0') or encoding not in ('cp1252', 'utf-8-sig'):
        raise SourceError('unsupported profile or encoding')
    directory = Path(directory)
    if directory.is_symlink() or not directory.is_dir():
        raise SourceError('expected a real tables directory')
    expected = set(BASE if profile == 'windows' else SCHEMAS)
    entries = list(directory.iterdir())
    if {p.name for p in entries} != {t + '.csv' for t in expected}:
        raise SourceError('table set differs from selected profile; provide every documented CSV only')
    raw_files = {}
    total = 0
    for p in sorted(entries):
        if p.is_symlink() or not p.is_file():
            raise SourceError('links and nested directories are unsupported')
        # Bounded read even if a file grows after stat.
        with p.open('rb') as stream:
            raw = stream.read(MAX_FILE_BYTES + 1)
        total += len(raw)
        if len(raw) > MAX_FILE_BYTES or total > MAX_TOTAL_BYTES:
            raise SourceError('source byte limit exceeded')
        raw_files[p.stem] = raw
    digest = hashlib.sha256()
    tables = {}
    count = 0
    for table, raw in raw_files.items():
        digest.update(table.encode() + b'\0' + len(raw).to_bytes(8, 'big') + raw)
        try:
            text = raw.decode(encoding, errors='strict')
            if '\0' in text or any(ord(c) < 32 and c not in '\t\n\r' for c in text):
                raise SourceError(table + ': invalid control character')
            reader = csv.reader(io.StringIO(text, newline=''), strict=True)
            header = next(reader, [])
            expected_header = SCHEMAS[table].split()
            if table == 'TblRegistroPonto' and profile == 'mestre-1.5.0':
                expected_header += ['pon_totalMinutos', 'pon_saldoMinutos']
            if header != expected_header:
                raise SourceError(table + ': unsupported column schema/order')
            rows = []
            ids = set()
            for number, values in enumerate(reader, 2):
                count += 1
                if count > MAX_ROWS:
                    raise SourceError('source row limit exceeded')
                if len(values) != len(header) or any(len(v) > MAX_FIELD for v in values):
                    raise SourceError(f'{table}:{number}: truncated/oversized row')
                row = dict(zip(header, values))
                key = values[0]
                if not re.fullmatch(r'[1-9]\d{0,9}', key) or key in ids:
                    raise SourceError(f'{table}:{number}: invalid/duplicate source identity')
                ids.add(key)
                for column, value in row.items():
                    if not value:
                        continue
                    try:
                        if column in MONEY:
                            money(value)
                        if column in DATES:
                            datetime.strptime(value, '%Y/%m/%d %H:%M:%S')
                    except ValueError:
                        raise SourceError(f'{table}:{number}:{column}: invalid date/decimal') from None
                rows.append(row)
            tables[table] = rows
        except (UnicodeError, csv.Error):
            raise SourceError(table + ': invalid encoding or CSV syntax') from None
    return {'version': VERSION, 'profile': profile, 'encoding': encoding,
            'sourceDigest': digest.hexdigest(), 'tables': tables}


def inspect(source):
    """Source issues are evidence only, never an operational Ready decision."""
    tables = source['tables']
    issues = []
    def issue(table, row, code):
        issues.append({'table': table, 'sourceId': next(iter(row.values())), 'code': code})
    def index(table, key):
        return {r[key]: r for r in tables.get(table, [])}
    accounts = index('TblContas', 'con_id')
    types = index('TblTipoCategoria', 'tipo_id')
    clients = index('TblClientes', 'cli_id')
    categories = index('TblCategoria', 'cat_id')
    products = index('TblProduto', 'produto_id')
    for table, rows in tables.items():
        for r in rows:
            for column in MONEY.intersection(r):
                if r[column] and money(r[column]) != money(r[column]).quantize(Decimal('0.01')):
                    issue(table, r, 'money-precision-review:' + column)
    for r in tables['TblLancamentos']:
        for col, lookup in [('id_conta', accounts), ('id_tipo', types), ('id_categoria', categories), ('id_cliente', clients)]:
            if r[col] and r[col] not in lookup:
                issue('TblLancamentos', r, 'orphan:' + col)
        account = accounts.get(r['id_conta'], {})
        if r['lco_sinal'] not in ('Positivo', 'Negativo'):
            issue('TblLancamentos', r, 'unknown-sign')
        if types.get(r['id_tipo'], {}).get('id_categoria') != r['id_categoria']:
            issue('TblLancamentos', r, 'category-conflict')
        if account.get('con_tipo') == 'Terminal':
            issue('TblLancamentos', r, 'custody-boundary-required')
            if r['lco_sinal'] == 'Positivo':
                issue('TblLancamentos', r, 'positive-terminal-2V-evidence')
            if r['id_tipo'] == '2':
                issue('TblLancamentos', r, 'terminal-fiado-no-inferred-pair')
        if 'teste' in r['lco_descricao'].casefold():
            issue('TblLancamentos', r, 'test-like-description-review')
    for r in tables['TblEstoque']:
        for col, lookup in [('con_id', accounts), ('prod_id', products)]:
            if r[col] not in lookup:
                issue('TblEstoque', r, 'orphan:' + col)
        if r['prod_id'] in ('7', '12', '14'):
            issue('TblEstoque', r, 'fee-report-mapping-required')
    for r in tables.get('TblRelMensal', []):
        issue('TblRelMensal', r, 'explicit-pay-year-required')
    for r in tables.get('TblLogExclusao', []):
        issue('TblLogExclusao', r, 'read-only-deletion-evidence')
    summary = {}
    for item in issues:
        summary[item['code']] = summary.get(item['code'], 0) + 1
    return {'toolVersion': VERSION, 'sourceDigest': source['sourceDigest'],
            'profile': source['profile'], 'encoding': source['encoding'],
            'status': 'evidence-only', 'activationSupported': False,
            'rowCounts': {t: len(rows) for t, rows in tables.items()},
            'issueCounts': dict(sorted(summary.items())), 'issues': issues}


def main():
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('tables')
    parser.add_argument('--profile', required=True, choices=['windows', 'mestre-1.5.0'])
    parser.add_argument('--encoding', required=True, choices=['cp1252', 'utf-8-sig'])
    parser.add_argument('--output', required=True, help='New private directory outside repository and source')
    args = parser.parse_args()
    output = Path(args.output).resolve()
    repo = Path(__file__).resolve().parents[3]
    source_root = Path(args.tables).resolve().parent
    if output == repo or repo in output.parents or output == source_root or source_root in output.parents:
        parser.error('output must be outside repository and source tree')
    try:
        result = inspect(read_source(args.tables, args.profile, args.encoding))
        # Never replace an existing report, source or directory.
        output.mkdir(mode=0o700, parents=False, exist_ok=False)
        with (output / 'source-inspection.json').open('x', encoding='utf-8') as stream:
            json.dump(result, stream, ensure_ascii=False, sort_keys=True, indent=2)
            stream.write('\n')
        print(json.dumps({'status': result['status'], 'tables': len(result['rowCounts']),
                          'issues': len(result['issues'])}))
    except (SourceError, OSError) as error:
        # OSError filenames may contain private path information.
        parser.exit(2, str(error) + '\n' if isinstance(error, SourceError) else 'private output/source IO failed\n')


if __name__ == '__main__':
    main()
