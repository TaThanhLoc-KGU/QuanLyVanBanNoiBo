# Trỏ tên miền qlvb.vnkgu.edu.vn vào app (app đang chạy Docker)

App vẫn giữ nguyên: container `congvan_app` nghe cổng `5100` trên máy chủ. aaPanel/nginx đứng trước
làm **reverse proxy**: nhận `qlvb.vnkgu.edu.vn` (80/443) rồi chuyển vào `http://127.0.0.1:5100`.
Không cần đổi Docker, không cần build lại image cho việc này.

> Code đã cập nhật (deploy 2026-08-27) để chạy đúng sau reverse proxy: đọc `X-Forwarded-Proto` /
> `X-Forwarded-For` (chỉ tin khi request đến từ dải IP nội bộ 10/172.16/192.168) — nhờ đó cookie
> đăng nhập / antiforgery hoạt động đúng khi vào bằng HTTPS qua tên miền.

## 0. Điều kiện tiên quyết (người quản trị mạng/DNS của trường)

- Bản ghi **A**: `qlvb.vnkgu.edu.vn` → IP máy chủ (IP mà người dùng truy cập được — public hoặc IP
  nội bộ trường). Kiểm tra: `nslookup qlvb.vnkgu.edu.vn` phải ra đúng IP đó.
- Máy chủ mở **cổng 80 và 443** (aaPanel → An toàn/Security → thêm rule). Cổng 5100 giữ nguyên.

## 1. Tạo site trong aaPanel

**Website → Add site**
- Domain: `qlvb.vnkgu.edu.vn`
- PHP version: chọn **Static / Pure static** (app không dùng PHP)
- Không cần tạo Database, không cần FTP.

## 2. Cấu hình Reverse Proxy

Vào site vừa tạo → tab **Reverse Proxy → Add reverse proxy**
- Proxy name: `congvan`
- Target URL / 目标URL: `http://127.0.0.1:5100`
- Send Domain / 发送域名: `$host`
- Cache: **TẮT**

Sau khi lưu, mở **Config file** của reverse proxy (hoặc của site) và đảm bảo có đủ các dòng sau
trong khối `location`:

```nginx
proxy_set_header Host              $host;
proxy_set_header X-Real-IP         $remote_addr;
proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto $scheme;      # <-- BẮT BUỘC, thiếu là login lỗi khi dùng HTTPS
proxy_http_version 1.1;
client_max_body_size 60m;                        # <-- cho phép tải file đính kèm tới 50MB
proxy_read_timeout 300s;                         # <-- OCR/PDF xử lý nền có thể lâu
```

(`aaPanel` mặc định đã thêm 3 dòng `X-*` đầu; thường thiếu `X-Forwarded-Proto`, `client_max_body_size`,
`proxy_read_timeout` — thêm tay.)

Reload nginx: nút **Reload** trong aaPanel, hoặc `nginx -s reload`.

## 3. Bật HTTPS

Site → tab **SSL** → **Let's Encrypt** → chọn domain `qlvb.vnkgu.edu.vn` → **Apply**.
Xong thì bật **Force HTTPS** (chuyển hướng http → https).

(Nếu trường có sẵn chứng chỉ riêng thì dán vào ô "Other Certificate" thay vì Let's Encrypt.)

## 4. Kiểm tra

- `https://qlvb.vnkgu.edu.vn` → ra trang đăng nhập.
- Đăng nhập thử — vào được, không bị đá ra lại (nếu bị: thiếu `X-Forwarded-Proto $scheme`).
- Tải 1 file đính kèm lớn — không bị lỗi 413 (nếu bị: thiếu `client_max_body_size`).
- API công khai: `https://qlvb.vnkgu.edu.vn/api/v1/vanban-cong-khai`

## 5. Truy cập cũ vẫn còn

Cổng `5100` vẫn bind `0.0.0.0` nên `http://100.110.62.27:5100` (Tailscale) và `http://<IP-LAN>:5100`
vẫn dùng được song song. Muốn CHỈ cho vào qua tên miền: sửa `docker-compose.yml` dòng ports thành
`"127.0.0.1:5100:5100"` rồi `docker compose up -d` — nhưng sẽ mất truy cập trực tiếp qua IP.
