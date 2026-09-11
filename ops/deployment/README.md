# Triển khai Thực Lực V2

Thư mục này chứa công cụ đóng gói release offline và các script cố định chạy trên máy chủ.
Thiết kế tổng thể nằm tại `docs/operations/thucluc-ops-console-plan.md`.

## Cách đơn giản cho máy TEST hiện tại

Máy `10.10.79.248` dùng chế độ **một máy chủ**. Ops Console chạy bằng Docker trên chính máy này,
thao tác trực tiếp với `/opt/thucluc-test` và không cần SSH.

### 1. Tạo bộ cài Ops Console trên máy có Internet

```powershell
.\ops\deployment\build-ops-bootstrap.ps1 -Version local-test-20260908-4
```

Kết quả là `artifacts/ops-bootstrap/thucluc-ops-bootstrap-<version>.zip`.

### 2. Cài một lần trên máy chủ offline

Upload ZIP lên máy chủ, sau đó chạy:

```bash
mkdir -p /opt/thucluc-install/ops-console
unzip thucluc-ops-bootstrap-local-test-20260908-4.zip -d /opt/thucluc-install/ops-console
cd /opt/thucluc-install/ops-console
bash install.sh
```

Script kiểm tra checksum, hỏi tài khoản/mật khẩu quản trị, load image và mở Ops Console tại:

```text
http://10.10.79.248:8088
```

Mật khẩu quản trị được băm PBKDF2 và không ghi vào `.env`. Bộ cài không sửa container MinIO hiện có.

### 3. Tạo gói cập nhật ứng dụng về sau

Mặc định chỉ build backend và frontend:

```powershell
.\ops\deployment\build-release.ps1 -Version test-20260909-1 -CommitSha abc123
```

Có thể chỉ build một thành phần:

```powershell
.\ops\deployment\build-release.ps1 -Version test-20260909-backend -Components Backend
.\ops\deployment\build-release.ps1 -Version test-20260909-frontend -Components Frontend
```

Mở Ops Console, vào **Phát hành**, upload ZIP, chọn thành phần và bấm triển khai. Gotenberg và MinIO
chỉ được đưa vào release khi chủ động truyền image nguồn; không phải upload lại trong mỗi lần sửa ứng dụng.

Các phần bên dưới dành cho mô hình nhiều máy chủ qua SSH và chưa cần áp dụng cho máy TEST hiện tại.

## Tạo gói release trên máy build Windows

Từ root repository:

```powershell
.\ops\deployment\build-release.ps1 -Version 1.0.0 -CommitSha abc123
```

Nếu release đồng thời update hạ tầng, image nguồn phải có sẵn trên máy build rồi truyền thêm:

```powershell
.\ops\deployment\build-release.ps1 `
  -Version 1.0.0 `
  -CommitSha abc123 `
  -GotenbergSourceImage gotenberg/gotenberg:8.17.3 `
  -MinioSourceImage minio/minio:RELEASE.2026-01-12T20-14-26Z
```

Script retag hai image thành `thucluc-gotenberg:<version>` và `thucluc-minio:<version>` để production
không phụ thuộc tag upstream. Nếu không truyền hai tham số này thì gói release mặc định chỉ chứa backend và frontend.

Kết quả:

```text
artifacts/releases/thucluc-release-1.0.0.zip
```

Script build các thành phần trong tham số `-Components`, xuất chúng thành file TAR, sao chép Flyway SQL
khi có backend, tạo manifest và checksum rồi mới đóng ZIP. Ops Console được phát hành riêng bằng bộ bootstrap.

## Chuẩn bị từng máy chủ

1. Upload thư mục `ops/deployment` lên một thư mục tạm trên máy chủ.
2. Cài các script với quyền `root`:

   ```bash
   sudo bash install-server-scripts.sh
   ```

3. Tạo user riêng `thucluc-ops` và cài SSH public key.
4. Dùng `visudo` để tạo `/etc/sudoers.d/thucluc-ops` từ mẫu `thucluc-ops.sudoers.example`.
5. Chỉ bỏ comment đúng dòng tương ứng với vai trò của máy chủ.
6. Kiểm tra file sudoers:

   ```bash
   sudo visudo --check --file=/etc/sudoers.d/thucluc-ops
   ```

Không thêm `thucluc-ops` vào nhóm `docker`. Các script root cố định là lớp giới hạn quyền truy cập Docker.

## Thư mục ứng dụng

Backend server:

```text
/opt/thucluc/backend/compose.yml
/opt/thucluc/backend/.env
/opt/thucluc/backend/releases/
/opt/thucluc/backend/logs/
```

Frontend server:

```text
/opt/thucluc/frontend/compose.yml
/opt/thucluc/frontend/.env
/opt/thucluc/frontend/releases/
```

Infra server:

```text
/opt/thucluc/infra/compose.yml
/opt/thucluc/infra/.env
/opt/thucluc/infra/minio-data/
/opt/thucluc/infra/backups/
```

Các file Compose và `.env.example` nằm trong `infra/production`. Sao chép file đúng vai trò tới máy chủ,
đổi tên thành `compose.yml` và `.env`, sau đó thay toàn bộ giá trị `CHANGE_ME`.

## Cấu hình Ops Console

Tạo các thư mục bền vững trước:

```bash
sudo mkdir -p /opt/thucluc-ops/{config,state,releases,backups,flyway-sql,secrets}
sudo chown -R 1654:1654 /opt/thucluc-ops/{config,state,releases,backups,flyway-sql}
```

