# ThucLuc V2 — Infrastructure (Local Dev)

Dev services chạy qua Docker Compose. Backend cần MinIO (file storage) và Gotenberg (PDF).

## Yêu cầu
- Docker Desktop hoặc Docker Engine + Compose plugin

## Khởi động

```bash
cd .sources/infra

# Copy env lần đầu
cp .env.example .env

# Start tất cả
docker compose up -d

# Chỉ MinIO
docker compose up -d minio
```

## Dừng / Xóa

```bash
docker compose stop        # dừng, giữ data
docker compose down        # dừng + xóa container (data volume vẫn còn)
docker compose down -v     # dừng + xóa cả volume (mất hết data)
```

## Services

| Service    | URL                    | Dùng để                       |
|------------|------------------------|-------------------------------|
| MinIO API  | http://localhost:9000  | Backend kết nối (S3 endpoint) |
| MinIO UI   | http://localhost:9001  | Quản lý bucket/file           |
| Gotenberg  | http://localhost:3000  | Render PDF báo cáo            |

Login MinIO UI: xem `.env`

## appsettings.Development.json

```json
"Minio": {
  "ServiceUrl": "http://localhost:9000",
  "AccessKey": "minioadmin",
  "SecretKey": "minioadmin123",
  "BucketName": "thuc-luc",
  "UseSsl": false
},
"Pdf": {
  "GotenbergUrl": "http://localhost:3000"
}
```
