#!/usr/bin/env bash

set -euo pipefail

DOCS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$DOCS_DIR/.." && pwd)"
EXPECTED_SYNC_GROUP="loto-backend-docs"
EXPECTED_CANONICAL_SOURCE="docs/loto-specs.md"

SPEC_FILE="$DOCS_DIR/loto-specs.md"
PRESENTATION_FILE="$DOCS_DIR/loto_presentation.html"
ERD_FILE="$DOCS_DIR/loto_entity_relationship_diagram.html"

FILES=("$SPEC_FILE" "$PRESENTATION_FILE" "$ERD_FILE")
HTML_FILES=("$PRESENTATION_FILE" "$ERD_FILE")
DESIGN_FILES=("$REPO_ROOT/design/docs/product.md" "$REPO_ROOT/design/docs/screens.md")

extract_field() {
  local file="$1"
  local field="$2"

  awk -F': ' -v field="$field" '$1 == field { print substr($0, length(field) + 3); exit }' "$file"
}

require_field() {
  local file="$1"
  local field="$2"
  local value

  value="$(extract_field "$file" "$field")"

  if [[ -z "$value" ]]; then
    echo "Missing '$field' in ${file#$REPO_ROOT/}" >&2
    return 1
  fi
}

for file in "${FILES[@]}"; do
  require_field "$file" "Sync group"
  require_field "$file" "Canonical source"
  require_field "$file" "Coverage"
  require_field "$file" "Spec revision"
done

spec_group="$(extract_field "$SPEC_FILE" "Sync group")"
presentation_group="$(extract_field "$PRESENTATION_FILE" "Sync group")"
erd_group="$(extract_field "$ERD_FILE" "Sync group")"

if [[ "$spec_group" != "$EXPECTED_SYNC_GROUP" ]]; then
  echo "Sync group mismatch: ${SPEC_FILE#$REPO_ROOT/} must use '$EXPECTED_SYNC_GROUP' but has '$spec_group'." >&2
  exit 1
fi

if [[ "$presentation_group" != "$spec_group" ]]; then
  echo "Sync group mismatch: ${PRESENTATION_FILE#$REPO_ROOT/} has '$presentation_group' but ${SPEC_FILE#$REPO_ROOT/} has '$spec_group'." >&2
  exit 1
fi

if [[ "$erd_group" != "$spec_group" ]]; then
  echo "Sync group mismatch: ${ERD_FILE#$REPO_ROOT/} has '$erd_group' but ${SPEC_FILE#$REPO_ROOT/} has '$spec_group'." >&2
  exit 1
fi

spec_revision="$(extract_field "$SPEC_FILE" "Spec revision")"

for file in "${HTML_FILES[@]}"; do
  html_revision="$(extract_field "$file" "Spec revision")"

  if [[ "$html_revision" != "$spec_revision" ]]; then
    echo "Spec revision mismatch: ${file#$REPO_ROOT/} has '$html_revision' but ${SPEC_FILE#$REPO_ROOT/} has '$spec_revision'." >&2
    exit 1
  fi
done

for file in "${HTML_FILES[@]}"; do
  canonical_source="$(extract_field "$file" "Canonical source")"

  if [[ "$canonical_source" != "$EXPECTED_CANONICAL_SOURCE" ]]; then
    echo "Canonical source mismatch: ${file#$REPO_ROOT/} must use '$EXPECTED_CANONICAL_SOURCE' but has '$canonical_source'." >&2
    exit 1
  fi
done

for file in "${DESIGN_FILES[@]}"; do
  if [[ ! -f "$file" ]]; then
    echo "Missing design sync file: ${file#$REPO_ROOT/}." >&2
    exit 1
  fi

  marker_count="$(awk '/^Synced against loto-specs:/ { count++ } END { print count + 0 }' "$file")"
  if [[ "$marker_count" -ne 1 ]]; then
    echo "Expected exactly one 'Synced against loto-specs: vNN' marker in ${file#$REPO_ROOT/}; found $marker_count." >&2
    exit 1
  fi

  design_revision="$(extract_field "$file" "Synced against loto-specs")"
  if [[ ! "$design_revision" =~ ^v[0-9]+$ ]]; then
    echo "Invalid design sync marker in ${file#$REPO_ROOT/}: use 'Synced against loto-specs: vNN'." >&2
    exit 1
  fi

  if [[ "$design_revision" != "$spec_revision" ]]; then
    echo "Design spec-sync mismatch: ${file#$REPO_ROOT/} was reviewed against $design_revision; docs/loto-specs.md is $spec_revision. Reconcile the catalog and gap ownership before updating the marker." >&2
    exit 1
  fi
done

echo "Lotero doc sync metadata is aligned for ${spec_revision}."
echo "Design catalog sync markers are aligned for ${spec_revision}; pending feature gates still apply."
