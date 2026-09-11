# Cài Oracle XE 21c bằng MobaXterm

> Máy chủ đã kiểm tra là **Oracle Linux 9.8**, đang chạy Oracle 19.22. Không áp dụng
> quy trình gói OL7/OL8 dưới đây cho máy đó. Nếu bạn chỉ cần database con và service riêng
> trong Oracle 19 hiện tại, xem [Chọn database con và phục hồi bằng DbManager](oracle-pdb-selection-and-restore.md).

Tài liệu này dành cho người không chuyên vận hành hệ thống. Thực hiện lần lượt từng bước, không cần chạy thêm lệnh ngoài tài liệu.

Sau khi hoàn thành:

- Oracle 19 hiện tại vẫn sử dụng service `ORCL`, cổng `1521`.
- Oracle XE 21 mới sử dụng service `XEPDB1`, cổng `1522`.
- DbManager và backend kết nối Oracle mới qua `IP_MAY_CHU:1522/XEPDB1`.

> Không chạy lệnh `configure` trước Bước 5, nếu không Oracle XE sẽ cố dùng cổng `1521` đang thuộc Oracle 19.

## Bước 1 — Xem máy chủ là OL7 hay OL8

Mở MobaXterm, tạo SSH session và kết nối vào máy chủ.

Chạy:

```bash
grep -E '^(NAME|VERSION_ID)=' /etc/os-release
```

Đọc dòng `VERSION_ID`:

- Nếu bắt đầu bằng `7`: dùng hai file OL7.
- Nếu bắt đầu bằng `8`: dùng hai file OL8.

Nếu máy là OL7 mà bạn đã tải file OL8 thì không dùng file OL8; tải lại đúng bản OL7.

### Hai file dành cho OL8

