#!/usr/bin/env bash

set -euo pipefail

DOCS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$DOCS_DIR/.." && pwd)"
FIXTURE_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/loto-doc-sync.XXXXXX")"
trap 'rm -rf "$FIXTURE_ROOT"' EXIT

FIXTURE_FILES=(
  "docs/check-loto-doc-sync.sh"
  "docs/loto-specs.md"
  "docs/loto_presentation.html"
  "docs/loto_entity_relationship_diagram.html"
  "design/docs/product.md"
  "design/docs/screens.md"
)
passed=0

reset_fixture() {
  local file
  mkdir -p "$FIXTURE_ROOT/docs" "$FIXTURE_ROOT/design/docs"
  for file in "${FIXTURE_FILES[@]}"; do
    cp "$REPO_ROOT/$file" "$FIXTURE_ROOT/$file"
  done
}

replace_field() {
  local file="$FIXTURE_ROOT/$1" field="$2" value="$3"
  awk -F': ' -v field="$field" -v value="$value" \
    '$1 == field { print field ": " value; next } { print }' "$file" > "$FIXTURE_ROOT/edited"
  mv "$FIXTURE_ROOT/edited" "$file"
}

expect_pass() {
  local label="$1"
  if ! bash "$FIXTURE_ROOT/docs/check-loto-doc-sync.sh" > "$FIXTURE_ROOT/output" 2>&1; then
    echo "FAIL: $label" >&2
    cat "$FIXTURE_ROOT/output" >&2
    exit 1
  fi
  passed=$((passed + 1))
  echo "PASS: $label"
}

expect_failure() {
  local label="$1" diagnostic="$2"
  if bash "$FIXTURE_ROOT/docs/check-loto-doc-sync.sh" > "$FIXTURE_ROOT/output" 2>&1; then
    echo "FAIL: $label unexpectedly passed" >&2
    exit 1
  fi
  if ! grep -Fq "$diagnostic" "$FIXTURE_ROOT/output"; then
    echo "FAIL: $label returned the wrong diagnostic" >&2
    cat "$FIXTURE_ROOT/output" >&2
    exit 1
  fi
  passed=$((passed + 1))
  echo "PASS: $label"
}

reset_fixture
expect_pass "current canonical documents and both design markers"

for file in "design/docs/product.md" "design/docs/screens.md"; do
  reset_fixture
  replace_field "$file" "Synced against loto-specs" "v0"
  expect_failure "$file stale marker" "Design spec-sync mismatch: $file"

  reset_fixture
  sed '/^Synced against loto-specs:/d' "$FIXTURE_ROOT/$file" > "$FIXTURE_ROOT/edited"
  mv "$FIXTURE_ROOT/edited" "$FIXTURE_ROOT/$file"
  expect_failure "$file missing marker" "Expected exactly one 'Synced against loto-specs: vNN' marker in $file"

  reset_fixture
  replace_field "$file" "Synced against loto-specs" ""
  expect_failure "$file empty marker" "Invalid design sync marker in $file"

  reset_fixture
  replace_field "$file" "Synced against loto-specs" "43"
  expect_failure "$file malformed marker" "Invalid design sync marker in $file"

  reset_fixture
  printf '\nSynced against loto-specs: v0\n' >> "$FIXTURE_ROOT/$file"
  expect_failure "$file duplicate marker" "Expected exactly one 'Synced against loto-specs: vNN' marker in $file"

  reset_fixture
  rm "$FIXTURE_ROOT/$file"
  expect_failure "$file missing document" "Missing design sync file: $file"
done

# A server revision bump must fail even if its three-file group is aligned.
reset_fixture
for file in "docs/loto-specs.md" "docs/loto_presentation.html" "docs/loto_entity_relationship_diagram.html"; do
  replace_field "$file" "Spec revision" "v999999"
done
expect_failure "canonical bump requires design re-review" "Design spec-sync mismatch: design/docs/product.md"
for file in "design/docs/product.md" "design/docs/screens.md"; do
  replace_field "$file" "Synced against loto-specs" "v999999"
done
expect_pass "markers reconciled with a future canonical revision"

# Preserve every original metadata gate, including required-field checks.
for file in "docs/loto-specs.md" "docs/loto_presentation.html" "docs/loto_entity_relationship_diagram.html"; do
  for field in "Sync group" "Canonical source" "Coverage" "Spec revision"; do
    reset_fixture
    replace_field "$file" "$field" ""
    expect_failure "$file requires $field" "Missing '$field' in $file"
  done

  reset_fixture
  replace_field "$file" "Sync group" "other-group"
  expect_failure "$file rejects wrong sync group" "Sync group mismatch: $file"
done

for file in "docs/loto_presentation.html" "docs/loto_entity_relationship_diagram.html"; do
  reset_fixture
  replace_field "$file" "Spec revision" "v0"
  expect_failure "$file rejects mismatched revision" "Spec revision mismatch: $file"

  reset_fixture
  replace_field "$file" "Canonical source" "docs/other-spec.md"
  expect_failure "$file rejects wrong canonical source" "Canonical source mismatch: $file"
done

echo "$passed doc-sync regression scenarios passed (disposable fixture only)."
