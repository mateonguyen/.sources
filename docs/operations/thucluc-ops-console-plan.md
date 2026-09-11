# Kế hoạch xây dựng ThucLuc Ops Console

> Trạng thái: Đang triển khai — đã hoàn thành lát cắt nền móng và deploy ứng dụng  
> Cập nhật gần nhất: 08/09/2026  
> Phạm vi: Deploy, update, backup, restore, Flyway và vận hành hạ tầng của hệ thống Thực Lực V2.

Tài liệu này là nguồn thông tin chính cho việc mở rộng miniapp `ThucLuc.DbManager` thành một ứng dụng
vận hành thống nhất. Sau mỗi lần thay đổi thiết kế hoặc triển khai chức năng liên quan, cần cập nhật:

1. Mục **Trạng thái hiện tại**.
2. Checklist trong **Lộ trình thực hiện**.
3. Mục **Nhật ký thay đổi** ở cuối tài liệu.

Các tài liệu liên quan:

- [Quy trình database, backup và restore](database-deployment-backup-restore-runbook.md)
- [Chọn/tạo PDB và restore](oracle-pdb-selection-and-restore.md)
- [Cài Oracle XE 21 song song Oracle 19](oracle-xe21-ol8-side-by-side-installation.md)

## 1. Mục tiêu đã thống nhất

Người vận hành chỉ sử dụng **một web app nội bộ**, dự kiến đổi tên thành **ThucLuc Ops Console**.

Ứng dụng này sẽ quản lý:

- Deploy và update backend.
- Deploy và update frontend.
- Chạy Flyway để cập nhật cấu trúc database.
- Backup và restore Oracle.
- Backup và kiểm tra MinIO.
- Update các container hạ tầng như MinIO và Gotenberg có kiểm soát.
- Kiểm tra phiên bản, trạng thái và log của các thành phần.
- Lưu lịch sử ai đã chạy thao tác nào, lúc nào và kết quả ra sao.

Ops Console là **bộ điều phối triển khai**, không phải nơi build source code. Docker image phải được build và
kiểm thử trên máy dev/build trước, sau đó mới đóng gói để đưa lên máy chủ.

## 2. Kiến trúc tổng thể

```text
Người vận hành
      │
      ▼
THUCLUC OPS CONSOLE
Một giao diện quản trị duy nhất
      │
      ├── SSH ──► Máy chủ Backend  ──► Docker / Backend API
      │
      ├── SSH ──► Máy chủ Frontend ──► Docker / Nginx / Angular
      │
      ├── SSH ──► Máy chủ Infra    ──► Docker / MinIO / Gotenberg
      │
      └────────► Máy chủ Oracle    ──► Data Pump / Flyway / PDB / Schema
```

Người dùng chỉ thấy một ứng dụng. Ở tầng kỹ thuật, Ops Console kết nối tới từng máy chủ bằng tài khoản
vận hành riêng và chỉ được gọi các script đã định nghĩa trước.

### Phương án ban đầu

Để dễ triển khai và phù hợp môi trường không có Internet:

- Dùng SSH từ Ops Console tới các máy chủ.
- Không cài thêm agent riêng ở giai đoạn đầu.
- Không cho nhập lệnh shell tùy ý trên giao diện.
- Trên mỗi máy chủ chỉ có các script cố định, do `root` sở hữu và người chạy app không được sửa.
- Tài khoản SSH chỉ được phép gọi đúng các script cần thiết thông qua cấu hình `sudoers` giới hạn.

Ví dụ script trên máy chủ backend:

```text
/opt/thucluc-ops/bin/deploy-backend
/opt/thucluc-ops/bin/rollback-backend
/opt/thucluc-ops/bin/status-backend
/opt/thucluc-ops/bin/logs-backend
```

Nếu sau này số lượng máy chủ tăng hoặc cần chạy job bền vững hơn, có thể bổ sung `Ops Runner` nhỏ trên
mỗi máy chủ. Việc này không làm thay đổi giao diện và người vận hành vẫn chỉ dùng một Ops Console.

## 3. Ranh giới an toàn

Ops Console được phép:

