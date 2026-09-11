# Chọn database con và phục hồi dữ liệu bằng DbManager

## Hiểu đúng máy chủ hiện tại

Theo kết quả đã kiểm tra trên máy chủ `10.10.79.248`:

- `ORCL19C` là tên instance Oracle đang chạy. Không cần đổi tên này.
- `orcl` là service dẫn vào `CDB$ROOT`, nơi quản trị chung.
- `bhxhpdb` là service dẫn vào database con `BHXHPDB`.
- Schema là tài khoản sở hữu các bảng, được tạo **bên trong** database con.

Bạn có thể tạo database con `CAND_QLCNTT`, rồi tạo schema cũng tên `CAND_QLCNTT`
bên trong. Hai tên giống nhau để dễ nhớ, nhưng là hai thứ khác nhau.
Một database con cũng có thể chứa nhiều schema; không bắt buộc mỗi schema phải có một database con.

Oracle tự tạo service mặc định cùng tên database con. DbManager đọc lại tên service thực tế
(có thể kèm tên miền) rồi thử kết nối trước khi lưu làm đích.
Xem [tài liệu tạo PDB của Oracle 19](https://docs.oracle.com/en/database/oracle/oracle-database/19/multi/creating-a-pdb-from-scratch.html).

Việc này không đổi service `orcl`, không đổi cổng `1521`, không xóa hoặc đổi các database con hiện có.
Database con mới vẫn dùng chung bộ nhớ, CPU và ổ đĩa của Oracle 19 đang chạy, không phải một máy chủ riêng.

## Thao tác trên miniapp

1. Mở **Sao lưu & phục hồi** → **Kết nối đích**.
2. Nhập máy chủ `10.10.79.248`, port `1521`, service đang đăng nhập được là `orcl`.
   Đây là kết nối để tìm/tạo database con; chưa dùng nó để phục hồi schema.
3. Mở **Chọn / tạo database con (PDB)**, nhập mật khẩu SYS đã đổi thành công,
   rồi bấm kiểm tra/tải danh sách.
4. Chọn một trong hai cách:
   - **Dùng database con có sẵn**: chọn đúng nơi dành cho ứng dụng này, rồi bấm dùng database con đó.
     Đừng chọn `BHXHPDB`, `KEYCLOAKPDB`… chỉ vì thấy chúng đang chạy; đó có thể là dữ liệu của ứng dụng khác.
   - **Tạo database con mới**: nhập tên `CAND_QLCNTT`. App gợi ý theo schema bạn đã nhập
     hoặc schema trong bản sao đang chọn; bạn có thể sửa tên trước khi tạo.
5. Với tạo mới, kiểm tra chỗ lưu file và giới hạn dữ liệu hiển thị, đánh dấu xác nhận, rồi bấm tạo.
   Chỉ bấm nút tạo mới thực sự thay đổi Oracle; kiểm tra và chọn chế độ không tạo gì.
6. Chờ thông báo **đã lưu service làm kết nối đích**. Ô service sẽ chuyển từ `orcl` sang service của
   database con bạn chọn/tạo. Không phải sửa listener, đổi SID hay mở thêm cổng.
7. Xuống phần **Phục hồi vào DB đích**, chọn bản sao và nhập schema `CAND_QLCNTT`.
   Nếu đây là database con vừa tạo, chọn **Tạo schema mới**, nhập SYS và mật khẩu schema,
   bấm **Kiểm tra DB đích**, rồi xác nhận phục hồi.

Tạo database con thành công **chưa phải** phục hồi thành công. App vẫn cần tạo schema và import bản sao ở bước 7.
SYS chỉ được dùng trong phiên thao tác, không được lưu vào cấu hình.

## Nếu app hỏi thư mục lưu file

Nếu Oracle đã có cấu hình tự quản lý vị trí file (`DB_CREATE_FILE_DEST`) hoặc quy tắc
`PDB_FILE_NAME_CONVERT`, app dùng cấu hình đó.
Bạn không cần nhập đường dẫn khác.

Nếu chưa có, app yêu cầu **một thư mục đã tồn tại trên máy chủ Oracle**, có đủ dung lượng và
user Linux `oracle` được phép ghi vào. Đây không phải thư mục trên máy trạm chạy trình duyệt,
cũng không phải thư mục tải file `.dmp`.

Không lấy đường dẫn Oracle Home hay đường dẫn file `.dbf` để điền. Nhờ người quản trị máy chủ
chuẩn bị/xác nhận thư mục dành cho dữ liệu mới, rồi nhập đường dẫn đó một lần vào app.
App không sửa chỗ lưu dữ liệu của toàn bộ Oracle và không tự xóa file.

Khi cần tạo tablespace `USERS`, app cấp ban đầu 100 MB, cho phép tự tăng đến giới hạn bạn chọn
(mặc định 2 GB). Đây chỉ là phần chứa bảng ứng dụng, **không phải tổng dung lượng database con**:
Oracle còn sao chép các file hệ thống từ mẫu có sẵn. Nếu mẫu đã có `USERS`, app giữ cấu hình của nó
và thông báo thay vì tự đổi kích thước. Kiểm tra ổ đĩa trước khi tạo vì máy chủ từng bị đầy ổ.

## Khi gặp lỗi

- **Kết nối vào CDB$ROOT**: chưa chọn/lưu database con làm đích. Quay lại phần Kết nối đích.
- **ORA-50201 ở bước kết nối**: máy chạy DbManager không tới được host/port/service Oracle hoặc
  listener không phản hồi. Lệnh tạo PDB chưa được gửi; kiểm tra mạng/VPN, cổng `1521` và listener.
- **Tên đã tồn tại**: tải lại danh sách và kiểm tra. App không ghi đè hoặc xóa database con trùng tên.
- **Database con chưa mở để ghi**: nhờ quản trị viên kiểm tra. App không tự mở/đóng database con có sẵn.
- **Tạo xong nhưng kết nối/lưu cấu hình lỗi**: database con có thể đã tồn tại. Kiểm tra danh sách trước khi
  thử lại; không xóa database con chỉ để bấm tạo lại. Nếu Oracle báo trạng thái không sử dụng được,
  cần quản trị viên xem lỗi tạo và file log.
- **TSTZ nguồn cao hơn đích**: tạo database con không tự nâng timezone file. App vẫn chặn bản sao
  không tương thích trước khi tạo/thay thế schema. Cần kiểm tra TSTZ thực tế của database con mới.

App tạo tài khoản quản trị cục bộ kỹ thuật `PDBADMIN` với mật khẩu ngẫu nhiên và khóa tài khoản đó;
không dùng nó làm schema ứng dụng. Bạn tiếp tục quản trị bằng SYS. App không tự xóa database con
nếu một bước chuẩn bị sau đó thất bại.

## Phạm vi đã kiểm thử

Chức năng này cần được xác nhận trên Oracle thật trong mạng nội bộ trước khi dùng cho dữ liệu chính thức.
Build/kiểm thử mã nguồn không chứng minh máy chủ còn đủ dung lượng, quyền ghi thư mục,
listener đã đăng ký service hay phiên bản TSTZ đã tương thích.
