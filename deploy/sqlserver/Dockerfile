# SQL Server for the POC environment (Railway). In FedNet, use the ministry's managed SQL Server instead.
FROM mcr.microsoft.com/mssql/server:2025-latest
USER root
COPY deploy/sqlserver/start.sh /usr/local/bin/start-sqlserver.sh
RUN chmod +x /usr/local/bin/start-sqlserver.sh
ENV ACCEPT_EULA=Y MSSQL_PID=Developer
EXPOSE 1433
ENTRYPOINT ["/usr/local/bin/start-sqlserver.sh"]