- Upload gói phát hành đã được kiểm tra.
- Gọi các script deploy/rollback/status cố định.
- Chạy các bước Flyway đã được phê duyệt.
- Thực hiện backup/restore qua các luồng có kiểm tra và xác nhận.
- Đọc health check, phiên bản và log giới hạn của ứng dụng.

Ops Console không được phép:

- Nhận và chạy câu lệnh shell tùy ý từ giao diện.
- Mount trực tiếp Docker socket vào web app.
- Tự nâng cấp hệ điều hành, Docker Engine hoặc Oracle major version.
- Tự động rollback database sau khi migration đã chạy.
- Tự động xóa volume, bucket, schema hoặc PDB mà không có luồng xác nhận riêng.

Quyền truy cập Docker daemon gần tương đương quyền `root` trên máy chủ. Vì vậy không đưa
`/var/run/docker.sock` trực tiếp vào container Ops Console.

## 4. Các màn hình dự kiến

| Màn hình               | Chức năng chính                                                   |
|------------------------|-------------------------------------------------------------------|
| Tổng quan              | Phiên bản, health, dung lượng và trạng thái các máy chủ            |
| Phát hành              | Upload gói release, kiểm tra, deploy và rollback                   |
| Cơ sở dữ liệu          | Kết nối, PDB/schema, backup và restore Oracle                      |
| Flyway                 | `info`, `validate`, danh sách migration chờ chạy và `migrate`      |
| MinIO                  | Bucket, dung lượng, backup/mirror và tình trạng bản sao            |
| Hạ tầng                | Trạng thái và update có kiểm soát cho MinIO/Gotenberg              |
| Máy chủ                | Khai báo backend, frontend, infra và thông tin kết nối             |
| Lịch sử thao tác       | Trạng thái job, người thực hiện, thời gian, log và lỗi              |

### Phân quyền dự kiến

| Vai trò       | Quyền                                                                  |
|---------------|------------------------------------------------------------------------|
| Người xem     | Xem phiên bản, trạng thái, lịch sử và log đã lọc                       |
| Vận hành      | Backup, deploy TEST, restart service được phép                         |
| Quản trị      | Deploy PROD, restore, chạy Flyway và cập nhật cấu hình máy chủ         |
| Phê duyệt     | Xác nhận thao tác rủi ro cao nếu sau này áp dụng quy trình hai người   |

## 5. Chuẩn gói phát hành offline

Máy chủ không cần Internet và không build source. Mỗi phiên bản được đóng thành một file ZIP:

```text
thucluc-release-1.5.0.zip
├── release-manifest.json
├── backend-1.5.0.tar
├── frontend-1.5.0.tar
├── ops-console-1.5.0.tar
├── gotenberg-1.5.0.tar           # tùy chọn
├── minio-1.5.0.tar               # tùy chọn
├── flyway/
│   └── sql/
└── checksums.sha256
```

Ví dụ `release-manifest.json`:

```json
{
  "version": "1.5.0",
  "backendImage": "thucluc-backend:1.5.0",
  "frontendImage": "thucluc-frontend:1.5.0",
  "opsConsoleImage": "thucluc-ops-console:1.5.0",
  "gotenbergImage": "thucluc-gotenberg:1.5.0",
  "minioImage": "thucluc-minio:1.5.0",
  "databaseMigration": "V90",
  "minimumOracleVersion": "19",
  "createdAt": "2026-09-07T14:00:00+07:00"
}
```

Quy tắc:

- Không dùng tag `latest` cho backend, frontend và các bản phát hành production.
- Mỗi image có tag version bất biến, ví dụ `1.5.0`.
- Gói release phải có checksum để phát hiện file upload lỗi.
- Ops Console lưu lại manifest và checksum cùng lịch sử triển khai.
- Chỉ gửi image tới đúng máy chủ cần chạy image đó.

## 6. Luồng deploy chuẩn

