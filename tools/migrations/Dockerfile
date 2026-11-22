# Migration generator for hosts without a local .NET SDK. Build context: repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0
SHELL ["/bin/bash", "-o", "pipefail", "-c"]
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 EMIT_ARCHIVE=1
RUN dotnet tool install --global dotnet-ef --version "10.*"
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
COPY tools/migrations/ tools/migrations/
# Runs once; keeps the container alive afterwards so the output stays in the logs and it doesn't restart-loop.
CMD ["bash", "-c", "bash tools/migrations/generate.sh || echo GENFAILED; sleep infinity"]
