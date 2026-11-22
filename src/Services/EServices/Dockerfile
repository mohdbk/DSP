# Build context: repository root
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Services/EServices/Dsp.EServices.Api/Dsp.EServices.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV PORT=8080
EXPOSE 8080
COPY --from=build /app/publish .
# Elsa keeps its startup lock files in App_Data; the non-root user must own it.
RUN mkdir -p /app/App_Data && chown -R $APP_UID /app/App_Data
USER $APP_UID
ENTRYPOINT ["dotnet", "Dsp.EServices.Api.dll"]
