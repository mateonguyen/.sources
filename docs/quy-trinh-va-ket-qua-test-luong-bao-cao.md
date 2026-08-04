# Quy trinh va ket qua test luong bao cao

## A2.5 - Che do nhap lieu theo cau hinh don vi (G2)

### Muc tieu

Bo sung che do nhap lieu theo cau hinh don vi TINH/CUC:

- TU_NHAP: don vi tu nhap du lieu nhu luong hien tai.
- TONG_HOP: don vi cap tren tong hop du lieu module tu cay don vi cap duoi, sau do nop bao cao.

### Thiet ke va trien khai

- DonVi API/UI:
  - DonVi DTO va Upsert contract ho tro truong cheDoNhapLieu.
  - Validate backend: chi TINH/CUC duoc set TONG_HOP; cap PHONG/XA bi chan voi ma loi DONVI_CHE_DO_NHAP_LIEU_INVALID (422).
  - UI DonVi hien dropdown Che do nhap lieu khi cap don vi la TINH/CUC; cap khac auto ve TU_NHAP.
- Snapshot submit context:
  - Bo sung endpoint lay submit context cho man Nop bao cao.
  - Context gom cheDoNhapLieu, tong so don vi con, so don vi da xac nhan, co/khong don vi con chua xac nhan, va co/khong du lieu cap con thay doi sau lan nop truoc.
- Submit trong che do TONG_HOP:
  - Cho phep nop khi con don vi chua xac nhan nhung bat buoc nguoi dung xac nhan canh bao.
  - Khi nop tao `RPT_SNAPSHOT_BATCH` va luu `RPT_SNAPSHOT_XAC_NHAN` cho tung don vi con.
  - Breakdown duoc doc lai tu endpoint `/api/v1/snapshot/{id}/breakdown`, khong con doc tu cot JSON trong snapshot.
- Tong hop module status:
  - Module status tren man Nop bao cao dem du lieu theo aggregate scope (don vi cha + cay don vi con) khi che do la TONG_HOP.
- Yeu cau bo sung chieu TINH_XUONG_PHONG:
  - Bo sung xac dinh CAP_GUI theo quan he cha-con va che do nhap lieu.
  - Tai su dung state machine hien co ChoDuyet -> DangBoSung -> HoanThanh/TuChoi.

### Quy uoc du lieu

- Khong su dung lai mo hinh mini-snapshot da bi loai bo.
- Tiep tuc mo hinh live data + DaXacNhan.
- Breakdown duoc chot vao `_HIS` + bang xac nhan, giup truy vet tong hop theo batch nop.

## B4.5 - Ket qua verify G2

### Backend verify

- DonVi:
  - Da co test chan cap PHONG/XA set TONG_HOP (422, DONVI_CHE_DO_NHAP_LIEU_INVALID).
- Snapshot submit:
  - Da co test yeu cau force khi TONG_HOP co don vi con chua xac nhan.
  - Bo sung test xac nhan BREAKDOWN_JSON duoc ghi voi child moduleCounts trong che do TONG_HOP.
    - Bo sung test TU_NHAP khong bi anh huong: submit thanh cong khong can force va breakdown endpoint tra ve rỗng.
    - Bo sung test breakdown endpoint tra ve `totalChildren`, `confirmedChildren`, va danh sach con tu `RPT_SNAPSHOT_XAC_NHAN`.

### Frontend verify

- Man DonVi hien/ghi cheDoNhapLieu theo cap don vi.
- Man Nop bao cao:
  - Hien thong tin submit context cho TONG_HOP.
  - Hien canh bao khi con don vi con chua xac nhan.
  - Gui co forceSubmitWhenChildrenUnconfirmed khi nguoi dung xac nhan nop.

### Luu y van hanh

- Truong hop build backend bi lock file la do process dotnet watch/dotnet run dang giu DLL output.
- Can dung process backend truoc khi build lai khi gap MSB3021/MSB3027.
