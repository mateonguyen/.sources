#!/usr/bin/env bash

set -Eeuo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Hãy chạy script này bằng sudo." >&2
  exit 1
fi

source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/server-scripts" && pwd)"

install -d -o root -g root -m 0755 /opt/thucluc-ops/bin /opt/thucluc-ops/lib
install -o root -g root -m 0644 "$source_dir/common.sh" /opt/thucluc-ops/lib/common.sh

for script in \
  stage-backend-release deploy-backend rollback-backend status-backend logs-backend \
  stage-frontend-release deploy-frontend rollback-frontend status-frontend logs-frontend \
  status-infra backup-minio stage-gotenberg-release stage-minio-release update-gotenberg update-minio
do
  install -o root -g root -m 0755 "$source_dir/$script" "/opt/thucluc-ops/bin/$script"
done

echo "Đã cài script vận hành vào /opt/thucluc-ops."
