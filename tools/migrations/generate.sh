#!/usr/bin/env bash
# Generates EF Core migrations for every service and provider, plus idempotent SQL scripts for review.
#
#   Locally (with the .NET SDK):  MIGRATION_NAME=AddFoo tools/migrations/generate.sh
#   On a build host without a checkout to write back to: set EMIT_ARCHIVE=1 and the result is printed
#   to stdout as a base64 tar archive (lines "MIG <n> <data>") to be decoded and committed.
set -euo pipefail

NAME="${MIGRATION_NAME:-InitialCreate}"
SERVICES=(Identity EServices Payments Documents Notifications Platform)
PROVIDERS=(SqlServer Postgres)
export PATH="$PATH:$HOME/.dotnet/tools"

mkdir -p deploy/database/scripts/SqlServer deploy/database/scripts/Postgres

for svc in "${SERVICES[@]}"; do
  proj="src/Services/$svc/Dsp.$svc.Infrastructure"
  lower="$(echo "$svc" | tr '[:upper:]' '[:lower:]')"
  echo "== $svc"
  dotnet build "$proj" -v q -nologo

  for prov in "${PROVIDERS[@]}"; do
    dotnet ef migrations add "$NAME" --no-build \
      --project "$proj" --startup-project "$proj" \
      --context "${svc}${prov}DbContext" \
      --output-dir "Persistence/Migrations/$prov"
  done

  dotnet build "$proj" -v q -nologo
  for prov in "${PROVIDERS[@]}"; do
    dotnet ef migrations script --idempotent --no-build \
      --project "$proj" --startup-project "$proj" \
      --context "${svc}${prov}DbContext" \
      --output "deploy/database/scripts/$prov/$lower.sql"
  done
done

if [[ "${EMIT_ARCHIVE:-0}" == "1" ]]; then
  # Everything dotnet ef wrote: migrations, designers and model snapshots (wherever EF put them), plus scripts.
  git_free_list=$( { find src -path '*/Persistence/Migrations/*' -type f; find src -name '*ModelSnapshot.cs'; find deploy/database/scripts -type f; } | sort -u)
  echo "$git_free_list" | sed 's/^/FILE /'
  tar cf /tmp/migrations.tgz $git_free_list   # uncompressed: large enough to be saved whole by log tooling
  base64 -w 3000 /tmp/migrations.tgz > /tmp/migrations.b64
  echo "MIGSTART $(sha256sum /tmp/migrations.tgz | cut -d' ' -f1) $(wc -l < /tmp/migrations.b64)"
  n=0
  while IFS= read -r line; do
    printf 'MIG %04d %s\n' "$n" "$line"
    n=$((n + 1))
  done < /tmp/migrations.b64
  echo "MIGEND"
fi
