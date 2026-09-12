#!/bin/bash
set -e

echo "=== 1. Ghi nho danh sach container dang chay truoc khi dung ==="
docker ps --format '{{.Names}}' > /tmp/running_containers_before.txt
cat /tmp/running_containers_before.txt

echo "=== 2. Dung Docker daemon ==="
systemctl stop docker.socket docker.service
sleep 2

echo "=== 3. Copy /var/lib/docker sang /home/docker-data (giu hardlink bang rsync -aHAX) ==="
mkdir -p /home/docker-data
rsync -aHAX /var/lib/docker/ /home/docker-data/
echo "Copy xong. Kich thuoc noi moi:"
du -sh /home/docker-data

echo "=== 4. Cap nhat /etc/docker/daemon.json ==="
cat > /etc/docker/daemon.json <<'EOF'
{
  "data-root": "/home/docker-data"
}
EOF
cat /etc/docker/daemon.json

echo "=== 5. Khoi dong lai Docker ==="
systemctl start docker
sleep 5

echo "=== 6. Xac nhan Docker Root Dir moi ==="
docker info 2>/dev/null | grep "Docker Root Dir"

echo "=== 7. Danh sach container ngay sau khi Docker khoi dong lai ==="
docker ps -a

echo "=== 8. Khoi dong lai nhung container CHUA tu chay (neu co) ==="
for c in $(cat /tmp/running_containers_before.txt); do
  status=$(docker inspect -f '{{.State.Running}}' "$c" 2>/dev/null || echo "missing")
  if [ "$status" != "true" ]; then
    echo "Container $c chua tu chay -> dang khoi dong lai..."
    docker start "$c" || echo "LOI khi khoi dong $c"
  else
    echo "Container $c da tu chay OK"
  fi
done

sleep 8
echo "=== 9. Trang thai cuoi cung tat ca container ==="
docker ps -a

echo "=== 10. Kiem tra rieng congvan_db (du lieu that) con healthy khong ==="
docker inspect -f '{{.State.Health.Status}}' congvan_db 2>/dev/null || echo "khong lay duoc trang thai health"

echo "=== MIGRATION_DONE ==="
