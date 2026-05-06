import {
  NhanLucCnttItem,
  NhanLucCnttSearchParams,
  NhanLucCnttUpsertPayload,
  PagedResult,
} from './nhan-luc-cntt.models';

const DON_VI_LABELS = new Map<number, string>([
  [1, 'Cục Công nghệ thông tin'],
  [2, 'Phòng Quản trị hệ thống'],
  [3, 'Phòng An toàn thông tin'],
  [4, 'Phòng Phát triển ứng dụng'],
  [5, 'Trung tâm Dữ liệu'],
  [6, 'Đội Hạ tầng mạng'],
]);

const SEED_DATA: NhanLucCnttItem[] = [
  {
    id: 101,
    donViId: 1,
    donViTen: 'Cục Công nghệ thông tin',
    donViCongTacId: 2,
    donViCongTacTen: 'Phòng Quản trị hệ thống',
    hoTen: 'Nguyễn Minh Khánh',
    ngaySinh: '1989-04-18',
    gioiTinh: 'NAM',
    capBac: 'THIEU_TA',
    chucVu: 'Phó trưởng phòng',
    dienThoai: '0988111222',
    loaiNhanLuc: 'CHUYEN_TRACH',
    trinhDoCntt: 'DAI_HOC',
    trinhDoLlct: 'CAO_CAP',
    chuyenNganh: 'Hệ thống thông tin',
    hocVi: 'Thạc sĩ',
    hocHam: null,
    ghiChu: 'Phụ trách hạ tầng máy chủ và giám sát trung tâm dữ liệu.',
    trangThaiCapNhat: 'Đã cập nhật',
    namBaoCao: 2025,
  },
  {
    id: 102,
    donViId: 1,
    donViTen: 'Cục Công nghệ thông tin',
    donViCongTacId: 3,
    donViCongTacTen: 'Phòng An toàn thông tin',
    hoTen: 'Trần Thu Hà',
    ngaySinh: '1992-09-07',
    gioiTinh: 'NU',
    capBac: 'DAI_UY',
    chucVu: 'Chuyên viên phân tích',
    dienThoai: '0912333444',
    loaiNhanLuc: 'KIEM_NHIEM',
    trinhDoCntt: 'THAC_SI',
    trinhDoLlct: 'TRUNG_CAP',
    chuyenNganh: 'An toàn thông tin',
    hocVi: 'Thạc sĩ',
    hocHam: null,
    ghiChu: 'Theo dõi an toàn hệ thống thư điện tử công vụ.',
    trangThaiCapNhat: 'Đã cập nhật',
    namBaoCao: 2025,
  },
  {
    id: 103,
    donViId: 1,
    donViTen: 'Cục Công nghệ thông tin',
    donViCongTacId: 4,
    donViCongTacTen: 'Phòng Phát triển ứng dụng',
    hoTen: 'Lê Quốc Hưng',
    ngaySinh: '1987-01-22',
    gioiTinh: 'NAM',
    capBac: 'TRUNG_TA',
    chucVu: 'Trưởng nhóm phát triển',
    dienThoai: '0977666555',
    loaiNhanLuc: 'CHUYEN_TRACH',
    trinhDoCntt: 'THAC_SI',
    trinhDoLlct: 'CAO_CAP',
    chuyenNganh: 'Kỹ thuật phần mềm',
    hocVi: 'Thạc sĩ',
    hocHam: null,
    ghiChu: 'Chịu trách nhiệm nghiệp vụ nền tảng dịch vụ dùng chung.',
    trangThaiCapNhat: 'Đã cập nhật',
    namBaoCao: 2025,
  },
  {
    id: 104,
    donViId: 1,
    donViTen: 'Cục Công nghệ thông tin',
    donViCongTacId: 5,
    donViCongTacTen: 'Trung tâm Dữ liệu',
    hoTen: 'Phạm Văn Bình',
    ngaySinh: '1990-12-03',
    gioiTinh: 'NAM',
    capBac: 'THUONG_TA',
    chucVu: 'Kỹ sư hệ thống',
    dienThoai: '0909555666',
    loaiNhanLuc: 'CHUYEN_TRACH',
    trinhDoCntt: 'DAI_HOC',
    trinhDoLlct: 'SO_CAP',
    chuyenNganh: 'Mạng máy tính',
    hocVi: 'Kỹ sư',
    hocHam: null,
    ghiChu: 'Vận hành cụm ảo hóa và hệ thống sao lưu dữ liệu.',
    trangThaiCapNhat: 'Cần rà soát',
    namBaoCao: 2024,
  },
  {
    id: 105,
    donViId: 1,
    donViTen: 'Cục Công nghệ thông tin',
    donViCongTacId: 6,
    donViCongTacTen: 'Đội Hạ tầng mạng',
    hoTen: 'Đỗ Mai Phương',
    ngaySinh: '1994-06-14',
    gioiTinh: 'NU',
    capBac: 'THIEU_UY',
    chucVu: 'Cán bộ mạng',
    dienThoai: '0933444555',
    loaiNhanLuc: 'HOP_DONG',
    trinhDoCntt: 'CAO_DANG',
    trinhDoLlct: 'SO_CAP',
    chuyenNganh: 'Quản trị mạng',
    hocVi: 'Cử nhân',
    hocHam: null,
    ghiChu: 'Theo dõi cấu hình switch lõi và kết nối WAN.',
    trangThaiCapNhat: 'Chờ bổ sung',
    namBaoCao: 2024,
  },
  {
    id: 106,
    donViId: 1,
    donViTen: 'Cục Công nghệ thông tin',
    donViCongTacId: 3,
    donViCongTacTen: 'Phòng An toàn thông tin',
    hoTen: 'Vũ Thanh Tùng',
    ngaySinh: '1988-11-30',
    gioiTinh: 'NAM',
    capBac: 'DAI_TA',
    chucVu: 'Tổ trưởng giám sát',
    dienThoai: '0983222111',
    loaiNhanLuc: 'CHUYEN_TRACH',
    trinhDoCntt: 'TIEN_SI',
    trinhDoLlct: 'CAO_CAP',
    chuyenNganh: 'An ninh mạng',
    hocVi: 'Tiến sĩ',
    hocHam: 'Phó giáo sư',
    ghiChu: 'Phụ trách trực SOC và phối hợp ứng cứu sự cố.',
    trangThaiCapNhat: 'Đã cập nhật',
    namBaoCao: 2023,
  },
  {
    id: 107,
    donViId: 1,
    donViTen: 'Cục Công nghệ thông tin',
    donViCongTacId: 4,
    donViCongTacTen: 'Phòng Phát triển ứng dụng',
    hoTen: 'Ngô Thị Lan',
    ngaySinh: '1995-03-27',
    gioiTinh: 'NU',
    capBac: 'THUONG_UY',
    chucVu: 'Lập trình viên',
    dienThoai: '0944555777',
    loaiNhanLuc: 'KIEM_NHIEM',
    trinhDoCntt: 'DAI_HOC',
    trinhDoLlct: 'TRUNG_CAP',
    chuyenNganh: 'Công nghệ phần mềm',
    hocVi: 'Cử nhân',
    hocHam: null,
    ghiChu: 'Phát triển các biểu mẫu điện tử dùng trong nội bộ.',
    trangThaiCapNhat: 'Đã cập nhật',
    namBaoCao: 2025,
  },
  {
    id: 108,
    donViId: 1,
    donViTen: 'Cục Công nghệ thông tin',
    donViCongTacId: 2,
    donViCongTacTen: 'Phòng Quản trị hệ thống',
    hoTen: 'Bùi Hải Nam',
    ngaySinh: '1991-08-11',
    gioiTinh: 'NAM',
    capBac: 'DAI_UY',
    chucVu: 'Quản trị cơ sở dữ liệu',
    dienThoai: '0966111777',
    loaiNhanLuc: 'CHUYEN_TRACH',
    trinhDoCntt: 'THAC_SI',
    trinhDoLlct: 'TRUNG_CAP',
    chuyenNganh: 'Cơ sở dữ liệu',
    hocVi: 'Thạc sĩ',
    hocHam: null,
    ghiChu: 'Quản lý sao lưu CSDL nghiệp vụ và tối ưu truy vấn.',
    trangThaiCapNhat: 'Đã cập nhật',
    namBaoCao: 2024,
  },
  {
    id: 109,
    donViId: 1,
    donViTen: 'Cục Công nghệ thông tin',
    donViCongTacId: 5,
    donViCongTacTen: 'Trung tâm Dữ liệu',
    hoTen: 'Hoàng Thị Nhung',
    ngaySinh: '1993-02-05',
    gioiTinh: 'NU',
    capBac: 'THIEU_TA',
    chucVu: 'Kỹ sư lưu trữ',
    dienThoai: '0922111333',
    loaiNhanLuc: 'HOP_DONG',
    trinhDoCntt: 'DAI_HOC',
    trinhDoLlct: 'SO_CAP',
    chuyenNganh: 'Hệ thống lưu trữ',
    hocVi: 'Cử nhân',
    hocHam: null,
    ghiChu: 'Theo dõi dung lượng SAN và kiểm tra sao lưu định kỳ.',
    trangThaiCapNhat: 'Cần rà soát',
    namBaoCao: 2023,
  },
  {
    id: 110,
    donViId: 1,
    donViTen: 'Cục Công nghệ thông tin',
    donViCongTacId: 6,
    donViCongTacTen: 'Đội Hạ tầng mạng',
    hoTen: 'Nguyễn Đức Thành',
    ngaySinh: '1986-07-19',
    gioiTinh: 'NAM',
    capBac: 'TRUNG_TA',
    chucVu: 'Đội trưởng',
    dienThoai: '0911222333',
    loaiNhanLuc: 'CHUYEN_TRACH',
    trinhDoCntt: 'THAC_SI',
    trinhDoLlct: 'CAO_CAP',
    chuyenNganh: 'Hạ tầng mạng',
    hocVi: 'Thạc sĩ',
    hocHam: null,
    ghiChu: 'Chịu trách nhiệm quy hoạch hạ tầng mạng toàn cục.',
    trangThaiCapNhat: 'Đã cập nhật',
    namBaoCao: 2025,
  },
  {
    id: 111,
    donViId: 1,
    donViTen: 'Cục Công nghệ thông tin',
    donViCongTacId: 4,
    donViCongTacTen: 'Phòng Phát triển ứng dụng',
    hoTen: 'Mai Văn Dũng',
    ngaySinh: '1992-05-29',
    gioiTinh: 'NAM',
    capBac: 'THUONG_UY',
    chucVu: 'Kiểm thử phần mềm',
    dienThoai: '0908777999',
    loaiNhanLuc: 'KIEM_NHIEM',
    trinhDoCntt: 'CAO_DANG',
    trinhDoLlct: 'TRUNG_CAP',
    chuyenNganh: 'Kiểm thử phần mềm',
    hocVi: 'Cử nhân',
    hocHam: null,
    ghiChu: 'Phụ trách kiểm thử hồi quy cho các đợt phát hành.',
    trangThaiCapNhat: 'Chờ bổ sung',
    namBaoCao: 2023,
  },
  {
    id: 112,
    donViId: 1,
    donViTen: 'Cục Công nghệ thông tin',
    donViCongTacId: 3,
    donViCongTacTen: 'Phòng An toàn thông tin',
    hoTen: 'Nguyễn Thị Hồng',
    ngaySinh: '1996-10-16',
    gioiTinh: 'NU',
    capBac: 'THIEU_UY',
    chucVu: 'Chuyên viên SOC',
    dienThoai: '0955333777',
    loaiNhanLuc: 'CHUYEN_TRACH',
    trinhDoCntt: 'DAI_HOC',
    trinhDoLlct: 'SO_CAP',
    chuyenNganh: 'An toàn thông tin',
    hocVi: 'Cử nhân',
    hocHam: null,
    ghiChu: 'Tham gia trực giám sát nhật ký và điều phối cảnh báo.',
    trangThaiCapNhat: 'Đã cập nhật',
    namBaoCao: 2024,
  },
];

