import csv
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import source


class SourceTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for table in source.BASE:
            self.write(table, [])

    def write(self, table, rows):
        with (self.root / (table + '.csv')).open('w', encoding='cp1252', newline='') as f:
            writer = csv.writer(f)
            writer.writerow(source.SCHEMAS[table].split())
            writer.writerows(rows)

    def read(self):
        return source.read_source(self.root, 'windows', 'cp1252')

    def test_determinism_and_byte_identity(self):
        a = self.read()
        self.assertEqual(a, self.read())
        self.write('TblProduto', [['1', 'Sintético']])
        self.assertNotEqual(a['sourceDigest'], self.read()['sourceDigest'])

    def test_duplicate_identity(self):
        self.write('TblProduto', [['1', 'A'], ['1', 'B']])
        with self.assertRaisesRegex(source.SourceError, 'duplicate'):
            self.read()

    def test_bounds(self):
        with patch.object(source, 'MAX_FILE_BYTES', 1):
            with self.assertRaisesRegex(source.SourceError, 'byte limit'):
                self.read()
        self.write('TblProduto', [['1', 'A']])
        with patch.object(source, 'MAX_ROWS', 0):
            with self.assertRaisesRegex(source.SourceError, 'row limit'):
                self.read()

    def test_nested_or_unknown_files(self):
        (self.root / 'macro.vba').write_text('never executed')
        with self.assertRaisesRegex(source.SourceError, 'table set'):
            self.read()

    def test_symlink(self):
        p = self.root / 'TblProduto.csv'
        p.unlink()
        p.symlink_to(self.root / 'TblContas.csv')
        with self.assertRaisesRegex(source.SourceError, 'links'):
            self.read()

    def test_truncated_csv(self):
        p = self.root / 'TblProduto.csv'
        p.write_text('produto_id,produto_nome\n1,"unfinished', encoding='cp1252')
        with self.assertRaisesRegex(source.SourceError, 'CSV syntax'):
            self.read()

    def test_schema_drift(self):
        (self.root / 'TblProduto.csv').write_text('produto_id,unknown\n')
        with self.assertRaisesRegex(source.SourceError, 'schema'):
            self.read()

    def test_decimal_and_calendar(self):
        for value in ['NaN', '1e3', '1,20', '1.1234567890123456789', '9999999999999', 'Infinity']:
            with self.assertRaises(source.SourceError):
                source.money(value)
        self.write('TblEstoque', [['1', '2026/02/30 00:00:00', '1', '2.00', '1', '']])
        with self.assertRaisesRegex(source.SourceError, 'invalid date/decimal'):
            self.read()

    def test_high_precision_is_preserved_and_flagged(self):
        self.write('TblEstoque', [['1', '2026/01/01 00:00:00', '1', '2.000000000001', '1', '']])
        data = self.read()
        self.assertEqual(data['tables']['TblEstoque'][0]['est_valor'], '2.000000000001')
        self.assertEqual(source.inspect(data)['issueCounts']['money-precision-review:est_valor'], 1)

    def test_issues_never_expose_descriptions_or_authorize(self):
        self.write('TblContas', [['1', 'Synthetic box', '', '', 'Terminal']])
        self.write('TblCategoria', [['1', 'Synthetic', 'Positivo']])
        self.write('TblTipoCategoria', [['2', 'Synthetic', '2']])
        self.write('TblLancamentos', [['1', '2026/01/01 00:00:00', '5.00', '1', '2',
                   'private teste name', '', '', '', '1', 'Positivo', '', '', '', '', '']])
        result = source.inspect(self.read())
        self.assertFalse(result['activationSupported'])
        self.assertNotIn('private teste name', str(result))
        self.assertEqual(set(result['issueCounts']), {'custody-boundary-required',
                         'positive-terminal-2V-evidence', 'terminal-fiado-no-inferred-pair',
                         'category-conflict', 'test-like-description-review'})


if __name__ == '__main__':
    unittest.main()
