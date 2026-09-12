#!/bin/sh
# Chạy khi container khởi động, LÚC CÒN LÀ ROOT (xem Dockerfile — không còn "USER congvan" tĩnh
# nữa). Sửa quyền cho 2 nơi TRƯỚC KHI dotnet khởi động, rồi hạ quyền xuống "congvan" mới chạy app.
set -e

# /data/files (bind mount tới /home/tainguyen trên host) PHẢI ghi được bởi CẢ app (user "congvan"
# trong container) LẪN người upload thủ công qua FTP/SFTP trực tiếp trên host — 2 bên chạy bằng 2
# UID Linux HOÀN TOÀN KHÁC NHAU, không cách nào chown về "cùng 1 chủ" cho cả hai. Thiết kế gốc dùng
# "chmod 777 /home/tainguyen" đúng để giải quyết việc này (xem apply-bind-mount-tainguyen.ps1) —
# nhưng chỉ áp cho thư mục gốc, KHÔNG đệ quy: thư mục con mới (vd congvanden/2026/10) do app HOẶC
# do client FTP tạo ra đều có thể "ra đời" với quyền hẹp hơn tùy umask của bên tạo, không tự động
# thừa hưởng 777 của thư mục cha. Đây là nguyên nhân thật của 2 sự cố đã gặp:
#   - 2026-09-06: app (congvan) không ghi được vào thư mục tháng mới do FTP/thao tác tay tạo trước.
#   - 2026-09-12: FTP ("553 Can't open that file: Permission denied" lúc STOR) không ghi được vào
#     thư mục do chown một lần từ phiên bản entrypoint TRƯỚC (chỉ chown về "congvan", không giữ lại
#     bit ghi cho "other") — bài học: KHÔNG chown thu hẹp quyền sở hữu ở đây, chỉ CHMOD nới quyền.
# "chmod -R a+rwX" là cách làm lại đúng ý "777 cho mọi thứ" một cách đệ quy, THÊM quyền (không bớt
# quyền của ai) — X viết hoa chỉ set quyền thực thi cho thư mục hoặc file đã có sẵn ít nhất 1 bit
# thực thi, không biến file thường thành file chạy được. Không chown ở đây: quyền sở hữu không còn
# quan trọng nữa khi bit "other" đã cho ghi.
if [ -d /data/files ]; then
  chmod -R a+rwX /data/files 2>/dev/null || true
fi

# /app/dp-keys (data-protection keys) chỉ app dùng, không ai khác cần ghi vào — giữ đúng chủ sở hữu
# "congvan" là đủ, không cần mở quyền cho "other" như /data/files.
if [ -d /app/dp-keys ]; then
  find /app/dp-keys \( ! -user congvan -o ! -group congvan \) -exec chown congvan:congvan {} + 2>/dev/null || true
fi

# Hạ quyền xuống "congvan" (không phải root) trước khi chạy app thật — gosu forward tín hiệu
# (SIGTERM lúc "docker stop") đúng cách cho tiến trình con, khác với su/sudo hay bị kẹt.
exec gosu congvan dotnet CongVan.dll --urls http://+:5100