export class NhanLucCnttMockRepository {
  private items = SEED_DATA.map((item) => ({ ...item }));
  private nextId = Math.max(...SEED_DATA.map((item) => item.id)) + 1;

  async search(
    params: NhanLucCnttSearchParams,
  ): Promise<PagedResult<NhanLucCnttItem>> {
    const page = Math.max(1, params.page || 1);
    const pageSize = Math.max(1, params.pageSize || 10);
    const keyword = normalizeText(params.tuKhoa ?? '');

    const filtered = this.items
      .filter((item) => {
        if (
          params.namBaoCao &&
          item.namBaoCao &&
          item.namBaoCao !== params.namBaoCao
        ) {
          return false;
        }

        if (
          params.donViCongTacId &&
          item.donViId !== params.donViCongTacId &&
          item.donViCongTacId !== params.donViCongTacId
        ) {
          return false;
        }

        if (params.gioiTinh && item.gioiTinh !== params.gioiTinh) {
          return false;
        }

        if (params.capBac && item.capBac !== params.capBac) {
          return false;
        }

        if (params.loaiNhanLuc && item.loaiNhanLuc !== params.loaiNhanLuc) {
          return false;
        }

        if (params.trinhDoCntt && item.trinhDoCntt !== params.trinhDoCntt) {
          return false;
        }

        if (!keyword) {
          return true;
        }

        return normalizeText(
          [
            item.hoTen,
            item.dienThoai,
            item.chucVu,
            item.donViTen,
            item.donViCongTacTen,
          ]
            .filter(Boolean)
            .join(' '),
        ).includes(keyword);
      })
      .sort((left, right) => left.hoTen.localeCompare(right.hoTen, 'vi'));

    const start = (page - 1) * pageSize;
    const pageItems = filtered.slice(start, start + pageSize);

    return {
      items: pageItems.map((item) => ({ ...item })),
      page,
      pageSize,
      totalItems: filtered.length,
      totalPages: Math.max(1, Math.ceil(filtered.length / pageSize)),
    };
  }

