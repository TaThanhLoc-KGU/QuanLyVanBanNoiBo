# Triển khai bằng Docker trên Rocky Linux + aaPanel

Bộ file này đóng gói ứng dụng CongVan (.NET 10) + SQL Server thành 2 container chạy qua
`docker-compose`. aaPanel chỉ đóng vai trò Nginx reverse-proxy + SSL ở phía trước — bản thân
ứng dụng và database chạy hoàn toàn trong Docker, không đụng tới PHP/MySQL mà aaPanel quản lý.

## 0. Cách nhanh nhất — `deploy-congvan.ps1` (khuyến nghị)

Nếu đang deploy từ máy Windows này và server (Rocky Linux, `100.110.62.27:32156`) đã cài Docker
sẵn (xem mục 1), chỉ cần chạy 1 lệnh — script tự đóng gói toàn bộ project (kèm `db-init/` chứa dữ
liệu thật đã nén), upload qua SSH/SCP, giải nén vào `/home/congvan`, tạo `.env` (nếu server chưa
có) với mật khẩu SA ngẫu nhiên, rồi `docker compose up -d --build`:

```powershell
.\deploy-congvan.ps1
```

Lần chạy ĐẦU TIÊN, container `db` sẽ tự động phục hồi toàn bộ dữ liệu thật từ
`db-init/db_thuc_te.sql.gz` (xem "Cách C" ở mục 3) — có thể mất vài phút do gần 30 nghìn dòng
`INSERT`, healthcheck đã được chỉnh để chờ đúng lúc phục hồi xong mới coi `app` là sẵn sàng khởi
động, không cần can thiệp tay. Các lần deploy SAU chỉ cập nhật code, không đụng lại dữ liệu (DB
đã tồn tại thì bước phục hồi tự bỏ qua).

**⚠️ Lần đầu chạy thật, hãy theo dõi log để chắc chắn bước phục hồi thực sự diễn ra** (không có
cách nào kiểm tra trước việc `entrypoint.sh` tương thích 100% với image gốc mà không chạy thật):
```bash
docker logs -f congvan_db
```
Phải thấy dòng `[entrypoint] Database '...' chua ton tai -> phuc hoi tu du lieu that...` rồi
`...thanh cong.` — nếu thấy dòng `LOI:` bất kỳ, hoặc thấy `da ton tai -> bo qua` ngay lần đầu (lẽ
ra database phải trống), dừng lại kiểm tra thay vì để `app` khởi động vào 1 database rỗng/thiếu.

**⚠️ `db-init/db_thuc_te.sql.gz` chứa dữ liệu THẬT (tên/email nhân viên, nội dung văn bản...)** —
đã được thêm vào `.gitignore`, tuyệt đối không commit lên git hay đẩy lên registry công khai.

Nếu chỉ muốn deploy lại phần cấu hình trên server (không đóng gói/upload lại từ đầu):
```powershell
.\deploy-congvan.ps1 -SkipUpload
```

## 1. Cài Docker trên Rocky Linux (nếu chưa có)

```bash
sudo dnf install -y dnf-plugins-core
sudo dnf config-manager --add-repo https://download.docker.com/linux/rhel/docker-ce.repo
sudo dnf install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
sudo systemctl enable --now docker
```

Nhiều bản aaPanel có sẵn plugin "Docker Manager" trong menu App Store — cài qua đó cũng được,
kết quả tương đương (docker-compose.yml dưới đây chạy được ở cả 2 cách).

## 2. Đưa code lên server + cấu hình

Nếu dùng `deploy-congvan.ps1` ở mục 0, bước này script đã tự làm (đích `/home/congvan`) — bỏ qua.
Nếu đưa code lên bằng cách khác (git pull, copy tay...):

```bash
# copy toàn bộ thư mục project lên server, ví dụ /home/congvan
cd /home/congvan
cp .env.example .env
nano .env   # đặt MSSQL_SA_PASSWORD thật + DB_NAME
```

## 3. Khởi tạo database — chọn 1 trong 3 cách

**⚠️ Lưu ý quan trọng:** thư mục `migrations/` chỉ chứa các thay đổi SCHEMA từng đợt (từ file
`002` trở đi) — KHÔNG có script tạo schema gốc ban đầu (bảng `NhanVien`, `CongVanDen`, `DonVi`...
đã có từ trước khi dự án này bắt đầu dùng migration script). Vì vậy **không thể** dựng database
trống rồi chạy hết migrations để ra được database đầy đủ — bắt buộc phải có bản sao dữ liệu gốc.

