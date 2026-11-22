# One-off job that provisions per-service roles and databases. Build context: repository root.
FROM postgres:17-alpine
COPY deploy/database/postgres/provision.sh /provision.sh
RUN chmod +x /provision.sh
USER postgres
# Runs once, then idles so it doesn't restart-loop. Redeploy to re-run (e.g. after rotating passwords).
CMD ["sh", "-c", "/provision.sh || echo PROVISIONING FAILED; sleep infinity"]
