export interface SelectOption<TValue extends string | number | boolean | null> {
  label: string;
  value: TValue;
}

export interface NhanLucCnttItem {
  id: number;
  donViId: number;
  donViTen?: string | null;
  donViCongTacId?: number | null;
  donViCongTacTen?: string | null;
  hoTen: string;
  ngaySinh?: string | null;
  gioiTinh?: string | null;
  capBac?: string | null;
  chucVu?: string | null;
  dienThoai?: string | null;
  loaiNhanLuc?: string | null;
  trinhDoCntt?: string | null;
  trinhDoLlct?: string | null;
  chuyenNganh?: string | null;
  hocVi?: string | null;
  hocHam?: string | null;
  ghiChu?: string | null;
  trangThaiCapNhat?: string | null;
  namBaoCao?: number | null;
}

export interface NhanLucCnttSearchParams {
  page: number;
  pageSize: number;
  tuKhoa?: string;
  namBaoCao?: number | null;
  donViCongTacId?: number | null;
  gioiTinh?: string | null;
  capBac?: string | null;
  loaiNhanLuc?: string | null;
  trinhDoCntt?: string | null;
}

export interface PagedResult<TItem> {
  items: TItem[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface NhanLucCnttFilterValue {
  tuKhoa: string;
  namBaoCao: number | null;
  donViCongTacId: number | null;
  gioiTinh: string | null;
  capBac: string | null;
  loaiNhanLuc: string | null;
  trinhDoCntt: string | null;
}

export interface NhanLucCnttTableRow {
  id: number;
  hoTen: string;
  hoTenDayDu: string;
  capBacText: string;
  chucVuText: string;
  donViCongTacText: string;
  loaiNhanLucText: string;
  trinhDoCnttText: string;
  dienThoaiText: string;
  gioiTinhText: string;
  ngaySinhText: string;
  raw: NhanLucCnttItem;
}

export interface NhanLucCnttUpsertInitialData {
  id?: number;
  donViId: number | null;
  donViCongTacId: number | null;
  hoTen: string;
  ngaySinh: string;
  gioiTinh: string | null;
  capBac: string | null;
  chucVu: string;
  dienThoai: string;
  loaiNhanLuc: string | null;
  trinhDoCntt: string | null;
  trinhDoLlct: string | null;
  ghiChu: string;
}

export interface NhanLucCnttUpsertPayload {
  donViId: number;
  donViCongTacId?: number | null;
  hoTen: string;
  ngaySinh?: string | null;
  gioiTinh?: string | null;
  capBac?: string | null;
  chucVu?: string | null;
  dienThoai?: string | null;
  loaiNhanLuc?: string | null;
  trinhDoCntt?: string | null;
  trinhDoLlct?: string | null;
  ghiChu?: string | null;
}

export interface NhanLucCnttUpsertSubmitEvent {
  id?: number;
  payload: NhanLucCnttUpsertPayload;
  keepDialogOpen: boolean;
}

export const NHAN_LUC_CNTT_PAGE_SIZE_OPTIONS: ReadonlyArray<
  SelectOption<number>
> = [
  { label: '10', value: 10 },
  { label: '20', value: 20 },
  { label: '50', value: 50 },
];
