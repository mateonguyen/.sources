# ThucLuc DbManager

Ứng dụng quản trị dữ liệu dùng chung để sao lưu từ một cơ sở dữ liệu Oracle nguồn và
phục hồi vào một cơ sở dữ liệu Oracle đích. DEV/TEST chỉ là một cách gán nguồn/đích,
không phải giới hạn của công cụ.

Ứng dụng độc lập với backend/frontend nghiệp vụ và không được public ra Internet.

## Trạng thái hiện tại

- Có dashboard kết nối nguồn/đích.
- Có chức năng kiểm tra máy chủ và đăng nhập Oracle thật.
- Kết nối nguồn/đích được nhập ngay trên màn hình **Sao lưu & phục hồi**; không còn tab cấu hình riêng.
- Kết nối đích có phần chọn/tạo database con (PDB) bằng SYS. App kiểm tra service dẫn vào đúng
  database con rồi lưu làm đích; không đổi service của CDB, không tự mở hay xóa PDB có sẵn.
- Biểu mẫu phục hồi có ba cách xử lý schema: dùng schema trống, tạo schema mới hoặc thay thế toàn bộ
  schema đang có. Hai chế độ thay đổi schema yêu cầu SYS và bước kiểm tra PDB/tablespace.
- Hỗ trợ cả Oracle CDB/PDB và non-CDB: chỉ chặn khi kết nối thật sự vào `CDB$ROOT`; với non-CDB,
  schema được tạo trực tiếp mà không thêm mệnh đề `CONTAINER`.
- Có chức năng tạo bản sao lưu Oracle thật từ một DB nguồn bằng `DBMS_DATAPUMP`.
- File dump được tải từ Oracle Directory về `backups`, kèm `export.log`, `manifest.json` và SHA-256.
- Có chức năng phục hồi thật: kiểm tra SHA-256, chuyển dump vào Oracle Directory và chạy
  `DBMS_DATAPUMP` import. Nếu thay thế schema có dữ liệu, miniapp bắt buộc tạo xong bản sao an toàn
  trước khi xóa và tạo lại schema.
- Migration vẫn khóa cho tới khi hoàn thiện xác nhận an toàn và kiểm thử.

## Chạy ngay để xem giao diện

Không cần cấu hình DB hoặc tài khoản trước khi khởi động:

```powershell
dotnet run --project .\src\ThucLuc.DbManager\ThucLuc.DbManager.csproj
```

Nếu chưa có `config/environments.json`, dashboard báo chưa cấu hình môi trường.
Không có thao tác DB nào tự chạy. Sao lưu chỉ bắt đầu sau khi người dùng chọn DB nguồn và
bấm **Tạo bản sao lưu**; các chức năng thay đổi dữ liệu vẫn khóa.

## Cấu hình trên giao diện

1. Mở **Sao lưu & phục hồi**.
2. Nhập kết nối nguồn và đích trong khu vực đầu màn hình.
3. DB nguồn cần host, port, Service/PDB, schema và tài khoản để sao lưu.
4. DB đích chỉ cần host, port và service đang kết nối được; schema có thể để trống.
   Nếu service dẫn vào `CDB$ROOT`, mở **Chọn / tạo database con (PDB)** để chọn nơi riêng cho dữ liệu.
5. Bấm **Lưu kết nối** tương ứng.

Khi phục hồi, tên và mật khẩu schema đích được nhập ngay trong biểu mẫu restore. Thẻ kết nối đích
không yêu cầu schema hoặc tài khoản. Nếu chọn tạo mới hoặc thay thế schema, giao diện yêu cầu thêm
SYS, mật khẩu sau phục hồi và tablespace.

Hướng dẫn từng bước, giải thích `ORCL19C`, `orcl`, PDB và schema:
[Chọn database con và phục hồi](../../docs/operations/oracle-pdb-selection-and-restore.md).
Tạo PDB không thay thế bước phục hồi, cũng không tự nâng phiên bản TSTZ.

## Tạo bản sao lưu

1. Mở **Sao lưu & phục hồi**.
2. Chọn duy nhất một **DB nguồn**; không cần DB đích.
3. Bấm **Tạo bản sao lưu**. Miniapp tự kiểm tra tài khoản, schema, Oracle Directory và Data Pump.
4. Kết quả được lưu tại `<BackupRoot>/repository/<môi-trường>/<backup-id>/` gồm file `.dmp`,
   `export.log` và `manifest.json` có dung lượng cùng SHA-256.

## Kho lưu trữ bản sao

Hai đường dẫn được cấu hình sẵn; miniapp tự chọn theo hệ điều hành, không cần đổi khóa thủ công:

