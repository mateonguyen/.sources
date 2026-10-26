# Tác vụ biến động đơn vị cho quản trị hệ thống

## Đã triển khai

- Màn **Đơn vị → Biến động đơn vị và tài khoản** dành cho `SYSTEM_ADMIN` có sáp nhập, chia tách, đổi đơn vị tài khoản và xem lịch sử. Mỗi tác vụ có bước xem trước và xác nhận; API kiểm tra lại trong giao dịch trước khi thực hiện.
- Sáp nhập giữ mã và hồ sơ đơn vị nguồn, chuyển đơn vị con và tài khoản đang hoạt động sang đơn vị kế thừa, rồi ngừng hoạt động nguồn. Chia tách yêu cầu chọn rõ đích cho từng đơn vị con và tài khoản; chỉ ngừng nguồn khi không còn đơn vị con, tài khoản đang hoạt động hay báo cáo nháp.
- Báo cáo đã nộp/khóa tiếp tục thuộc đơn vị gốc. Đơn vị kế thừa tra cứu và xuất báo cáo tiền nhiệm đã nộp **trước** thời điểm biến động. Bản nháp và dữ liệu nhập mới không được mở rộng quyền theo quan hệ kế thừa. Phiên đăng nhập của tài khoản được chuyển đơn vị sẽ hết hiệu lực ngay.
- Dữ liệu nghiệp vụ đang nhập ở đơn vị nguồn không tự chuyển sang đơn vị đích; dữ liệu kỳ cũ đọc qua báo cáo đã chốt. Cần chốt hoặc xử lý bản nháp trước khi biến động. Nếu cần tiếp tục một hồ sơ nghiệp vụ dở dang tại đơn vị mới, phải xử lý riêng theo quy tắc của module đó.
- Flyway `V100` và `V101` tạo lịch sử phiên bản đơn vị, quan hệ kế thừa và lịch sử chuyển tài khoản. Cần chạy migration trên Oracle trước khi dùng màn này. Các tên/cây đơn vị trước thời điểm `V100` chỉ có thể khởi tạo theo trạng thái tại lúc migration; hệ thống không thể tái tạo những lần đổi tên đã xảy ra trước đó nếu không có nguồn lịch sử bên ngoài.

## Ghi chú thiết kế ban đầu (lưu để đối chiếu)

## Hiện trạng

- Màn Đơn vị cho phép sửa `ParentId`, tên, mã và trạng thái của từng đơn vị. Thao tác này chưa xử lý các bảng nghiệp vụ liên quan.
- Dữ liệu gắn với đơn vị trải trên người dùng, phân vai trò, danh mục theo đơn vị, 17 nhóm nghiệp vụ và bảng lịch sử của chúng, trạng thái kỳ, snapshot, xác nhận, tệp và yêu cầu bổ sung. Quan hệ với đơn vị ở các bảng báo cáo dùng `Restrict`; không thể xóa đơn vị cũ khi vẫn còn dữ liệu.
- Vì vậy cần một tác vụ riêng ở máy chủ. Một nút trên giao diện chỉ gửi yêu cầu đã kiểm tra; không cập nhật nhiều bảng từ trình duyệt.

## Luồng đề xuất

1. Quản trị viên chọn loại biến động, đơn vị nguồn, đơn vị đích, ngày hiệu lực và phạm vi chuyển.
2. API xem trước trả về số bản ghi theo từng nhóm, các kỳ báo cáo liên quan, xung đột mã/khóa duy nhất, tài khoản bị ảnh hưởng và trạng thái đơn vị sau thao tác. Không thực thi nếu có xung đột chưa được xử lý.
3. Quản trị viên xác nhận một lần. API thực thi trong một transaction Oracle; toàn bộ cập nhật thành công hoặc toàn bộ được rollback.
4. Ghi nhật ký gồm người thao tác, thời điểm, tham số, bản xem trước, số bản ghi thực tế và mã tác vụ. Từ mã này có thể tra cứu kết quả và kiểm toán.

## Quy tắc cần chốt trước khi làm API thực thi

- **Chuyển đơn vị**: chỉ đổi cấp trên trong cây, hay chuyển cả người dùng và dữ liệu nghiệp vụ sang đơn vị đích?
- **Sáp nhập**: đơn vị nguồn được ngừng hoạt động hay xóa? Con trực thuộc chuyển sang đơn vị đích hay giữ cây cũ?
- **Lịch sử**: snapshot, bản ghi lịch sử và các kỳ đã khóa giữ nguyên đơn vị nguồn hay chuyển sang đơn vị đích? Nếu giữ nguyên, cần duy trì đơn vị nguồn để tra cứu và báo cáo cũ.
- **Bản ghi trùng**: khi cùng mã nghiệp vụ ở nguồn và đích, ưu tiên bản nào, gộp trường nào, hay chặn tác vụ để quản trị viên xử lý?
- **Ngày hiệu lực**: áp dụng ngay hay cho phép đặt ngày trong tương lai? Nếu có ngày tương lai, cần hàng đợi thực thi và kiểm tra lại xung đột tại thời điểm chạy.

## Điều kiện kỹ thuật tối thiểu

- Chỉ `SYSTEM_ADMIN` được xem trước và thực thi; kiểm tra quyền ở API.
- Chặn nguồn trùng đích, vòng lặp cây đơn vị, đơn vị không tồn tại hoặc không hoạt động, và tác vụ trùng đang chạy.
- Bản xem trước có mã phiên bản dữ liệu để phát hiện thay đổi giữa lúc xem trước và xác nhận.
- Kiểm tra khóa ngoại và các ràng buộc duy nhất của từng module trước khi cập nhật; không dùng một lệnh cập nhật `DonViId` chung cho mọi bảng.
- Kiểm thử tích hợp Oracle với dữ liệu có snapshot đã khóa, lịch sử module, người dùng và con trực thuộc; xác nhận rollback khi một nhóm thất bại.
