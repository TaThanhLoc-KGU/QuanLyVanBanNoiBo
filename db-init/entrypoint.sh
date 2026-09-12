#!/bin/bash
# Entrypoint tùy chỉnh cho container SQL Server: khởi động sqlservr như bình thường, nhưng nếu
# database đích chưa tồn tại (volume rỗng — lần chạy đầu tiên), tự động phục hồi từ bản dữ liệu
# thật đã nén kèm theo image (db_thuc_te.sql.gz). Nếu database đã có sẵn (chạy lại container, hoặc
# volume đã có dữ liệu từ trước), bỏ qua bước phục hồi — không đè lên dữ liệu đang chạy.
set -e
set -o pipefail   # QUAN TRỌNG: thiếu dòng này thì "sqlcmd ... | tr ..." vẫn "thành công" (exit 0)
                   # ngay cả khi sqlcmd nối SQL Server thất bại (stdout rỗng) — tr trên input rỗng
                   # vẫn trả 0, khiến DB_EXISTS="" bị hiểu nhầm thành "đã tồn tại" và ÂM THẦM BỎ QUA
                   # bước phục hồi dữ liệu thật ngay từ lần khởi động đầu tiên, không log lỗi nào.

DB_NAME="${DB_NAME:-congvan.vnkgu.edu.vn}"
SQLCMD=/opt/mssql-tools18/bin/sqlcmd
INIT_SQL_GZ=/opt/mssql-init/db_thuc_te.sql.gz
READY_FLAG=/tmp/mssql-restore-done

rm -f "$READY_FLAG"

# Khởi động SQL Server ở nền — giữ nguyên hành vi gốc của image (không thay entrypoint chính).
# Chuyển tiếp SIGTERM/SIGINT (docker stop / compose down) cho đúng tiến trình sqlservr để nó tự
# shutdown sạch — không có trap này, bash (PID 1 trong container) chết trước, kernel SIGKILL cả
# namespace, sqlservr bị giết đột ngột, lần khởi động sau phải crash-recovery không cần thiết.
/opt/mssql/bin/sqlservr &
SQLPID=$!
trap 'kill -TERM "$SQLPID" 2>/dev/null; wait "$SQLPID"' TERM INT

echo "[entrypoint] Cho SQL Server khoi dong..."
SERVER_READY=0
for i in $(seq 1 90); do
    if "$SQLCMD" -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -Q "SELECT 1" >/dev/null 2>&1; then
        echo "[entrypoint] SQL Server da san sang."
        SERVER_READY=1
        break
    fi
    sleep 2
done

if [ "$SERVER_READY" != "1" ]; then
    echo "[entrypoint] LOI: SQL Server khong san sang sau 180s cho. Dung lai, khong doan mo."
    exit 1
fi

DB_EXISTS=$("$SQLCMD" -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -h -1 \
    -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = N'$DB_NAME'" | tr -d '[:space:]')

if [ "$DB_EXISTS" = "0" ]; then
    echo "[entrypoint] Database '$DB_NAME' chua ton tai -> phuc hoi tu du lieu that (lan dau khoi tao)..."
    gunzip -c "$INIT_SQL_GZ" > /tmp/db_thuc_te.sql
    "$SQLCMD" -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -i /tmp/db_thuc_te.sql
    rm -f /tmp/db_thuc_te.sql
    echo "[entrypoint] Da phuc hoi database '$DB_NAME' thanh cong."
elif [ "$DB_EXISTS" = "1" ]; then
    echo "[entrypoint] Database '$DB_NAME' da ton tai -> bo qua buoc phuc hoi (khong ghi de du lieu dang chay)."
else
    echo "[entrypoint] LOI: khong xac dinh duoc database '$DB_NAME' da ton tai hay chua (ket qua: '$DB_EXISTS'). Dung lai thay vi doan mo de tranh de len du lieu that hoac bo qua nham."
    exit 1
fi

# Đánh dấu sẵn sàng CHỈ sau khi phục hồi xong (hoặc xác nhận không cần phục hồi) — healthcheck
# trong docker-compose.yml chờ file này để tránh app kết nối vào lúc DB đang phục hồi dở dang.
touch "$READY_FLAG"

wait "$SQLPID"