```json
"BackupRootLinux": "/var/lib/thucluc-db-manager/backups",
"BackupRootWindows": "D:\\ThucLucData\\DbBackups",
"DataPumpExportVersion": "COMPATIBLE"
```

Khi khởi động, miniapp tự tạo các thư mục con sau. Tài khoản chạy service phải có quyền ghi vào
thư mục cha:

```text
<BackupRoot>/
├── repository/   # Bản sao đã hoàn chỉnh, dùng để phục hồi
├── inbox/        # File .dmp được đưa vào từ công cụ khác
├── work/         # File tạm trong lúc xử lý
└── quarantine/   # File lỗi hoặc chưa đạt kiểm tra
```

Khóa `BackupRoot` cũ vẫn được đọc để tương thích. Nếu khóa cũ này có giá trị thì nó được ưu tiên
hơn hai đường dẫn theo hệ điều hành.

Tài khoản Oracle cần quyền export schema và quyền `READ`, `WRITE` trên Oracle Directory đã cấu hình.
File dump do `DBMS_DATAPUMP` tạo trên máy chủ Oracle được miniapp lưu về máy chạy Ops Console.
Miniapp dùng kết nối ODP.NET đã xác thực nên mật khẩu không nằm trong command line, manifest hoặc log.

Với Oracle chạy bằng Docker trên chính máy Ops Console, miniapp tự tìm container theo port Oracle và
dùng `docker cp` để lấy file ra Windows. Với DB từ xa, miniapp mới dùng `BFILE` làm phương án dự phòng.

Nếu schema chưa có quyền Oracle Directory, màn hình backup hiện **Thiết lập bằng SYS**. Người dùng
nhập mật khẩu SYS một lần; miniapp kết nối `SYSDBA`, cấp `READ`, `WRITE` trên `DATA_PUMP_DIR`, cập
nhật kết nối và tiếp tục backup. Mật khẩu SYS không được lưu vào file, kho mã hóa hay nhật ký.

## Xử lý schema khi phục hồi

Trong **Sao lưu & phục hồi → Phục hồi vào DB đích**, chọn một trong ba chế độ:

- **Dùng schema trống đã có**: nhập mật khẩu schema rồi kiểm tra; hệ thống xác nhận đăng nhập, số
  đối tượng, Oracle Directory, phiên bản và `COMPATIBLE`, đồng thời dừng nếu schema không trống.
- **Tạo schema mới**: nhập SYS, mật khẩu schema mới, kiểm tra PDB và chọn tablespace.
- **Thay thế toàn bộ schema đang có**: nhập SYS, mật khẩu schema sau phục hồi, kiểm tra DB đích và
  nhập chính xác tên schema để xác nhận.

Schema không được tạo ở bước kiểm tra. Nó chỉ được tạo ngay trước khi Data Pump import bắt đầu và
không có chức năng tạo schema độc lập. SYS không được lưu.

Ở chế độ thay thế, miniapp dùng SYS tạo một bản sao Data Pump của schema đích hiện tại, tải file về
kho và xác minh SHA-256. Chỉ sau khi bản sao mang trạng thái **Hoàn tất**, miniapp mới chạy
`DROP USER ... CASCADE`, tạo lại schema và import. Miniapp khóa tài khoản và ngắt các phiên của đúng
schema đích trước khi xóa; vẫn nên dừng backend để tránh giao dịch đang chạy bị hủy đột ngột.

## Phục hồi vào DB đích

1. Lưu kết nối đích bằng máy chủ, port và Service/PDB; chưa cần schema.
2. Chọn một bản sao đã hoàn tất. Nếu file `.dmp` nằm trên máy trạm, bấm **Tải file .dmp**,
   nhập schema nguồn có trong dump và đưa file vào kho phục hồi.
3. Chọn bản sao và nhập **Schema đích**.
4. Chọn cách xử lý schema phù hợp.
5. Với schema trống, nhập mật khẩu hiện tại. Với schema mới hoặc cần thay thế, nhập SYS và mật khẩu
   schema sau phục hồi rồi bấm **Kiểm tra DB đích**.
6. Nếu thay thế, nhập chính xác tên schema để xác nhận; miniapp sẽ sao lưu schema hiện tại trước.
7. Đọc nội dung xác nhận và bấm nút phục hồi.

File tải lên được ghi tạm vào `<BackupRoot>/inbox`, kiểm tra đuôi file, dung lượng và SHA-256 rồi
chuyển nguyên tử sang `<BackupRoot>/repository/uploaded/<backup-id>/`. Giới hạn mặc định là 50 GB.
Schema nguồn phải được người dùng nhập vì tên file `.dmp` không bảo đảm cho biết schema nằm trong dump.