```text
Upload/chọn gói release
          │
          ▼
Kiểm tra manifest và checksum
          │
          ▼
Kiểm tra SSH, Docker, dung lượng, Oracle và MinIO
          │
          ▼
Xác định thành phần thực sự thay đổi
          │
          ├── Có migration ──► Backup Oracle ──► Flyway validate/migrate
          │
          ├── Có thay đổi file/MinIO ──► Backup MinIO theo chính sách
          │
          ▼
Deploy backend ──► Chờ `/health/ready`
          │
          ▼
Deploy frontend ──► Smoke test qua URL thật
          │
          ▼
Ghi nhận thành công hoặc chuyển sang luồng xử lý lỗi
```

### Trình tự chi tiết

1. Người vận hành upload hoặc chọn gói release.
2. App đọc manifest, kiểm tra checksum và các file bắt buộc.
3. App kiểm tra kết nối đến từng máy chủ cần dùng.
4. App kiểm tra Docker, dung lượng đĩa và phiên bản hiện tại.
5. Nếu có migration:
   - Chạy Flyway `info`.
   - Chạy Flyway `validate`.
   - Tạo backup Oracle bắt buộc.
   - Chạy Flyway `migrate`.
6. Upload image backend tới backend server.
7. Chạy `docker load` và cập nhật backend bằng script cố định.
8. Chờ backend đạt `/health/ready`.
9. Upload image frontend tới frontend server.
10. Cập nhật frontend và chạy smoke test.
11. Lưu version mới, log và kết quả vào lịch sử.

### Khi deploy thất bại

- Frontend lỗi: quay lại image frontend trước đó.
- Backend lỗi trước migration: quay lại image backend trước đó.
- Backend lỗi sau migration: chỉ rollback image nếu backend cũ vẫn tương thích schema mới.
- Migration lỗi: dừng quy trình và không deploy tiếp.
- Không tự restore database. Restore phải là thao tác riêng có xác nhận vì có thể làm mất dữ liệu phát sinh.
- MinIO update lỗi: không tự hạ version nếu chưa xác nhận khả năng tương thích dữ liệu.

## 7. Chính sách backup

| Loại thay đổi                         | Oracle | MinIO | Ghi chú                                      |
|---------------------------------------|:------:|:-----:|----------------------------------------------|
| Chỉ thay đổi frontend                 | Không  | Không | Không làm thay đổi dữ liệu                   |
| Backend, không thay đổi database      | Tùy    | Không | Backup nếu release có rủi ro nghiệp vụ cao   |
| Có Flyway migration                   | Có     | Tùy   | Oracle backup là bắt buộc                    |
| Thay đổi luồng upload/xóa file        | Có     | Có    | Cần giữ đồng bộ metadata và object           |
| Update MinIO                          | Không  | Có    | Phải kiểm tra bản sao trước khi update       |
| Release lớn hoặc lần đầu lên PROD     | Có     | Có    | Nên bật chế độ bảo trì tạm thời               |

Oracle và MinIO là hai vùng dữ liệu riêng:

- Oracle lưu dữ liệu nghiệp vụ và thông tin tham chiếu file.
- MinIO lưu nội dung file thật.
- Backup database không chứa nội dung file trong MinIO.

Hai bản backup nên được gom thành một `Backup Set` chung:

```text
PROD-20260907-140000/
├── oracle/
│   ├── cand_qlcntt_20260907.dmp
│   ├── export.log
│   └── manifest.json
├── minio/
│   └── objects-or-replication-reference.json
└── backup-set.json
```

Để tăng tính nhất quán cho bản sao quan trọng:

1. Bật chế độ bảo trì hoặc chặn thao tác ghi mới.
2. Chờ các request đang xử lý hoàn tất.
3. Backup Oracle.
4. Backup/mirror MinIO.
5. Kiểm tra kết quả và checksum.
6. Tắt chế độ bảo trì.

## 8. Job và lịch sử thao tác

Job không được phụ thuộc vào tab trình duyệt hoặc Blazor circuit. Đóng trình duyệt không được làm hủy deploy.

Trạng thái chung:

```text
Queued
  └── Preflight
        └── Backup
              └── Migrate
                    └── DeployBackend
                          └── DeployFrontend
                                └── Verify
                                      ├── Succeeded
                                      └── Failed
```

Mỗi job cần lưu tối thiểu:

