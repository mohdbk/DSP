# Builds the whole solution and runs every test. A failing test fails the build.
# Build context: repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0
SHELL ["/bin/bash", "-o", "pipefail", "-c"]
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props Dsp.sln ./
COPY src/ src/
COPY tests/ tests/
RUN dotnet build Dsp.sln -c Release -warnaserror:CS8602,CS8604 2>&1 | tail -n 30
RUN dotnet test Dsp.sln -c Release --no-build --logger "console;verbosity=normal" 2>&1 | tee /test-output.txt | grep -E "Passed|Failed|Skipped|error|Total tests|passed|failed" | tail -n 200
CMD ["bash", "-c", "grep -E 'Failed |Total tests|Passed!|Failed!' /test-output.txt; sleep infinity"]
