#!/bin/bash
# SQL Server on a shared host (Railway): it detects the host's CPUs and memory rather than the
# container's limits, which can crash it at startup. Pin it to a few CPUs, cap memory, use small dumps.
set -e

mkdir -p /var/opt/mssql
rm -rf /var/opt/mssql/log/core.* /var/opt/mssql/log/*.tbz2 2>/dev/null || true

cat > /var/opt/mssql/mssql.conf <<EOF
[memory]
memorylimitmb = ${MSSQL_MEMORY_LIMIT_MB:-3072}
[coredump]
captureminiandfull = false
coredumptype = mini
EOF

ulimit -s 16384 2>/dev/null || true

FIRST_CPU=$(awk '/Cpus_allowed_list/{print $2}' /proc/self/status | cut -d, -f1 | cut -d- -f1)
LAST_CPU=$((FIRST_CPU + ${MSSQL_CPU_COUNT:-4} - 1))
echo "Starting SQL Server on CPUs ${FIRST_CPU}-${LAST_CPU} with ${MSSQL_MEMORY_LIMIT_MB:-3072} MB"

exec taskset -c "${FIRST_CPU}-${LAST_CPU}" /opt/mssql/bin/sqlservr -T8015
