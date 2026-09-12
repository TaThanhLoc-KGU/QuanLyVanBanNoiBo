# Image SQL Server tùy chỉnh: đóng gói sẵn dữ liệu thật đã nén + script tự phục hồi lúc khởi
# động lần đầu — xem entrypoint.sh và DOCKER.md ("Cách C — tự động, khuyến nghị").
FROM mcr.microsoft.com/mssql/server:2022-latest

USER root
COPY db_thuc_te.sql.gz /opt/mssql-init/db_thuc_te.sql.gz
COPY entrypoint.sh /opt/mssql-init/entrypoint.sh
RUN chmod +x /opt/mssql-init/entrypoint.sh && chown -R mssql:mssql /opt/mssql-init

USER mssql
ENTRYPOINT ["/opt/mssql-init/entrypoint.sh"]