### Cách C — Tự động lúc container khởi động lần đầu (khuyến nghị, mặc định khi dùng `deploy-congvan.ps1`)

`docker-compose.yml`'s service `db` build từ `db-init/` (không dùng thẳng image gốc SQL Server
nữa) — image này đóng gói sẵn `db-init/db_thuc_te.sql.gz` (bản dữ liệu thật đã nén, xem mục 0) và
`db-init/entrypoint.sh`. Lúc container khởi động: nếu database `congvan.vnkgu.edu.vn` CHƯA tồn tại
(volume `mssql_data` rỗng — lần đầu), tự giải nén + chạy script phục hồi toàn bộ schema + dữ liệu;
nếu database ĐÃ tồn tại (chạy lại/khởi động lại), bỏ qua bước phục hồi, không ghi đè dữ liệu đang
chạy. Chỉ cần:

```bash
docker compose up -d --build
```

không cần thao tác thêm — mục 4 dưới đây thực chất chính là bước này.

Để cập nhật `db-init/db_thuc_te.sql.gz` bằng một bản dữ liệu thật mới hơn: thay file `.sql` gốc,
nén lại (PowerShell: `[System.IO.Compression.GZipStream]`, hoặc `gzip -k` trên Linux/WSL), rồi
`docker compose up -d --build db` — LƯU Ý: cách này CHỈ có tác dụng nếu volume `mssql_data` đang
trống (lần đầu); muốn nạp lại đè lên dữ liệu đang có phải xóa volume trước (`docker compose down`
rồi `docker volume rm <tên>_mssql_data` — **mất hết dữ liệu hiện có trong container**, chỉ làm khi
chắc chắn muốn thay toàn bộ).

### Cách A — Phục hồi từ backup (.bak) của SQL Server hiện tại (thủ công)

Trên server SQL Server đang chạy thật (Windows), sao lưu database:

```sql
BACKUP DATABASE [congvan.vnkgu.edu.vn]
TO DISK = N'C:\backup\congvan.bak' WITH COMPRESSION;
```

Copy file `.bak` đó vào thư mục `db-backup/` cạnh `docker-compose.yml` trên server Linux
(container SQL Server đã mount sẵn thư mục này vào `/db-backup`), rồi khởi động + phục hồi:

```bash
docker compose up -d db          # chỉ khởi động SQL Server trước, chờ healthy
docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q "RESTORE DATABASE [congvan.vnkgu.edu.vn] FROM DISK = N'/db-backup/congvan.bak' WITH MOVE 'congvan.vnkgu.edu.vn' TO '/var/opt/mssql/data/congvan.mdf', MOVE 'congvan.vnkgu.edu.vn_log' TO '/var/opt/mssql/data/congvan.ldf', REPLACE"
```

(Tên logical file `congvan.vnkgu.edu.vn`/`congvan.vnkgu.edu.vn_log` có thể khác — xem trước bằng
`RESTORE FILELISTONLY FROM DISK = N'/db-backup/congvan.bak'` nếu lệnh trên báo lỗi tên file.)

Sau khi phục hồi xong, áp các migration mà bản backup CHƯA có (kiểm tra bằng cách so bảng nào đã
tồn tại — nếu backup lấy từ bản mới nhất thì bỏ qua bước này):

```bash
for f in migrations/*.sql; do
  echo "→ $f"
  docker compose exec -T db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d "$DB_NAME" -i /dev/stdin < "$f"
done
```

### Cách B — Dùng SQL Server có sẵn ở nơi khác (không chạy SQL Server trong Docker)

Nếu trường đã/sẽ có SQL Server chạy riêng (server khác, hoặc dịch vụ quản lý), bỏ qua container
`db` — xóa/comment phần `db:` trong `docker-compose.yml`, xóa `depends_on` ở service `app`, rồi
sửa thẳng `ConnectionStrings__CongVanConnection` trong `docker-compose.yml` trỏ tới server đó:

```yaml
environment:
  ConnectionStrings__CongVanConnection: "Data Source=<ip-hoặc-hostname-sql-server>,1433;Initial Catalog=congvan.vnkgu.edu.vn;User ID=...;Password=...;TrustServerCertificate=True"
```

## 4. Build và chạy

```bash
docker compose up -d --build
docker compose logs -f app   # xem log khởi động, Ctrl+C để thoát xem log (không dừng container)
```