- ID job.
- Môi trường đích.
- Loại thao tác.
- Người thực hiện.
- Phiên bản trước và sau.
- Release manifest/checksum.
- Thời gian bắt đầu/kết thúc.
- Trạng thái từng bước.
- Log đã lọc thông tin nhạy cảm.
- Backup Set liên quan.

Chỉ cho phép một job thay đổi chạy tại cùng thời điểm trên mỗi môi trường. Các thao tác chỉ đọc như xem
health hoặc log có thể chạy song song.

Giai đoạn đầu dùng SQLite để lưu job, lịch sử và khóa thao tác. Không dùng bộ nhớ RAM vì lịch sử sẽ mất
khi Ops Console restart hoặc tự update.

## 9. Cấu trúc triển khai đề xuất trên máy chủ

### Máy chủ quản trị/Ops Console

```text
/opt/thucluc-ops/
├── compose.yml
├── config/
├── state/
├── releases/
├── backups/
└── logs/
```

Các thư mục `config`, `state`, `releases`, `backups` và `logs` phải được mount ra ngoài container.

### Máy chủ Backend

```text
/opt/thucluc/backend/
├── compose.yml
├── .env
├── releases/
└── logs/

/opt/thucluc-ops/bin/
├── deploy-backend
├── rollback-backend
├── status-backend
└── logs-backend
```

### Máy chủ Frontend

```text
/opt/thucluc/frontend/
├── compose.yml
├── .env
└── releases/

/opt/thucluc-ops/bin/
├── deploy-frontend
├── rollback-frontend
├── status-frontend
└── logs-frontend
```

### Máy chủ Infra

```text
/opt/thucluc/infra/
├── compose.yml
├── .env
├── minio-data/
└── backups/

/opt/thucluc-ops/bin/
├── backup-minio
├── status-infra
├── update-gotenberg
└── update-minio
```

## 10. Trạng thái source hiện tại

| Hạng mục                                      | Trạng thái       | Ghi chú                                                |
|-----------------------------------------------|------------------|--------------------------------------------------------|
| Miniapp .NET 8 Blazor Server                  | Đã có            | Hiện mang tên `ThucLuc.DbManager`                       |
| Font chữ miniapp                              | Đã chuẩn hóa     | Google Sans tự lưu trữ, hỗ trợ tiếng Việt, không gọi Internet |
| Backup/restore Oracle                         | Đã có            | Đã phát triển luồng Data Pump                           |
| Chọn/tạo PDB                                  | Đã có            | Cần tiếp tục kiểm tra thực tế theo từng môi trường      |
| Flyway trên giao diện                         | Đã có bản đọc    | Đối chiếu file SQL với `flyway_schema_history`; chưa migrate |
| Lịch sử thao tác bền vững                     | Đã có            | SQLite tại `state/operations.db`                        |
| Dockerfile backend                            | Đã có            | Multi-stage build .NET 8, health check qua Compose      |
| Dockerfile frontend                           | Đã có            | Build Angular và chạy bằng Nginx                        |
| Dockerfile Ops Console                        | Đã có            | Có Docker CLI; hỗ trợ cả Local mode và SSH mode         |
| Compose production                            | Đã có bản đầu    | Tách file cho backend, frontend, infra và Ops Console   |
| Upload/deploy release offline                 | Đã có bản đầu    | ZIP theo thành phần; Local mode không cần SSH/SCP       |
| Backup MinIO                                  | Đã có bản đầu    | Mirror object; chưa kiểm thử restore/replication        |
| Update Gotenberg/MinIO                        | Đã có bản đầu    | Image tùy chọn trong release; MinIO bắt buộc backup trước |
| SSH execution                                 | Đã có            | Strict host key, action allowlist và script cố định     |
| Health check backend                          | Đã có            | Có `/health`, `/health/live`, `/health/ready`, `/version`|
| API frontend dùng URL tương đối               | Đã có            | Dùng `/api/v1`, Nginx proxy tới backend                 |
| Bảo vệ truy cập Ops Console                   | Đã có bản đầu    | Cookie login; mật khẩu quản trị băm PBKDF2              |
| Chế độ một máy chủ TEST                      | Đã có            | Docker local, `/opt/thucluc-test`, không yêu cầu SSH    |
| Bộ cài Ops Console offline                    | Đã có            | Một ZIP và `install.sh`; kiểm tra checksum trước cài    |