  async getById(id: number): Promise<NhanLucCnttItem> {
    const item = this.items.find((entry) => entry.id === id);
    if (!item) {
      throw new Error(
        'Không tìm thấy dữ liệu nhân lực CNTT trong mock repository.',
      );
    }

    return { ...item };
  }

  async create(payload: NhanLucCnttUpsertPayload): Promise<NhanLucCnttItem> {
    const item: NhanLucCnttItem = {
      id: this.nextId++,
      donViId: payload.donViId,
      donViTen: DON_VI_LABELS.get(payload.donViId) ?? 'Đơn vị chưa xác định',
      donViCongTacId: payload.donViCongTacId ?? null,
      donViCongTacTen: payload.donViCongTacId
        ? (DON_VI_LABELS.get(payload.donViCongTacId) ?? 'Đơn vị chưa xác định')
        : null,
      hoTen: payload.hoTen,
      ngaySinh: payload.ngaySinh ?? null,
      gioiTinh: payload.gioiTinh ?? null,
      capBac: payload.capBac ?? null,
      chucVu: payload.chucVu ?? null,
      dienThoai: payload.dienThoai ?? null,
      loaiNhanLuc: payload.loaiNhanLuc ?? null,
      trinhDoCntt: payload.trinhDoCntt ?? null,
      trinhDoLlct: payload.trinhDoLlct ?? null,
      chuyenNganh: null,
      hocVi: null,
      hocHam: null,
      ghiChu: payload.ghiChu ?? null,
      trangThaiCapNhat: 'Đã cập nhật',
      namBaoCao: new Date().getFullYear(),
    };

    this.items = [item, ...this.items];
    return { ...item };
  }

