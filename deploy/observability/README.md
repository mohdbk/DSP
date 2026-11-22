# Observability

Every service, the gateway and the workflow engine emit **logs, traces and metrics** through OpenTelemetry
(`src/BuildingBlocks/Dsp.BuildingBlocks/Web/HostingExtensions.cs`). Export is switched on by setting:

| Variable | Example |
|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://grafana.railway.internal:4317` |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` |

Each request carries an `X-Correlation-Id` (set by the gateway or the first service) and a W3C trace context, so one
citizen application can be followed from the portal through the gateway, E-Services, Elsa, Payments and Documents.

## POC (Railway)

One `grafana/otel-lgtm` container provides the OpenTelemetry Collector, Loki (logs), Tempo (traces),
Prometheus (metrics) and Grafana. It is for development and demos only: data is not persisted across restarts.

In Grafana, use **Explore**:

- **Logs (Loki):** `{service_name="dsp-eservices"}`, or search by correlation ID.
- **Traces (Tempo):** search by service, e.g. `dsp-gateway`, and open a trace to see every hop.
- **Metrics (Prometheus):** `http_server_request_duration_seconds` by `service_name`.

## FedNet (production)

Run the components separately, with persistent storage and retention agreed with the security team:

```
services ──OTLP──▶ OpenTelemetry Collector ──▶ Loki       (logs)
                                         ├──▶ Tempo      (traces)
                                         └──▶ Prometheus (metrics) ──▶ Grafana (SSO, read-only roles)
                                         └──▶ SIEM       (security events)
```

- The collector removes personal data (Emirates ID, mobile, email) from log attributes before export.
- Business audit (who approved what) stays in the Audit service's database, not in technical logs.
- Alert rules: 5xx rate per service, p95 latency, outbox backlog (`OutboxMessages` with `PublishedAt IS NULL`),
  dead-letter queue depth (`dsp.*.dead-letter`), review tasks past SLA.
