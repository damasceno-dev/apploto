#!/usr/bin/env bash

set -euo pipefail

SERVER_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EXPECTED_SYNC_GROUP="loto-backend-docs"
EXPECTED_CANONICAL_SOURCE="server/docs/loto-specs.md"

SPEC_FILE="$SERVER_DIR/docs/loto-specs.md"
PRESENTATION_FILE="$SERVER_DIR/docs/loto_presentation.html"
ERD_FILE="$SERVER_DIR/docs/loto_entity_relationship_diagram.html"

FILES=("$SPEC_FILE" "$PRESENTATION_FILE" "$ERD_FILE")
HTML_FILES=("$PRESENTATION_FILE" "$ERD_FILE")

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
    echo "Missing '$field' in ${file#$SERVER_DIR/}" >&2
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
  echo "Sync group mismatch: ${SPEC_FILE#$SERVER_DIR/} must use '$EXPECTED_SYNC_GROUP' but has '$spec_group'." >&2
  exit 1
fi

if [[ "$presentation_group" != "$spec_group" ]]; then
  echo "Sync group mismatch: ${PRESENTATION_FILE#$SERVER_DIR/} has '$presentation_group' but ${SPEC_FILE#$SERVER_DIR/} has '$spec_group'." >&2
  exit 1
fi

if [[ "$erd_group" != "$spec_group" ]]; then
  echo "Sync group mismatch: ${ERD_FILE#$SERVER_DIR/} has '$erd_group' but ${SPEC_FILE#$SERVER_DIR/} has '$spec_group'." >&2
  exit 1
fi

spec_revision="$(extract_field "$SPEC_FILE" "Spec revision")"

for file in "${HTML_FILES[@]}"; do
  html_revision="$(extract_field "$file" "Spec revision")"

  if [[ "$html_revision" != "$spec_revision" ]]; then
    echo "Spec revision mismatch: ${file#$SERVER_DIR/} has '$html_revision' but ${SPEC_FILE#$SERVER_DIR/} has '$spec_revision'." >&2
    exit 1
  fi
done

for file in "${HTML_FILES[@]}"; do
  canonical_source="$(extract_field "$file" "Canonical source")"

  if [[ "$canonical_source" != "$EXPECTED_CANONICAL_SOURCE" ]]; then
    echo "Canonical source mismatch: ${file#$SERVER_DIR/} must use '$EXPECTED_CANONICAL_SOURCE' but has '$canonical_source'." >&2
    exit 1
  fi
done

echo "Lotero doc sync metadata is aligned for ${spec_revision}."