  async update(
    id: number,
    payload: NhanLucCnttUpsertPayload,
  ): Promise<NhanLucCnttItem> {
    const index = this.items.findIndex((entry) => entry.id === id);
    if (index < 0) {
      throw new Error(
        'Không tìm thấy dữ liệu nhân lực CNTT trong mock repository.',
      );
    }

    const current = this.items[index];
    const next: NhanLucCnttItem = {
      ...current,
      donViId: payload.donViId,
      donViTen: DON_VI_LABELS.get(payload.donViId) ?? 'Đơn vị chưa xác định',
      donViCongTacId: payload.donViCongTacId ?? null,
      donViCongTacTen: payload.donViCongTacId
        ? (DON_VI_LABELS.get(payload.donViCongTacId) ?? 'Đơn vị chưa xác định')
        : null,
      hoTen: payload.hoTen,
      ngaySinh: payload.ngaySinh ?? null,
      gioiTinh: payload.gioiTinh ?? null,
      capBac: payload.capBac ?? null,
      chucVu: payload.chucVu ?? null,
      dienThoai: payload.dienThoai ?? null,
      loaiNhanLuc: payload.loaiNhanLuc ?? null,
      trinhDoCntt: payload.trinhDoCntt ?? null,
      trinhDoLlct: payload.trinhDoLlct ?? null,
      chuyenNganh: current.chuyenNganh ?? null,
      hocVi: current.hocVi ?? null,
      hocHam: current.hocHam ?? null,
      ghiChu: payload.ghiChu ?? null,
      trangThaiCapNhat: current.trangThaiCapNhat ?? 'Đã cập nhật',
    };

    this.items = this.items.map((entry) => (entry.id === id ? next : entry));
    return { ...next };
  }

  async delete(id: number): Promise<void> {
    this.items = this.items.filter((entry) => entry.id !== id);
  }
}

function normalizeText(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim();
}
