# ── Build stage ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Cache lớp restore riêng — chỉ chạy lại khi .csproj đổi, không phải mỗi lần sửa code
COPY CongVan.csproj .
RUN dotnet restore CongVan.csproj

COPY . .
RUN dotnet publish CongVan.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Tesseract OCR (đọc chữ từ PDF scan/ảnh) — gói NuGet Tesseract chỉ bundle sẵn native binary cho
# Windows, trên Linux phải cài qua apt để có libtesseract.so mà wrapper .NET P/Invoke vào. PDFtoImage
# (render PDF ra ảnh trước khi OCR) thì KHÔNG cần bước này — native binary Linux (PDFium/Skia) đã tự
# đóng gói sẵn trong publish output qua NuGet, xem CongVan.csproj.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tesseract-ocr tesseract-ocr-vie \
    && rm -rf /var/lib/apt/lists/*

# Chạy bằng user thường (không phải root) khi app thật sự thực thi — chuẩn bảo mật cho container.
# UID GHIM CỨNG =1655 (không để useradd tự chọn nữa) — trước đây "useradd -m congvan" không ép UID
# nên image rebuild có thể đổi UID bất cứ lúc nào, làm quyền sở hữu đã chown trên host (bind mount
# /home/tainguyen) lệch pha với UID mới → lặp lại đúng lỗi "upload 500 Access denied" đã gặp
# 2026-09-06. Ghim cứng UID để chown trên host luôn còn đúng nghĩa qua mọi lần rebuild.
RUN useradd -m -u 1655 congvan \
    && mkdir -p /data/files /app/dp-keys \
    && chown -R congvan:congvan /data /app

# gosu: entrypoint.sh cần chạy vài giây đầu bằng root để tự chown lại /data/files (xem entrypoint.sh
# giải thích lý do phải làm mỗi lần khởi động, không phải 1 lần lúc build) rồi hạ quyền xuống
# "congvan" mới thật sự chạy app — gosu forward tín hiệu (SIGTERM) đúng cách, không như su/sudo.
RUN apt-get update \
    && apt-get install -y --no-install-recommends gosu \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY entrypoint.sh /entrypoint.sh
RUN chown -R congvan:congvan /app && chmod +x /entrypoint.sh

# KHÔNG "USER congvan" ở đây — container phải khởi động bằng root để entrypoint.sh tự chown lại
# /data/files được (chown cần quyền root), sau đó entrypoint.sh mới gosu hạ quyền xuống "congvan".
# Đây chính là cơ chế "server tự cấp quyền cho tháng mới/năm mới" — chạy lại ở MỌI LẦN container
# khởi động (deploy lại, server reboot...), không cần SSH vào chown tay như trước nữa.

# FileStorage__RootPath và ConnectionStrings__CongVanConnection nên được override qua
# docker-compose.yml (biến môi trường) thay vì sửa file này — xem docker-compose.yml.
ENV ASPNETCORE_ENVIRONMENT=Production \
    FileStorage__RootPath=/data/files

EXPOSE 5100

# appsettings.json hard-codes "Urls": "http://localhost:5100" (đúng ý cho lúc chạy tay ngoài
# Docker) — giá trị này thắng cả biến môi trường ASPNETCORE_URLS ở trên trong pipeline cấu hình
# của ASP.NET Core, nên trong container sẽ chỉ nghe được ở loopback bên trong container (không ai
# từ ngoài container gọi vào được dù đã map cổng). entrypoint.sh truyền --urls trực tiếp cho dotnet,
# có độ ưu tiên cao nhất, ép Kestrel nghe mọi interface bên trong container.
ENTRYPOINT ["/entrypoint.sh"]
