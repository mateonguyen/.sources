# Thiet ke feature G2 - Che do nhap lieu TONG_HOP theo cau hinh don vi

## 1. Muc tieu va pham vi

Feature G2 bo sung hanh vi theo cau hinh `CheDoNhapLieu` cua don vi TINH/CUC:

- `TU_NHAP`: giu nguyen hanh vi hien tai.
- `TONG_HOP`: don vi cap con (PHONG/XA) nhap lieu + xac nhan; don vi TINH/CUC tong hop theo cay don vi, sau do nop bao cao.

Pham vi bao gom:

- UI + API cau hinh `CheDoNhapLieu` tren DonVi.
- Hanh vi tong hop tren luong nop bao cao va snapshot breakdown.
- Luong YeuCauBoSung chieu `TINH_XUONG_PHONG`.
- Integration test + E2E checklist.

Khong pham vi:

- Khong khoi phuc bang mini-snapshot da bi drop o V42.
- Khong thay doi mo hinh "live data + DaXacNhan" dang dung.

## 2. Rang buoc ky thuat

- Flyway la source of truth; migration moi neu can dat ten V76+.
- Oracle safety:
  - Tranh `AnyAsync` voi bool phuc tap tren duong query nong, uu tien `CountAsync(...) > 0`.
  - Date binding su dung pattern dang on dinh cua he thong.
- Khong pha regression luong `TU_NHAP`.
- Lam theo thu tu: thiet ke -> duyet -> code theo tung commit nho.

## 3. Hien trang va khoang trong

### 3.1 San co

- `DonVi.CheDoNhapLieu` da map (DB co cot `REF_DON_VI.CHE_DO_NHAP_LIEU`).
- `KyTrangThaiDonVi.DaXacNhan` da hoat dong cho cap duoi.
- `TongHopTienDoService` da co logic scope theo `ParentId` + dem so ban ghi module theo DonVi con.
- `BaoCaoSnapshot` khong con dung `BREAKDOWN_JSON`; PA3 chuyen breakdown sang `_HIS` + bang xac nhan snapshot.

### 3.2 Chua co

- Chua co cho nao doc `CheDoNhapLieu` de doi hanh vi nop bao cao.
- DonVi DTO/API/UI chua tra/nhan truong `CheDoNhapLieu`.
- Chua co luong YeuCauBoSung `TINH_XUONG_PHONG`.
- Chua co tai lieu nghiep vu cho xu ly khi don vi con sua du lieu sau khi tinh da nop.

## 4. De xuat thiet ke

## 4.1 Domain rule cho CheDoNhapLieu

Gia tri hop le:

- `TU_NHAP` (mac dinh)
- `TONG_HOP`

Rule validate:

- Chi don vi `TINH` hoac `CUC` duoc set `TONG_HOP`.
- Don vi cap `PHONG/XA` bat buoc `TU_NHAP`.
- Neu payload gui `TONG_HOP` cho cap khong hop le -> 422 `DONVI_CHE_DO_NHAP_LIEU_INVALID`.

## 4.2 DonVi API/UI

Backend:

- Mo rong `DonViDto` + `UpsertDonViRequest` them `CheDoNhapLieu`.
- `DonViService` map read/write + validate theo cap don vi.

Frontend:

- Man DonVi them dropdown `Che do nhap lieu`.
- Chi hien dropdown neu cap don vi dang chon la `TINH`/`CUC`.
- Neu cap khac: an field va payload mac dinh `TU_NHAP`.
- Gate quyen sua theo `don_vi:update` nhu cac field khac.

## 4.3 Nguon su that de quyet dinh hanh vi nop bao cao

Them helper service (Application layer), vi du: `IDonViInputModeService`:

- Input: `donViId`.
- Output:
  - `InputMode` (`TU_NHAP`|`TONG_HOP`)
  - Danh sach don vi con trong pham vi tong hop (children recursive).

Service nay duoc dung boi:

- KyBaoCao flow (man nop/canh bao).
- BaoCaoSnapshot submit/build breakdown.
- YeuCauBoSung flow theo cap gui.

## 4.4 Tong hop du lieu cho don vi `TONG_HOP`

### 4.4.1 Muc hien thi tren man nop bao cao

Khi don vi cha la `TONG_HOP`, hien them khoi:

- Tien do xac nhan cap duoi: `so don vi da xac nhan / tong don vi con`.
- Tong hop theo module: dem record theo module tu tat ca don vi con trong cay.

Tai su dung logic dem tu `TongHopTienDoService` qua mot query helper chung, tranh duplicate.

### 4.4.2 Nut Nop bao cao

Neu con don vi chua `DaXacNhan`:

- Van cho nop, nhung bat buoc confirm popup canh bao.
- Pattern UX giong canh bao module thieu du lieu hien co.

### 4.4.3 Khi submit snapshot

Khi don vi cha `TONG_HOP` submit:

- Tao `RPT_SNAPSHOT_BATCH` de chot 1 lan copy du lieu cho ca cha + cay don vi con.
- Copy du lieu live sang cac bang `_HIS` theo `SnapshotBatchId` hoac theo thoi gian batch khi bang HIS chua co cot batch rieng.
- Tao `RPT_SNAPSHOT_XAC_NHAN` de luu trang thai xac nhan cua tung don vi con tai thoi diem nop.

Luu y:

- Snapshot van la metadata-first; khong dua vao cot `SNAPSHOT_JSON` da drop.
- Breakdown khong luu JSON trong snapshot nua; client lay tu endpoint `/api/v1/snapshot/{id}/breakdown`.

### 4.4.4 Du lieu module trong bao cao/PDF

Quy uoc nghiep vu:

- Neu `TU_NHAP`: bao cao/PDF dung du lieu don vi do.
- Neu `TONG_HOP`: bao cao/PDF dung du lieu gop cua cha + toan bo con trong cay.