- [Oracle XE 21c OL8](https://download.oracle.com/otn-pub/otn_software/db-express/oracle-database-xe-21c-1.0-1.ol8.x86_64.rpm)
- [Preinstall OL8](https://yum.oracle.com/repo/OracleLinux/OL8/appstream/x86_64/getPackage/oracle-database-preinstall-21c-1.0-1.el8.x86_64.rpm)

### Hai file dành cho OL7

- [Oracle XE 21c OL7](https://download.oracle.com/otn-pub/otn_software/db-express/oracle-database-xe-21c-1.0-1.ol7.x86_64.rpm)
- [Preinstall OL7](https://yum.oracle.com/repo/OracleLinux/OL7/latest/x86_64/getPackage/oracle-database-preinstall-21c-1.0-1.el7.x86_64.rpm)

`Preinstall` là gói chuẩn bị Linux cho Oracle. Cần cả file database và file preinstall.

## Bước 2 — Kiểm tra cổng và dung lượng

Chạy hai lệnh:

```bash
sudo ss -ltnp | grep -E ':1521|:1522|:5501' || true
df -h /opt
```

Ý nghĩa:

- Thấy `1521` là bình thường vì Oracle 19 đang dùng.
- Không được thấy `1522` vì cổng này dành cho Oracle 21.
- Không được thấy `5501` vì cổng này dành cho màn hình quản trị Oracle 21.
- Ở kết quả `df`, cột `Avail` của `/opt` nên còn tối thiểu khoảng `10G`. Càng nhiều càng tốt vì dữ liệu cũng được lưu ở đây.

Kiểm tra thêm:

```bash
sudo grep '^XE:' /etc/oratab || true
```

Không có kết quả là đúng. Nếu hiện ra dòng bắt đầu bằng `XE:` thì dừng lại vì máy đã từng có Oracle XE.

## Bước 3 — Upload hai file bằng MobaXterm

Trong cửa sổ SSH của MobaXterm, chạy:

```bash
mkdir -p /tmp/oracle-xe21
```

Ở khung file bên trái của MobaXterm:

1. Mở folder `/tmp/oracle-xe21` trên máy chủ.
2. Kéo thả hai file RPM từ máy trạm vào folder này.
3. Chờ upload hoàn tất.

Kiểm tra file đã lên đủ:

```bash
ls -lh /tmp/oracle-xe21
```

Phải thấy đúng hai file: một file `preinstall` và một file `oracle-database-xe`.

## Bước 4 — Cài phần mềm

Chỉ chạy nhóm lệnh đúng với phiên bản Linux ở Bước 1.

### Nếu máy chủ là OL8

```bash
sudo dnf -y localinstall /tmp/oracle-xe21/oracle-database-preinstall-21c-1.0-1.el8.x86_64.rpm
sudo dnf -y localinstall /tmp/oracle-xe21/oracle-database-xe-21c-1.0-1.ol8.x86_64.rpm
```

### Nếu máy chủ là OL7

```bash
sudo yum -y localinstall /tmp/oracle-xe21/oracle-database-preinstall-21c-1.0-1.el7.x86_64.rpm
sudo yum -y localinstall /tmp/oracle-xe21/oracle-database-xe-21c-1.0-1.ol7.x86_64.rpm
```

Hai lệnh này lần lượt:

1. Chuẩn bị Linux cho Oracle.
2. Cài phần mềm Oracle XE 21.

Nếu màn hình kết thúc với lỗi dependency/package thì dừng và gửi lại toàn bộ đoạn lỗi. Không chạy `configure`, không dùng `--nodeps`.

Nếu không có lỗi, kiểm tra:

```bash
rpm -q oracle-database-xe-21c
```

Kết quả phải có tên package `oracle-database-xe-21c`.

## Bước 5 — Chuyển Oracle XE sang cổng 1522

Copy nguyên hai lệnh sau:

```bash
sudo sed -i 's/^LISTENER_PORT=.*/LISTENER_PORT=1522/' /etc/sysconfig/oracle-xe-21c.conf
sudo sed -i 's/^EM_EXPRESS_PORT=.*/EM_EXPRESS_PORT=5501/' /etc/sysconfig/oracle-xe-21c.conf
```

Hai lệnh này chỉ sửa cổng của Oracle XE 21, không sửa Oracle 19.

Kiểm tra lại:

```bash
sudo grep -E '^(LISTENER_PORT|EM_EXPRESS_PORT)=' /etc/sysconfig/oracle-xe-21c.conf
```

Kết quả bắt buộc phải là:

```text
LISTENER_PORT=1522
EM_EXPRESS_PORT=5501
```

Nếu không đúng như trên thì chưa làm Bước 6.

## Bước 6 — Tạo database XE và PDB

Chạy:

```bash
sudo /etc/init.d/oracle-xe-21c configure
```

Chương trình yêu cầu nhập mật khẩu và nhập lại mật khẩu. Đây là mật khẩu ban đầu cho các tài khoản quản trị `SYS`, `SYSTEM` và `PDBADMIN`.

Khi gõ mật khẩu, terminal không hiện dấu `*`; đây là hành vi bình thường. Gõ xong nhấn Enter.

Quá trình này có thể mất vài phút. Chờ tới khi báo cấu hình database thành công.

## Bước 7 — Kiểm tra Oracle 19 và Oracle 21

Chạy:

```bash
sudo ss -ltnp | grep -E ':1521|:1522'
```

Kết quả phải có cả hai cổng:

- `1521`: Oracle 19.
- `1522`: Oracle XE 21.

Kiểm tra service của Oracle 21:

```bash
sudo -u oracle /opt/oracle/product/21c/dbhomeXE/bin/lsnrctl services
```

Trong kết quả cần thấy chữ `XEPDB1`.

Nếu Bước 6 hoặc Bước 7 có lỗi, lấy 100 dòng log cuối:

```bash
sudo tail -n 100 /opt/oracle/cfgtoollogs/dbca/XE/XE.log
```

## Bước 8 — Kiểm tra timezone của Oracle 21

Chạy:

```bash
sudo -u oracle bash -c 'export ORACLE_HOME=/opt/oracle/product/21c/dbhomeXE; export ORACLE_SID=XE; $ORACLE_HOME/bin/sqlplus / as sysdba'
```

Khi thấy dấu nhắc `SQL>`, copy lần lượt:

```sql
ALTER PLUGGABLE DATABASE XEPDB1 OPEN;
ALTER PLUGGABLE DATABASE XEPDB1 SAVE STATE;
ALTER SESSION SET CONTAINER = XEPDB1;
SHOW CON_NAME;
SELECT version FROM v$timezone_file;
EXIT;
```

Kết quả cần thấy:

- `CON_NAME` là `XEPDB1`.
- `VERSION` bằng hoặc lớn hơn `35` để restore bản dump hiện tại.

## Bước 9 — Mở cổng 1522

Nếu máy chủ sử dụng `firewalld`, chạy:

```bash
sudo firewall-cmd --permanent --add-port=1522/tcp
sudo firewall-cmd --reload
```

Nếu lệnh báo `command not found`, máy chủ không dùng `firewalld`; gửi thông báo đó cho người quản trị mạng để họ mở TCP `1522`.

Không cần mở cổng `5501` nếu không dùng màn hình EM Express.

## Bước 10 — Thử kết nối bằng SQL Developer

Tạo một connection mới:

```text
Hostname     = IP máy chủ Linux
Port         = 1522
Service name = XEPDB1
Username     = SYSTEM
Password     = mật khẩu đã nhập ở Bước 6
Role         = Default
```

Nếu `Test` báo `Success` thì Oracle XE 21 đã cài thành công.

## Bước 11 — Kết nối DbManager và restore

Trong DbManager, lưu DB đích:

```text
Máy chủ     = IP máy chủ Linux
Port        = 1522
Service/PDB = XEPDB1
```

Sau đó:

1. Bấm **Tải file .dmp** hoặc chọn bản backup có sẵn.
2. Chọn DB đích vừa lưu.
3. Nhập schema đích.
4. Chọn **Tạo schema mới** nếu schema chưa tồn tại.
5. Nhập mật khẩu `SYS` của XE 21 và mật khẩu schema mới.
6. Bấm **Kiểm tra DB đích**.
7. Nếu đúng Oracle 21, `XEPDB1` và TSTZ phù hợp, bấm **Bắt đầu phục hồi**.

Backend không dùng tài khoản `SYS`. Backend dùng tài khoản schema ứng dụng và kết nối vào:

```text
IP_MAY_CHU:1522/XEPDB1
```

## Sau khi cài xong

Cho Oracle XE tự khởi động cùng máy chủ:

```bash
sudo systemctl enable oracle-xe-21c
```

Khi cần xem trạng thái:

```bash
sudo systemctl status oracle-xe-21c --no-pager
```

Không cần xóa hai file trong `/tmp` ngay. Hệ điều hành có thể tự dọn folder này sau.

Nguồn chính thức:

- [Oracle XE 21c Downloads](https://www.oracle.com/database/technologies/xe-downloads.html)
- [Hướng dẫn cài Oracle XE 21c trên Linux](https://docs.oracle.com/en/database/oracle/oracle-database/21/xeinl/installing-oracle-database-xe.html)
