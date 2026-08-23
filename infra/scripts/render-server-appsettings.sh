#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SERVER_API_DIR="$ROOT_DIR/server/server.API"
ENV_FILE="${1:-.env}"

if [[ -f "$ENV_FILE" ]]; then
  # shellcheck disable=SC2046
  export $(grep -v '^#' "$ENV_FILE" | grep -v '^$' | xargs)
fi

ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-}"

case "$ENVIRONMENT" in
  Staging|Production)
    ;;
  *)
    echo "ASPNETCORE_ENVIRONMENT must be Staging or Production." >&2
    exit 1
    ;;
esac

required_vars=(
  SERVER_DB_HOST
  SERVER_DB_NAME
  SERVER_DB_USERNAME
  SERVER_DB_PASSWORD
  SERVER_TOKEN_SIGNING_KEY
  SERVER_TOKEN_EXPIRATION_MINUTES
)

for var_name in "${required_vars[@]}"; do
  if [[ -z "${!var_name:-}" ]]; then
    echo "Missing required variable: ${var_name}" >&2
    exit 1
  fi
done

db_port="${SERVER_DB_PORT:-5432}"
idempotency_coordination_lock_timeout_seconds="${SERVER_IDEMPOTENCY_COORDINATION_LOCK_TIMEOUT_SECONDS:-5}"
idempotency_cleanup_interval_hours="${SERVER_IDEMPOTENCY_CLEANUP_INTERVAL_HOURS:-24}"
idempotency_cleanup_batch_size="${SERVER_IDEMPOTENCY_CLEANUP_BATCH_SIZE:-500}"
target_file="$SERVER_API_DIR/appsettings.${ENVIRONMENT}.json"

cat > "$target_file" <<EOF
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=${SERVER_DB_HOST};Port=${db_port};Database=${SERVER_DB_NAME};Username=${SERVER_DB_USERNAME};Password=${SERVER_DB_PASSWORD}"
  },
  "Token": {
    "SigningKey": "${SERVER_TOKEN_SIGNING_KEY}",
    "ExpirationTimeInMinutes": ${SERVER_TOKEN_EXPIRATION_MINUTES}
  },
  "IdempotencyRequestCoordination": {
    "LockTimeoutSeconds": ${idempotency_coordination_lock_timeout_seconds}
  },
  "IdempotencyRequestCleanup": {
    "SweepIntervalHours": ${idempotency_cleanup_interval_hours},
    "BatchSize": ${idempotency_cleanup_batch_size}
  }
}
EOF

echo "Generated ${target_file}"