1. Sao chép `ops/db-manager/config/environments.example.json` thành
   `/opt/thucluc-ops/config/environments.json` trên máy chạy Ops Console.
2. Sửa ba host `backend`, `frontend`, `infra` theo IP thực tế. Các máy thuộc cùng TEST hoặc PROD phải dùng
   cùng `EnvironmentKey` để hàng đợi không chạy hai thao tác thay đổi cùng lúc trên cùng môi trường.
3. Đặt SSH private key và file `known_hosts` trong `/opt/thucluc-ops/secrets` theo hướng dẫn bên dưới; chỉ user
   trong container được đọc các file này.
4. Tạo `ops-console.env` từ `infra/production/ops-console.env.example`, đặt đúng tag image rồi chạy:

   ```bash
   cd /opt/thucluc-ops
   docker compose --env-file ops-console.env --file compose.yml up -d --wait
   ```

5. Vì app chưa có đăng nhập, cổng mặc định chỉ bind `127.0.0.1`. Từ máy trạm mở SSH tunnel:

   ```bash
   ssh -L 8088:127.0.0.1:8088 <user-ssh>@<may-chu-ops>
   ```

6. Mở `http://127.0.0.1:8088`, vào **Máy chủ** và bấm **Kiểm tra tất cả** trước khi upload release.

Không đổi `OPS_CONSOLE_BIND_IP` thành `0.0.0.0` khi chưa có lớp đăng nhập/reverse proxy bảo vệ phía trước.

### Tạo secrets cho Ops Console

Chạy một lần trên máy Ops Console. Chứng thư này chỉ dùng để mã hóa key bảo vệ các mật khẩu DB mà app lưu:

```bash
sudo mkdir -p /opt/thucluc-ops/secrets
sudo chmod 0700 /opt/thucluc-ops/secrets
cd /opt/thucluc-ops/secrets

sudo openssl req -x509 -newkey rsa:3072 -sha256 -days 3650 -nodes \
  -subj "/CN=ThucLuc Ops Data Protection" \
  -keyout data-protection.key -out data-protection.crt

read -rsp "Mật khẩu bảo vệ file PFX: " PFX_PASSWORD
printf '%s' "$PFX_PASSWORD" | sudo tee data-protection-pfx-password >/dev/null
unset PFX_PASSWORD

sudo openssl pkcs12 -export \
  -inkey data-protection.key \
  -in data-protection.crt \
  -out data-protection.pfx \
  -passout file:data-protection-pfx-password
```

Sau khi sao chép SSH private key thành `/opt/thucluc-ops/secrets/id_ed25519` và tạo file
`/opt/thucluc-ops/secrets/known_hosts`, đặt quyền đọc đúng với user `app` (UID 1654) trong image .NET:

```bash
sudo chown -R 1654:1654 /opt/thucluc-ops/secrets
sudo chmod 0700 /opt/thucluc-ops/secrets
sudo chmod 0400 /opt/thucluc-ops/secrets/data-protection.pfx
sudo chmod 0400 /opt/thucluc-ops/secrets/data-protection-pfx-password
sudo chmod 0400 /opt/thucluc-ops/secrets/id_ed25519
sudo chmod 0400 /opt/thucluc-ops/secrets/known_hosts
```

Giữ bản sao `data-protection.pfx` và mật khẩu PFX ở nơi an toàn, tách khỏi backup `state`. Nếu mất certificate
trong khi `state` vẫn chứa credential đã mã hóa, app không thể giải mã lại mật khẩu đã lưu. Hai file
`data-protection.key` và `data-protection.crt` trung gian không cần mount vào container.

Rollback trên giao diện chỉ chuyển backend/frontend về image liền trước được ghi trên từng máy chủ. App không
tự rollback Oracle. Nếu backend cũ không tương thích schema hiện tại thì không được chọn rollback backend.

Khi release có Backend và migration, Ops Console tự chạy một job theo thứ tự: sao lưu Oracle, nạp Flyway image
từ gói offline, chạy `flyway migrate`, cập nhật Backend rồi cập nhật Frontend. Flyway tự validate trước khi migrate;
nếu backup hoặc migration thất bại thì job dừng và không thay image ứng dụng.

Gói release Backend phải chứa cả `flyway-<version>.tar` và `flyway/sql`. Máy chủ không cần Internet và không cần
cài Flyway trực tiếp.

## Lưu ý

- File `.env`, SSH private key và mật khẩu không được commit vào Git.
- Luôn dùng image tag cố định, không dùng `latest`.
- `deploy-*` chỉ nhận version gồm chữ, số, `.`, `_` và `-`.
- `rollback-*` chỉ dùng image trước đó vẫn còn trong Docker image cache.
- Backup MinIO hiện tạo bản mirror các object hiện tại. Đây chưa phải server-side replication và chưa thay thế
  kiểm thử restore định kỳ.
- Máy chủ infra phải được nạp sẵn cả image `MINIO_IMAGE` và `MINIO_MC_IMAGE`; job backup dùng container
  `minio/mc` riêng, không giả định image MinIO server có sẵn lệnh `mc`.
- Khi chọn update MinIO từ release, job luôn chạy backup trước. Script trên server cũng từ chối update nếu không
  thấy marker backup hoàn tất trong 24 giờ. Nếu MinIO mới không lên được, script không tự hạ version vì format dữ
  liệu có thể đã thay đổi; đây là lỗi cần kiểm tra thủ công.