Trien khai:

- `BuildSnapshotJsonAsync` giu vai tro tao draft metadata cho man nhap lieu.
- `GetBreakdownAsync` doc tu `_HIS` va `RPT_SNAPSHOT_XAC_NHAN` de phuc vu UI tra cuu.
- Rendering PDF dung thong tin snapshot da nộp va co the doc breakdown tu cung batch chot neu can hien thi tong hop.

## 4.5 State model sau khi tinh nop va khi con sua du lieu

### 4.5.1 Sau khi tinh/CUC nop (TONG_HOP)

- Khong auto lock cac bang BIZ\_\* cua cap con (giu triet ly V39/V41).
- Snapshot cua tinh ghi nhan 1 thoi diem tong hop (immutable theo snapshot id/phien ban).
- Du lieu breakdown duoc chot vao batch HIS, khong con phu thuoc vao cot JSON trong snapshot.

### 4.5.2 Neu cap con sua du lieu sau khi tinh da nop

- Snapshot da nop khong doi.
- Tinh thay canh bao "du lieu cap con da thay doi sau lan nop gan nhat" khi mo man nop.
- Nguon canh bao:
  - so sanh `UpdatedAt` max cua du lieu cap con voi `SubmittedAt` snapshot active cua tinh.

### 4.5.3 Quan he voi MoLai/NopLai

- Giu flow mo-lai hien co:
  - `MoLai` tao draft moi, snapshot active cu -> superseded.
- Voi `TONG_HOP`, draft moi phai tai tao breakdown tu du lieu hien tai.

## 4.6 YeuCauBoSung chieu `TINH_XUONG_PHONG`

Mo rong service YeuCauBoSung:

- Tao yeu cau:
  - Neu nguoi gui thuoc don vi cha `TONG_HOP`, cho phep tao den don vi con voi `CAP_GUI = TINH_XUONG_PHONG`.
- Hien thi danh sach:
  - Don vi cha thay yeu cau do minh tao cho cap con.
  - Don vi con thay yeu cau gui den minh theo cap gui nay.
- Duyet/Tu choi/Hoan thanh:
  - Tai su dung state machine hien co, bo sung kiem tra pham vi theo `CAP_GUI`.
  - Don vi con sua du lieu + xac nhan lai -> yeu cau `HoanThanh`.

## 5. Ke hoach trien khai (theo commit)

Commit 1 (design + contract backend):

- Cap nhat DTO/request + validate DonVi `CheDoNhapLieu`.
- Khong doi hanh vi business submit.

Commit 2 (UI DonVi):

- Them field cau hinh tren man DonVi + gate quyen.

Commit 3 (aggregate core service):

- Tao service chung tinh aggregate theo cay don vi.
- Hook vao snapshot submit de ghi `BREAKDOWN_JSON` khi `TONG_HOP`.

Commit 4 (UI KyBaoCao/Tong hop tren man nop):

- Hien khoi tong hop + canh bao don vi con chua xac nhan.
- Confirm truoc khi nop neu con thieu.

Commit 5 (YeuCauBoSung TINH_XUONG_PHONG):

- Mo rong backend + UI theo cap gui.

Commit 6 (tests + docs update):

- Integration tests + E2E checklist + cap nhat tai lieu tong hop ket qua.

## 6. Test plan

## 6.1 Integration tests (bat buoc)

1. Don vi `TONG_HOP` submit:

- Setup: cha (CA_HN) mode `TONG_HOP`, co 2 con voi du lieu va trang thai xac nhan khac nhau.
- Assert:
  - submit thanh cong.
  - snapshot co `BREAKDOWN_JSON` hop le.
  - `children[].moduleCounts` dung so lieu.

2. Don vi `TU_NHAP` khong bi anh huong:

- Submit flow cu van pass.
- Khong bat buoc tao breakdown theo child.

3. Validation mode:

- XA/PHONG set `TONG_HOP` -> 422 `DONVI_CHE_DO_NHAP_LIEU_INVALID`.
- User khong co quyen update DonVi -> 403.

4. YeuCauBoSung TINH_XUONG_PHONG:

- Tao -> duyet -> cap con xac nhan lai -> `HoanThanh`.

## 6.2 E2E checklist

1. Cau hinh `CA_HN = TONG_HOP`.
2. `capxa.e2e` nhap lieu + xac nhan.
3. `donvi.user` thay khoi tong hop + nop thanh cong.
4. `h05.user` thay bao cao cua CA_HN co breakdown.
5. Chuyen lai `CA_HN = TU_NHAP` -> khoi tong hop bien mat.

### 7. Flyway va du lieu

PA3 can migration moi cho:

- `RPT_SNAPSHOT_XAC_NHAN`.
- neu can, bo sung index cho cac bang HIS chua co `SnapshotBatchId` de toi uu breakdown theo batch.

Bat dau tu V76+.

## 8. Update tai lieu tong ket

Sau khi code va verify xong, cap nhat:

- Muc A2.5 (hanh vi theo cau hinh nhap lieu)
- Muc B4.5 (ket qua verify G2)

Tai lieu dich: `docs/quy-trinh-va-ket-qua-test-luong-bao-cao.md`.
Neu workspace hien tai chua co file nay, tam thoi ghi vao `docs/ket-qua-test-e2e-luong-bao-cao.md` va migrate lai sau.

## 9. Rui ro va giam thieu

- Rui ro regression `TU_NHAP`: bao ve bang test regression hien co + test moi cho mode branch.
- Rui ro Oracle query plan khi aggregate lon: uu tien query don gian, bo sung index neu can.
- Rui ro khong dong nhat so lieu UI/PDF: dung chung mot aggregate provider cho ca 2 ben.
