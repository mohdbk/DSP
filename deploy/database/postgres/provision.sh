#!/bin/sh
# Creates one login role and its own database(s) per service. Idempotent: safe to re-run,
# and re-running rotates each role's password to the value in the environment.
#
# Each role owns only its databases; PUBLIC can't connect to them, so a service can't read another's data.
# Required env: PGHOST PGPORT ADMIN_USER ADMIN_PASSWORD and <SERVICE>_DB_PASSWORD for each service.
set -eu

export PGPASSWORD="$ADMIN_PASSWORD"
admin() { psql -v ON_ERROR_STOP=1 -q -h "$PGHOST" -p "$PGPORT" -U "$ADMIN_USER" -d postgres "$@"; }

until admin -c 'select 1' >/dev/null 2>&1; do echo "waiting for postgres..."; sleep 3; done

provision() {
  role="$1"; password="$2"; shift 2
  admin <<SQL
DO \$\$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '$role') THEN
    CREATE ROLE $role LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD '$password';
  ELSE
    ALTER ROLE $role WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD '$password';
  END IF;
END
\$\$;
SQL
  for db in "$@"; do
    if [ "$(admin -tAc "SELECT 1 FROM pg_database WHERE datname = '$db'")" != "1" ]; then
      admin -c "CREATE DATABASE $db OWNER $role"
    fi
    admin -c "ALTER DATABASE $db OWNER TO $role" \
          -c "REVOKE ALL ON DATABASE $db FROM PUBLIC" \
          -c "GRANT CONNECT, TEMPORARY ON DATABASE $db TO $role"
    echo "provisioned database $db for $role"
  done
}

provision dsp_identity      "$IDENTITY_DB_PASSWORD"      identity
provision dsp_eservices     "$ESERVICES_DB_PASSWORD"     eservices workflows
provision dsp_payments      "$PAYMENTS_DB_PASSWORD"      payments
provision dsp_documents     "$DOCUMENTS_DB_PASSWORD"     documents
provision dsp_notifications "$NOTIFICATIONS_DB_PASSWORD" notifications
provision dsp_platform      "$PLATFORM_DB_PASSWORD"      platform

echo "PROVISIONING COMPLETE"
