#!/usr/bin/env bash
# Resets a DISPOSABLE development or staging database and re-applies every migration (M9.6 Phase 2.8).
#
# The M9.6 session/custody migration quarantines existing account/day closes and cashbox rows as Unresolved
# history: it never invents custodians, sequences or intervals, and a cashbox with unresolved history cannot start a
# session. Where that data is disposable fixture data, the owner may reset instead of mapping it. This script DROPS the
# named database. It refuses production names and asks for the database name to be typed back.
#
# Usage:
#   server/scripts/reset-disposable-database.sh <connection-string-without-database> <database-name>
# Example (local Docker Compose database):
#   server/scripts/reset-disposable-database.sh "Host=localhost;Username=postgres;Password=..." loto_dev_local
#
# Afterwards create branches through the API (or operated onboarding); each new cashbox gets its physical terminal and
# starts its first session through Conferir abertura e iniciar (or an opening count first).
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "usage: $0 <connection-string-without-database> <database-name>" >&2
  exit 64
fi

connection="$1"
database="$2"

case "$database" in
  loto_dev_local|loto_staging) ;;
  *)
    echo "Refusing to reset '$database': only loto_dev_local or loto_staging are disposable." >&2
    exit 65
    ;;
esac

read -r -p "This DROPS database '$database' and all its data. Type the database name to continue: " confirmation
if [[ "$confirmation" != "$database" ]]; then
  echo "Confirmation did not match; nothing was changed." >&2
  exit 66
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
server_dir="$(cd "$script_dir/.." && pwd)"

ConnectionStrings__DefaultConnection="${connection};Database=${database}" \
  dotnet ef database drop --force \
    --project "$server_dir/server.Infrastructure" \
    --startup-project "$server_dir/server.API"

ConnectionStrings__DefaultConnection="${connection};Database=${database}" \
  dotnet ef database update \
    --project "$server_dir/server.Infrastructure" \
    --startup-project "$server_dir/server.API"

echo "Database '$database' was recreated at the latest migration."
