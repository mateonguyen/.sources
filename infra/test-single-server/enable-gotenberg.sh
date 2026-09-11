#!/usr/bin/env bash
set -Eeuo pipefail

cd "$(dirname "$0")"

if ! docker inspect thucluc-backend-test >/dev/null 2>&1; then
  echo "Không tìm thấy container thucluc-backend-test đang chạy."
  exit 1
fi

while IFS= read -r entry; do
  case "$entry" in
    ConnectionStrings__Oracle=*)
      export ORACLE_CONNECTION_STRING="${entry#ConnectionStrings__Oracle=}"
      ;;
    Database__Schema=*)
      export ORACLE_SCHEMA="${entry#Database__Schema=}"
      ;;
    Jwt__SigningKey=*)
      export JWT_SIGNING_KEY="${entry#Jwt__SigningKey=}"
      ;;
    Minio__AccessKey=*)
      export MINIO_ACCESS_KEY="${entry#Minio__AccessKey=}"
      ;;
    Minio__SecretKey=*)
      export MINIO_SECRET_KEY="${entry#Minio__SecretKey=}"
      ;;
    Minio__BucketName=*)
      export MINIO_BUCKET_NAME="${entry#Minio__BucketName=}"
      ;;
    Cors__AllowedOrigins__0=*)
      export FRONTEND_ORIGIN="${entry#Cors__AllowedOrigins__0=}"
      ;;
  esac
done < <(docker inspect thucluc-backend-test \
  --format '{{range .Config.Env}}{{println .}}{{end}}')

required=(
  ORACLE_CONNECTION_STRING
  ORACLE_SCHEMA
  JWT_SIGNING_KEY
  MINIO_ACCESS_KEY
  MINIO_SECRET_KEY
  MINIO_BUCKET_NAME
  FRONTEND_ORIGIN
)

for variable_name in "${required[@]}"; do
  if [[ -z "${!variable_name:-}" ]]; then
    echo "Thiếu cấu hình $variable_name trong container backend hiện tại."
    exit 1
  fi
done

docker compose -f compose.yml up -d

echo
docker compose -f compose.yml ps
echo

wait_for_url() {
  local name="$1"
  local url="$2"

  for _ in {1..30}; do
    if curl --fail --silent "$url" >/dev/null; then
      return 0
    fi
    sleep 2
  done

  echo "$name chưa sẵn sàng tại $url"
  return 1
}

wait_for_url "Gotenberg" "http://127.0.0.1:3000/health"
wait_for_url "Backend" "http://127.0.0.1:8080/health/ready"

echo "DONE: Gotenberg và backend đều hoạt động."
