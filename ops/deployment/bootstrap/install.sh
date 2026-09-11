#!/usr/bin/env bash

set -Eeuo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Hãy chạy bằng root: sudo bash install.sh" >&2
  exit 1
fi

for required in docker awk sed grep sha256sum install cp date hostname tr; do
  if ! command -v "$required" >/dev/null 2>&1; then
    echo "Thiếu lệnh bắt buộc: $required" >&2
    exit 2
  fi
done
docker compose version >/dev/null

bundle_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
(
  cd "$bundle_dir"
  sha256sum --check --quiet checksums.sha256
)
image="$(tr -d '[:space:]' < "$bundle_dir/ops-image.txt")"
archive="$bundle_dir/ops-console.tar"
if [[ -z "$image" || ! -f "$archive" ]]; then
  echo "Bộ cài thiếu image Ops Console." >&2
  exit 3
fi
if [[ ! -d /opt/thucluc-test || ! -f /opt/thucluc-test/compose.yml || ! -f /opt/thucluc-test/.env ]]; then
  echo "Không tìm thấy ứng dụng hiện tại tại /opt/thucluc-test (cần compose.yml và .env)." >&2
  exit 4
fi

if ! grep -q 'THUCLUC_BACKEND_IMAGE' /opt/thucluc-test/compose.yml; then
  backup_name="/opt/thucluc-test/compose.yml.before-ops-$(date -u +%Y%m%d-%H%M%S)"
  cp --preserve=all /opt/thucluc-test/compose.yml "$backup_name"
  install -m 0644 "$bundle_dir/application.compose.yml" /opt/thucluc-test/compose.yml
  echo "Đã sao lưu Compose cũ tại: $backup_name"
fi

install_root=/opt/thucluc-ops
install -d -m 0750 \
  "$install_root/config" "$install_root/state" "$install_root/releases" \
  "$install_root/backups" "$install_root/flyway-sql" "$install_root/secrets"

admin_user=""
password_hash=""
if [[ -f "$install_root/.env" && -f "$install_root/secrets/admin-password-hash" ]]; then
  admin_user="$(awk -F= '$1 == "OPS_ADMIN_USER" { print substr($0, index($0, "=") + 1); exit }' "$install_root/.env")"
  password_hash="$(tr -d '\r\n' < "$install_root/secrets/admin-password-hash")"
  if [[ ! "$admin_user" =~ ^[A-Za-z][A-Za-z0-9._-]{2,63}$ ]] || [[ "$password_hash" != pbkdf2-sha256\$* ]]; then
    admin_user=""
    password_hash=""
  else
    echo "Giữ nguyên tài khoản quản trị hiện tại: $admin_user"
  fi
fi

if [[ -z "$password_hash" ]]; then
  read -r -p "Tài khoản quản trị [admin]: " admin_user
  admin_user="${admin_user:-admin}"
  if [[ ! "$admin_user" =~ ^[A-Za-z][A-Za-z0-9._-]{2,63}$ ]]; then
    echo "Tài khoản quản trị không hợp lệ." >&2
    exit 5
  fi
  read -r -s -p "Mật khẩu quản trị (ít nhất 10 ký tự): " admin_password
  echo
  read -r -s -p "Nhập lại mật khẩu: " admin_password_again
  echo
  if [[ "$admin_password" != "$admin_password_again" || ${#admin_password} -lt 10 ]]; then
    echo "Mật khẩu không khớp hoặc ngắn hơn 10 ký tự." >&2
    exit 6
  fi
fi

docker load --input "$archive"
docker image inspect "$image" >/dev/null
if [[ -z "$password_hash" ]]; then
  password_hash="$(printf '%s\n' "$admin_password" | docker run --rm -i --entrypoint dotnet "$image" ThucLuc.DbManager.dll --hash-password)"
  unset admin_password admin_password_again
fi
if [[ "$password_hash" != pbkdf2-sha256\$* ]]; then
  echo "Không tạo được mật khẩu quản trị an toàn." >&2
  exit 7
fi

install -m 0640 "$bundle_dir/compose.yml" "$install_root/compose.yml"
if [[ ! -f "$install_root/config/environments.json" ]]; then
  install -m 0640 "$bundle_dir/environments.local.json" "$install_root/config/environments.json"
else
  # Bật migration cho cấu hình TEST đã có từ các bản Ops Console cũ.
  sed -i -E 's/"(AllowMigrate|allowMigrate)"[[:space:]]*:[[:space:]]*false/"\1": true/g' \
    "$install_root/config/environments.json"
fi
printf '%s\n' "$password_hash" > "$install_root/secrets/admin-password-hash"
chmod 0600 "$install_root/secrets/admin-password-hash"
printf 'THUCLUC_OPS_IMAGE=%s\nOPS_ADMIN_USER=%s\nOPS_CONSOLE_PORT=8088\n' \
  "$image" "$admin_user" > "$install_root/.env"
chmod 0600 "$install_root/.env"

docker compose \
  --project-directory "$install_root" \
  --env-file "$install_root/.env" \
  --file "$install_root/compose.yml" \
  up --detach --wait

server_ip="$(hostname -I 2>/dev/null | awk '{print $1}')"
echo
echo "Ops Console đã sẵn sàng: http://${server_ip:-10.10.79.248}:8088"
echo "Tài khoản: $admin_user"