## 11. Lộ trình thực hiện

### Giai đoạn 1 — Nền móng Ops Console

- [x] Chốt tên `ThucLuc Ops Console` và cập nhật navigation.
- [x] Thiết kế model môi trường và danh sách máy chủ.
- [x] Lưu cấu hình không nhạy cảm vào file hoặc SQLite.
- [x] Mã hóa credential DB bằng Data Protection key được bảo vệ bằng PFX; mount SSH secrets chỉ-đọc.
- [ ] Bổ sung quy trình xoay SSH key, certificate và token định kỳ.
- [x] Thay `InMemoryOperationJournal` bằng SQLite.
- [x] Tạo background job worker không phụ thuộc trình duyệt.
- [x] Tạo khóa thao tác theo môi trường.
- [x] Thêm đăng nhập quản trị nội bộ; vai trò chi tiết và audit đăng nhập làm sau.
- [x] Xây dựng SSH executor chỉ gọi script cho phép.

Kết quả mong đợi: app đọc được trạng thái các máy chủ và chạy được một job kiểm tra an toàn.

### Giai đoạn 2 — Đóng gói và deploy backend/frontend

- [x] Tạo Dockerfile backend.
- [x] Tạo Dockerfile frontend + Nginx.
- [x] Đổi frontend sang API URL tương đối `/api/v1`.
- [x] Chuẩn hóa biến môi trường backend.
- [x] Tạo Compose production cho backend.
- [x] Tạo Compose production cho frontend.
- [x] Tạo script deploy/rollback/status trên từng máy chủ.
- [x] Thiết kế `release-manifest.json`.
- [x] Upload và kiểm tra gói release offline.
- [x] Deploy backend và chờ `/health/ready`.
- [x] Deploy frontend và chạy smoke test qua health check container.
- [ ] Bổ sung smoke test URL nghiệp vụ sau deploy frontend.
- [x] Bổ sung nút rollback trên Ops Console; không tự rollback database.
- [x] Cho phép release chỉ chứa backend hoặc frontend; tách Ops Console sang bộ cài riêng.

Kết quả mong đợi: cập nhật backend hoặc frontend bằng một luồng trên Ops Console.

### Giai đoạn 3 — Flyway và backup đồng bộ

- [x] Hiển thị Flyway `info` và migration đang chờ bằng truy vấn chỉ đọc.
- [x] Chạy validate của Flyway trước/trong lệnh migrate.
- [x] Bắt buộc backup Oracle khi release Backend có migration.
- [x] Thực thi Flyway bằng job nền trước Backend và Frontend.
- [ ] Thiết kế Backup Set Oracle + MinIO.
- [x] Thực hiện backup/mirror MinIO bản đầu.
- [ ] Kiểm thử khôi phục MinIO trên môi trường test.
- [ ] Bổ sung maintenance mode để chặn ghi khi cần.

Kết quả mong đợi: release có migration và dữ liệu file được bảo vệ theo cùng một quy trình.

### Giai đoạn 4 — Quản trị hạ tầng

- [x] Hiển thị trạng thái container MinIO và Gotenberg qua script `status-infra`.
- [x] Update Gotenberg theo image version, tự thử quay về image cũ nếu health check lỗi.
- [x] Update MinIO sau khi tạo backup và xác minh marker hoàn tất trong 24 giờ.
- [x] Hiển thị dung lượng volume MinIO/backup và cảnh báo theo `DiskWarningPercent`.
- [ ] Bổ sung cơ chế Ops Runner nếu cần mở rộng nhiều máy chủ.
- [x] Có bộ bootstrap để cài/cập nhật Ops Console bằng một lệnh; tự cập nhật ngay trong giao diện làm sau.

Kết quả mong đợi: các container hạ tầng thông thường được quản lý trong cùng giao diện nhưng vẫn có chốt an toàn.

## 12. Việc không làm trong giai đoạn đầu

