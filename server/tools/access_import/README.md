# Access source inspection (M10 foundation)

This offline, read-only tool accepts the `tables/` directory from the Windows
export (`windows`, CP1252) or inspected Mestre BE/FE 1.5.0 export
(`mestre-1.5.0`, UTF-8). These are explicit schema profiles, not detection or a
claim that arbitrary Access versions work. It never opens Access, executes VBA,
extracts archives, follows table symlinks, or writes operational data.

Run from the repository root, choosing a **new private directory outside the
repository and export tree** (its parent must already exist):

```sh
python3 server/tools/access_import/source.py /external/export/tables \
  --profile mestre-1.5.0 --encoding utf-8-sig \
  --output /private/tmp/loto-source-inspection-unique
python3 -m unittest discover -s server/tools/access_import -v
```

The selected profile requires exactly its documented table set and column order;
unknown, missing or nested files refuse. Limits: 16 MiB/file, 64 MiB total,
100,000 total records, 16,384 characters/field (CSV parser also caps field size).
Dates use the exporter’s `yyyy/MM/dd HH:mm:ss`. Decimal notation is invariant,
without exponents or group separators, up to 12 integer and 18 fractional digits.
Extra monetary precision is retained verbatim and flagged for review, never
rounded. Duplicate source identities and malformed CSV refuse the whole read.
An encoding is always explicit; no replacement characters are introduced.

The report contains byte-level source digest, tool/profile/encoding, table
counts, issue codes and source identities. It omits customer names, descriptions,
credentials and financial values; **source identities remain private evidence**.
Console output contains only counts/status. Reports are never overwritten.
The digest covers sorted table names, byte lengths and original bytes; changing
line endings or encoding changes source identity even with equivalent CSV cells.

This is source inspection, **not D4 financial replay or an API import**. Its
`evidence-only` status always has `activationSupported: false`. It flags source
precision, selected orphans/category conflicts, positive Terminal rows,
Terminal fiado, test-like descriptions, fee/report mappings, unknown pay year,
and read-only deletion evidence. Issue counts are neither exhaustive data-quality
acceptance nor proof of readiness. It does not resolve clients/custody from names,
calculate variance, infer pay years, authenticate operators or suggest exclusions.
Persistent mapping APIs, target snapshots, per-box/day replay, reviewed activation,
opening obligations and the protected Web desk are implemented separately. See
[the local go-live guide](../../docs/go-live.md) for their exact supported scope,
source→target map, launch settings and private D4 replay command. This Python
inspection report remains evidence-only and cannot activate data.