Ứng dụng nghe ở `127.0.0.1:5100` trên server (không public trực tiếp — cổng chỉ bind localhost
trong `docker-compose.yml`, đúng chủ đích để bắt buộc mọi truy cập phải qua Nginx của aaPanel).

## 4b. Tìm kiếm theo nội dung PDF (OCR) — kiểm tra sau khi deploy

Từ bản 2026-08-23, tìm kiếm trên "Sổ văn bản đến" quét luôn cả nội dung chữ trong file PDF đính
kèm — đọc trực tiếp nếu PDF có sẵn lớp chữ, tự động OCR (Tesseract) nếu là bản scan/ảnh. Phần
render PDF→ảnh (PDFtoImage/PDFium) tự đóng gói sẵn native binary Linux qua NuGet, không cần cài gì
thêm; riêng **Tesseract OCR cần cài qua `apt`** (đã thêm vào `Dockerfile`: `tesseract-ocr` +
`tesseract-ocr-vie`) — đây là phần **chưa từng chạy thử trên Linux thật**, chỉ mới xác nhận hoạt
động đúng trên máy dev Windows. Sau lần deploy đầu tiên có tính năng này, kiểm tra:

```bash
docker compose exec app dotnet --info   # chỉ để chắc container app còn sống
# Upload thử 1 văn bản đến kèm file PDF scan, đợi ~10-30s rồi tìm 1 từ chỉ có trong nội dung file đó
docker compose logs app | grep -i tesseract   # nếu OCR lỗi thường sẽ có exception log ở đây
```

Nếu tìm không ra (nhưng file rõ ràng có chữ đọc được bằng mắt), khả năng cao là
`libtesseract.so`/`libleptonica.so` trên image Linux không khớp phiên bản mà gói NuGet `Tesseract`
mong đợi — đây là vấn đề tương thích thư viện native kinh điển của wrapper này trên Linux, không
phải lỗi logic code. Cách chẩn đoán: `docker compose exec app bash -c "ldconfig -p | grep tesseract"`
để xem tên `.so` thật cài được, so với tên wrapper đang tìm (xem log lỗi). Việc trích xuất chạy nền
sau khi upload xong (không chặn upload) nên **không ảnh hưởng gì tới các tính năng khác** dù OCR có
lỗi trên production — chỉ riêng tìm-theo-nội-dung-PDF sẽ không hoạt động cho tới khi vá.

## 5. Cấu hình aaPanel (Nginx reverse-proxy + SSL)

Trong aaPanel: **Website → Add site** → domain `congvan.vnkgu.edu.vn` (không cần chọn PHP, chọn
"None"/"Pure static" rồi sửa config sau) → sau khi tạo site, vào **Settings → Config file** của
site đó, thêm khối proxy sau (thay cho phần `location /` mặc định):

```nginx
location / {
    proxy_pass http://127.0.0.1:5100;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    client_max_body_size 50m;   # cho phép upload file đính kèm lớn hơn mặc định 1m của Nginx
}
```

Sau đó vào tab **SSL** của site, bật **Let's Encrypt** (miễn phí) để có HTTPS — aaPanel tự gia
hạn. Ứng dụng bên trong Docker vẫn chạy HTTP thuần, SSL được xử lý ở Nginx (đúng mô hình chuẩn).

## 6. File đính kèm & dữ liệu — sao lưu

3 volume Docker giữ dữ liệu sống còn của hệ thống:

| Volume | Nội dung | Mất thì sao |
|---|---|---|
| `mssql_data` | Toàn bộ database | Mất hết dữ liệu văn bản |
| `congvan_files` | File đính kèm đã upload | Văn bản còn nhưng mất file kèm |
| `congvan_dpkeys` | Khóa mã hóa session/antiforgery | Mọi người bị đăng xuất, không mất dữ liệu |

Xem đường dẫn thật trên host: `docker volume inspect congvan_congvan_files` (tên volume có thể
có tiền tố tên thư mục project). Sao lưu định kỳ bằng cách backup thư mục đó, hoặc dùng
`docker run --rm -v congvan_congvan_files:/data -v $(pwd)/backup:/backup alpine tar czf /backup/files_$(date +%F).tar.gz -C /data .`

## 7. Cập nhật lên phiên bản mới

```bash
git pull            # hoặc copy code mới lên
docker compose up -d --build app   # chỉ build lại app, không đụng container db đang chạy
```

Nếu bản cập nhật có thêm migration mới trong `migrations/`, chạy riêng file đó theo cú pháp ở
mục 3 (Cách A) trước khi/sau khi build lại app.