- Không triển khai Kubernetes.
- Không bắt buộc cài Jenkins hoặc GitLab Runner.
- Không dựng private Docker Registry ngay nếu upload image TAR đã đáp ứng được nhu cầu.
- Không build backend/frontend trên production server.
- Không tự nâng Oracle, Docker Engine hoặc hệ điều hành từ giao diện web.
- Không tự động rollback database.
- Không cho phép chạy shell command tùy ý.

## 13. Các quyết định còn cần chốt

| Mã      | Nội dung cần quyết định                                      | Trạng thái |
|---------|--------------------------------------------------------------|------------|
| DEC-001 | Ops Console sẽ đặt trên server infra hay server quản trị riêng | Chưa chốt  |
| DEC-002 | TEST và PROD dùng chung hay tách Ops Console                  | Chưa chốt  |
| DEC-003 | Dùng tài khoản nội bộ hay tích hợp Keycloak                    | Chưa chốt  |
| DEC-004 | MinIO backup bằng mirror định kỳ hay server-side replication   | Chưa chốt  |
| DEC-005 | Thư mục/ổ đĩa vật lý lưu release và backup                     | Chưa chốt  |
| DEC-006 | Có yêu cầu hai người phê duyệt thao tác PROD hay không         | Chưa chốt  |

## 14. Quy tắc cập nhật tài liệu

Mỗi pull request hoặc lần chỉnh sửa ảnh hưởng tới Ops Console cần:

1. Cập nhật trạng thái checklist tương ứng.
2. Sửa mục **Trạng thái source hiện tại** nếu chức năng đã thay đổi.
3. Thêm một dòng vào **Nhật ký thay đổi**.
4. Nếu thay đổi một quyết định kiến trúc, thêm hoặc cập nhật mã `DEC-xxx`.
5. Không ghi mật khẩu, SSH private key, token hoặc thông tin nhạy cảm vào tài liệu.

## 15. Nhật ký thay đổi

| Ngày       | Thay đổi                                                                 | Trạng thái |
|------------|--------------------------------------------------------------------------|------------|
| 07/09/2026 | Tạo tài liệu; chốt hướng một Ops Console, SSH và script giới hạn quyền   | Đề xuất    |
| 07/09/2026 | Hoàn thành SQLite journal, job nền, host inventory và SSH/SCP allowlist  | Hoàn thành |
| 07/09/2026 | Thêm Dockerfile, Compose production và gói release offline               | Hoàn thành |
| 07/09/2026 | Thêm màn hình trạng thái hạ tầng và job mirror MinIO bản đầu              | Hoàn thành |
| 07/09/2026 | Thêm màn hình đối chiếu Flyway chỉ đọc với `flyway_schema_history`        | Hoàn thành |
| 07/09/2026 | Nối rollback backend/frontend vào hàng đợi và sửa tham số script SSH       | Hoàn thành |
| 07/09/2026 | Siết gói release đủ ba image và tách MinIO backup sang image `minio/mc`    | Hoàn thành |
| 08/09/2026 | Thêm image hạ tầng tùy chọn, update Gotenberg và MinIO có chốt backup       | Hoàn thành |
| 08/09/2026 | Hiển thị dung lượng ổ MinIO/backup và cảnh báo khi vượt ngưỡng cấu hình    | Hoàn thành |
| 08/09/2026 | Bảo vệ credential bằng PFX và tách SSH key/known_hosts sang mount chỉ-đọc | Hoàn thành |
| 08/09/2026 | Khóa cổng Ops Console về localhost cho tới khi chốt cơ chế đăng nhập       | Hoàn thành |
| 08/09/2026 | Thêm status/log máy chủ, timeout job dài và tách riêng volume release      | Hoàn thành |
| 08/09/2026 | Thêm Local mode, đăng nhập quản trị, release theo thành phần và bộ bootstrap | Hoàn thành |
| 10/09/2026 | Chuẩn hóa miniapp sang Google Sans offline; gom vào lần cập nhật Ops Console kế tiếp | Hoàn thành |
| 10/09/2026 | Tích hợp release offline theo thứ tự backup Oracle → Flyway → Backend → Frontend | Hoàn thành |