Miniapp không import chồng vào schema đang có dữ liệu. Muốn ghi đè, phải chọn rõ chế độ thay thế để
hệ thống sao lưu, xóa và tạo lại schema. File dump được kiểm tra dung lượng/SHA-256 trước khi chuyển
qua kết nối Oracle; dung lượng trên Oracle Directory được đối chiếu lại trước khi import.
Mật khẩu SYS chỉ dùng trong thao tác hiện tại; mật khẩu schema được lưu mã hóa sau khi phục hồi thành công.
Miniapp không áp đặt tối thiểu 12 ký tự; Oracle đích sẽ quyết định chính sách mật khẩu thực tế.
Mỗi manifest ghi phiên bản Oracle nguồn và phiên bản thực tế của dump. Khi chưa kiểm tra DB đích,
`COMPATIBLE` giữ định dạng theo DB nguồn. Sau khi bấm **Kiểm tra DB đích**, miniapp đọc cả phiên bản
phần mềm và tham số `COMPATIBLE`; nút sao lưu hiển thị mức tương thích đích và tự dùng phiên bản cao
nhất không vượt quá cả nguồn lẫn `COMPATIBLE` của đích. Trước khi tạo, xóa
hoặc thay đổi schema, miniapp so phiên bản dump với DB đích và dừng sớm nếu dump mới hơn.
Các dump cũ tạo với Oracle 21c ở chế độ `COMPATIBLE` phải được export lại; đổi cấu hình không thể
chuyển đổi một file `.dmp` đã tồn tại.
Khi đã kiểm tra DB đích và phát hiện lệch phiên bản, nút phục hồi bị khóa và màn hình cung cấp
**Tạo bản sao tương thích Oracle x.y**. Thao tác này export lại từ DB nguồn với chính phiên bản DB
đích (ví dụ phần mềm đích 19c nhưng `COMPATIBLE=12.2` thì dùng `VERSION=12.2`), tự chọn bản sao mới
và giữ nguyên file cũ.

Nếu một lần **Tạo schema mới** đã tạo user thành công nhưng import lỗi, schema có thể đã tồn tại và
vẫn trống. Khi kiểm tra lại, miniapp tự chuyển sang **Dùng schema trống đã có**; sử dụng đúng mật khẩu
schema vừa tạo để chạy lại sau khi đã có dump tương thích.

Miniapp cũng ghi và hiển thị phiên bản timezone file (TSTZ). Nếu TSTZ nguồn cao hơn đích, miniapp
dừng trước khi tạo hoặc thay đổi schema đích và yêu cầu DBA nâng timezone file của DB đích lên bằng
hoặc cao hơn nguồn. Tham số `VERSION` chỉ điều chỉnh định dạng Data Pump, không hạ timezone file nằm
trong dữ liệu. Miniapp không tự nâng cấp cấp database vì thao tác đó cần backup, lịch bảo trì và có
thể phải khởi động lại Oracle.
Kết quả import do trạng thái Data Pump quyết định; lỗi chỉ xảy ra khi tải/đóng file log cục bộ được hiển thị
thành cảnh báo và không làm một phiên import đã hoàn tất bị báo thất bại.

Thông tin kết nối được lưu tại `config/environments.json`; tài khoản được mã hóa tại
`state/oracle-credentials.protected`. Cả hai đều không được commit vào Git.

## Biến môi trường tùy chọn

Máy chủ hoặc CI có thể cấp tài khoản bằng biến môi trường. Các giá trị này có độ
ưu tiên cao hơn tài khoản lưu trên giao diện:

```powershell
$env:THUCLUC_OPS_SOURCE_DB_USER = "<source-user>"
$env:THUCLUC_OPS_SOURCE_DB_PASSWORD = "<source-password>"
$env:THUCLUC_OPS_TARGET_DB_USER = "<target-user>"
$env:THUCLUC_OPS_TARGET_DB_PASSWORD = "<target-password>"
```

Đây là biến môi trường của riêng cửa sổ PowerShell hiện tại. Thay phần trong
dấu `<...>` bằng giá trị thật. Khi đóng cửa sổ, các biến này mất đi. Không ghi
giá trị thật vào JSON hoặc Git.

Nếu đã lưu tài khoản trên giao diện thì không cần đặt các biến trên.

Không commit `config/environments.json`, `appsettings.Local.json`, file dump hoặc log vận hành.

## Tài liệu quy trình

Xem `docs/operations/database-deployment-backup-restore-runbook.md` từ root repository.
