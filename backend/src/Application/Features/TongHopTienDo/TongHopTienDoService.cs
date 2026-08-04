using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Features.DonVi;
using ThucLuc.Domain.Entities.Reporting;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Application.Features.TongHopTienDo;

public sealed class TongHopTienDoService : ITongHopTienDoService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDonViInputModeService _donViInputModeService;

    public TongHopTienDoService(
        IApplicationDbContext db,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IDonViInputModeService donViInputModeService)
    {
        _db = db;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _donViInputModeService = donViInputModeService;
    }

    public async Task<IReadOnlyCollection<TienDoDonViDto>> GetTienDoAsync(
        TienDoDonViQuery query, CancellationToken ct)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var parentId = query.ParentDonViId ?? currentUser.DonViId;
        var activeOnly = true;

        var children = await _db.DonVis
            .Where(x => x.ParentId == parentId && x.IsActive == activeOnly)
            .Select(x => new { x.Id, x.TenDonVi, x.CapDonVi })
            .ToListAsync(ct);

        if (children.Count == 0)
            return Array.Empty<TienDoDonViDto>();

        var childIds = children.Select(x => x.Id).ToList();

        var trangThaiMap = await _db.KyTrangThaiDonVis
            .Where(x => x.KyBaoCao!.KyCode == query.KyBaoCaoCode && childIds.Contains(x.DonViId))
            .Select(x => new { x.DonViId, x.DaXacNhan, x.NgayXacNhan, x.UpdatedAt })
            .ToDictionaryAsync(x => x.DonViId, ct);

        var kyId = await _db.KyBaoCaos
            .Where(x => x.KyCode == query.KyBaoCaoCode)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(ct);

        var hanBoSungMap = await _db.YeuCauBoSungs
            .Where(x => x.KyBaoCaoId == kyId
                     && childIds.Contains(x.DonViId)
                     && x.TrangThai == YeuCauBoSungStatus.DangBoSung)
            .GroupBy(x => x.DonViId)
            .Select(g => new { DonViId = g.Key, HanBoSung = g.First().HanBoSung })
            .ToDictionaryAsync(x => x.DonViId, x => x.HanBoSung, ct);

        var nhanLuc = await GroupCount(_db.NhanLucCntts, childIds, ct);
        var nangLucSo = await GroupCount(_db.NangLucSos, childIds, ct);
        var daoTao = await GroupCount(_db.DaoTaoBoiDuongs, childIds, ct);
        var daoTaoHocVien = await GroupCount(_db.DaoTaoHocViens, childIds, ct);
        var httt = await GroupCount(_db.HeThongThongTins, childIds, ct);
        var htttTieuChuan = await GroupCount(_db.HtttTieuChuans, childIds, ct);
        var duAn = await GroupCount(_db.DuAnCntts, childIds, ct);
        var thietBi = await GroupCount(_db.ThietBiCntts, childIds, ct);
        var haTang = await GroupCount(_db.HaTangMangs, childIds, ct);
        var giamSatNoc = await GroupCount(_db.GiamSatNocs, childIds, ct);
        var cameraQuanLy = await GroupCount(_db.CameraQuanLies, childIds, ct);
        var cameraThucTrang = await GroupCount(_db.CameraThucTrangs, childIds, ct);
        var giamSatSoc = await GroupCount(_db.GiamSatSocs, childIds, ct);
        var atttVanHanh = await GroupCount(_db.AtttHtttVanHanhs, childIds, ct);
        var atttDauTu = await GroupCount(_db.AtttHtttDauTus, childIds, ct);
        var atttGiaiPhap = await GroupCount(_db.GiaiPhapAttts, childIds, ct);
        var vanBanQppl = await GroupCount(_db.VanBanQppls, childIds, ct);

        var maxUpdatedMap = MergeMax(
            await GroupMaxUpdated(_db.NhanLucCntts, childIds, ct),
            await GroupMaxUpdated(_db.NangLucSos, childIds, ct),
            await GroupMaxUpdated(_db.DaoTaoBoiDuongs, childIds, ct),
            await GroupMaxUpdated(_db.DaoTaoHocViens, childIds, ct),
            await GroupMaxUpdated(_db.HeThongThongTins, childIds, ct),
            await GroupMaxUpdated(_db.HtttTieuChuans, childIds, ct),
            await GroupMaxUpdated(_db.DuAnCntts, childIds, ct),
            await GroupMaxUpdated(_db.ThietBiCntts, childIds, ct),
            await GroupMaxUpdated(_db.HaTangMangs, childIds, ct),
            await GroupMaxUpdated(_db.GiamSatNocs, childIds, ct),
            await GroupMaxUpdated(_db.CameraQuanLies, childIds, ct),
            await GroupMaxUpdated(_db.CameraThucTrangs, childIds, ct),
            await GroupMaxUpdated(_db.GiamSatSocs, childIds, ct),
            await GroupMaxUpdated(_db.AtttHtttVanHanhs, childIds, ct),
            await GroupMaxUpdated(_db.AtttHtttDauTus, childIds, ct),
            await GroupMaxUpdated(_db.GiaiPhapAttts, childIds, ct),
            await GroupMaxUpdated(_db.VanBanQppls, childIds, ct));

        return children.Select(c =>
        {
            trangThaiMap.TryGetValue(c.Id, out var tt);
            hanBoSungMap.TryGetValue(c.Id, out var hanBoSung);
            var mocXacNhan = tt?.NgayXacNhan ?? tt?.UpdatedAt;
            var maxUpdated = maxUpdatedMap.GetValueOrDefault(c.Id);
            return new TienDoDonViDto
            {
                DonViId = c.Id,
                TenDonVi = c.TenDonVi,
                CapDonVi = c.CapDonVi ?? string.Empty,
                DaXacNhan = tt?.DaXacNhan ?? false,
                NgayXacNhan = tt?.NgayXacNhan,
                CoThayDoiSauXacNhan = (tt?.DaXacNhan ?? false)
                    && mocXacNhan.HasValue
                    && maxUpdated.HasValue
                    && maxUpdated.Value > mocXacNhan.Value,
                CapNhatLanCuoi = tt?.UpdatedAt,
                HanBoSung = hanBoSung,
                SoNhanLuc = nhanLuc.GetValueOrDefault(c.Id),
                SoNangLucSo = nangLucSo.GetValueOrDefault(c.Id),
                SoDaoTao = daoTao.GetValueOrDefault(c.Id),
                SoDaoTaoHocVien = daoTaoHocVien.GetValueOrDefault(c.Id),
                SoHeThongThongTin = httt.GetValueOrDefault(c.Id),
                SoHtttTieuChuan = htttTieuChuan.GetValueOrDefault(c.Id),
                SoDuAn = duAn.GetValueOrDefault(c.Id),
                SoThietBi = thietBi.GetValueOrDefault(c.Id),
                SoHaTangMang = haTang.GetValueOrDefault(c.Id),
                SoGiamSatNoc = giamSatNoc.GetValueOrDefault(c.Id),
                SoCameraQuanLy = cameraQuanLy.GetValueOrDefault(c.Id),
                SoCameraThucTrang = cameraThucTrang.GetValueOrDefault(c.Id),
                SoGiamSatSoc = giamSatSoc.GetValueOrDefault(c.Id),
                SoAtttVanHanh = atttVanHanh.GetValueOrDefault(c.Id),
                SoAtttDauTu = atttDauTu.GetValueOrDefault(c.Id),
                SoAtttGiaiPhap = atttGiaiPhap.GetValueOrDefault(c.Id),
                SoVanBanQppl = vanBanQppl.GetValueOrDefault(c.Id),
            };
        }).ToList();
    }

    public async Task<TienDoDonViDto> GetMyTienDoAsync(string kyBaoCaoCode, CancellationToken ct)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var donViId = currentUser.DonViId;
        var activeOnly = true;

        var donVi = await _db.DonVis
            .Where(x => x.Id == donViId && x.IsActive == activeOnly)
            .Select(x => new { x.Id, x.TenDonVi, x.CapDonVi })
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException("DON_VI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);

        var tt = await _db.KyTrangThaiDonVis
            .Where(x => x.KyBaoCao!.KyCode == kyBaoCaoCode && x.DonViId == donViId)
            .Select(x => new { x.DaXacNhan, x.NgayXacNhan, x.UpdatedAt })
            .FirstOrDefaultAsync(ct);

        var kyId = await _db.KyBaoCaos
            .Where(x => x.KyCode == kyBaoCaoCode)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(ct);

        var hanBoSung = await _db.YeuCauBoSungs
            .Where(x => x.KyBaoCaoId == kyId
                     && x.DonViId == donViId
                     && x.TrangThai == YeuCauBoSungStatus.DangBoSung)
            .Select(x => x.HanBoSung)
            .FirstOrDefaultAsync(ct);

        var mocXacNhan = tt?.NgayXacNhan ?? tt?.UpdatedAt;
        var maxUpdated = MergeMax(
            await MaxUpdatedAt(_db.NhanLucCntts, donViId, ct),
            await MaxUpdatedAt(_db.NangLucSos, donViId, ct),
            await MaxUpdatedAt(_db.DaoTaoBoiDuongs, donViId, ct),
            await MaxUpdatedAt(_db.DaoTaoHocViens, donViId, ct),
            await MaxUpdatedAt(_db.HeThongThongTins, donViId, ct),
            await MaxUpdatedAt(_db.HtttTieuChuans, donViId, ct),
            await MaxUpdatedAt(_db.DuAnCntts, donViId, ct),
            await MaxUpdatedAt(_db.ThietBiCntts, donViId, ct),
            await MaxUpdatedAt(_db.HaTangMangs, donViId, ct),
            await MaxUpdatedAt(_db.GiamSatNocs, donViId, ct),
            await MaxUpdatedAt(_db.CameraQuanLies, donViId, ct),
            await MaxUpdatedAt(_db.CameraThucTrangs, donViId, ct),
            await MaxUpdatedAt(_db.GiamSatSocs, donViId, ct),
            await MaxUpdatedAt(_db.AtttHtttVanHanhs, donViId, ct),
            await MaxUpdatedAt(_db.AtttHtttDauTus, donViId, ct),
            await MaxUpdatedAt(_db.GiaiPhapAttts, donViId, ct),
            await MaxUpdatedAt(_db.VanBanQppls, donViId, ct));

        return new TienDoDonViDto
        {
            DonViId = donVi.Id,
            TenDonVi = donVi.TenDonVi,
            CapDonVi = donVi.CapDonVi ?? string.Empty,
            DaXacNhan = tt?.DaXacNhan ?? false,
            NgayXacNhan = tt?.NgayXacNhan,
            CoThayDoiSauXacNhan = (tt?.DaXacNhan ?? false)
                && mocXacNhan.HasValue
                && maxUpdated.HasValue
                && maxUpdated.Value > mocXacNhan.Value,
            CapNhatLanCuoi = tt?.UpdatedAt,
            HanBoSung = hanBoSung,
            SoNhanLuc = await _db.NhanLucCntts.CountAsync(x => x.DonViId == donViId, ct),
            SoNangLucSo = await _db.NangLucSos.CountAsync(x => x.DonViId == donViId, ct),
            SoDaoTao = await _db.DaoTaoBoiDuongs.CountAsync(x => x.DonViId == donViId, ct),
            SoDaoTaoHocVien = await _db.DaoTaoHocViens.CountAsync(x => x.DonViId == donViId, ct),
            SoHeThongThongTin = await _db.HeThongThongTins.CountAsync(x => x.DonViId == donViId, ct),
            SoHtttTieuChuan = await _db.HtttTieuChuans.CountAsync(x => x.DonViId == donViId, ct),
            SoDuAn = await _db.DuAnCntts.CountAsync(x => x.DonViId == donViId, ct),
            SoThietBi = await _db.ThietBiCntts.CountAsync(x => x.DonViId == donViId, ct),
            SoHaTangMang = await _db.HaTangMangs.CountAsync(x => x.DonViId == donViId, ct),
            SoGiamSatNoc = await _db.GiamSatNocs.CountAsync(x => x.DonViId == donViId, ct),
            SoCameraQuanLy = await _db.CameraQuanLies.CountAsync(x => x.DonViId == donViId, ct),
            SoCameraThucTrang = await _db.CameraThucTrangs.CountAsync(x => x.DonViId == donViId, ct),
            SoGiamSatSoc = await _db.GiamSatSocs.CountAsync(x => x.DonViId == donViId, ct),
            SoAtttVanHanh = await _db.AtttHtttVanHanhs.CountAsync(x => x.DonViId == donViId, ct),
            SoAtttDauTu = await _db.AtttHtttDauTus.CountAsync(x => x.DonViId == donViId, ct),
            SoAtttGiaiPhap = await _db.GiaiPhapAttts.CountAsync(x => x.DonViId == donViId, ct),
            SoVanBanQppl = await _db.VanBanQppls.CountAsync(x => x.DonViId == donViId, ct),
        };
    }

    public async Task XacNhanAsync(XacNhanRequest request, CancellationToken ct)
    {
        var currentUser = _currentUserService.GetCurrentUser();

        var ky = await _db.KyBaoCaos.FirstOrDefaultAsync(x => x.KyCode == request.KyBaoCaoCode, ct)
            ?? throw new AppException("KY_NOT_FOUND", "Không tìm thấy kỳ báo cáo.", 404);

        var tt = await _db.KyTrangThaiDonVis
            .FirstOrDefaultAsync(x => x.KyBaoCaoId == ky.Id && x.DonViId == currentUser.DonViId, ct);

        if (tt is null)
        {
            tt = new KyTrangThaiDonVi
            {
                KyBaoCaoId = ky.Id,
                DonViId = currentUser.DonViId,
            };
            _db.KyTrangThaiDonVis.Add(tt);
        }

        tt.DaXacNhan = request.DaXacNhan;

        if (request.DaXacNhan)
        {
            tt.NgayXacNhan = _dateTimeProvider.Now;
            tt.ConfirmedBy = currentUser.UserId;
        }
        else
        {
            tt.NgayMoLai = _dateTimeProvider.Now;
            tt.MoLaiBy = currentUser.UserId;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<ChiTietModuleDto> GetChiTietModuleAsync(long donViId, string moduleCode, CancellationToken ct)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var modeContext = await _donViInputModeService.GetContextAsync(currentUser.DonViId, ct);
        var allowed = donViId == currentUser.DonViId
            || modeContext.DescendantDonViIds.Contains(donViId);
        if (!allowed)
        {
            throw new AppException(
                "TIEN_DO_SCOPE_DENIED",
                "Chỉ được xem chi tiết dữ liệu của đơn vị trực thuộc.",
                403);
        }

        var tenDonVi = await _db.DonVis
            .Where(x => x.Id == donViId)
            .Select(x => x.TenDonVi)
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException("DON_VI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);

        var normalizedModule = (moduleCode ?? string.Empty).Trim().ToUpperInvariant();
        var (columns, rows, total) = normalizedModule switch
        {
            "NHAN_LUC_CNTT" => await ProjectAsync(_db.NhanLucCntts, donViId, ct,
                ("hoTen", "Họ tên", x => x.HoTen),
                ("capBac", "Cấp bậc", x => x.CapBac),
                ("chucVu", "Chức vụ", x => x.ChucVu),
                ("dienThoai", "Điện thoại", x => x.DienThoai),
                ("loaiNhanLuc", "Loại nhân lực", x => x.LoaiNhanLuc),
                ("trinhDoCntt", "Trình độ CNTT", x => x.TrinhDoCntt),
                ("ghiChu", "Ghi chú", x => x.GhiChu)),
            "NANG_LUC_SO" => await ProjectAsync(_db.NangLucSos, donViId, ct,
                ("nhomViTri", "Nhóm vị trí", x => x.NhomViTri),
                ("tongSoDienDanhGia", "Diện đánh giá", x => x.TongSoDienDanhGia),
                ("tongSoDat", "Số đạt", x => x.TongSoDat),
                ("tongSoChuaDat", "Số chưa đạt", x => x.TongSoChuaDat),
                ("ghiChu", "Ghi chú", x => x.GhiChu)),
            "DAO_TAO_BOI_DUONG" => await ProjectAsync(_db.DaoTaoBoiDuongs, donViId, ct,
                ("tenKhoaHoc", "Tên khóa học", x => x.TenKhoaHoc),
                ("donViToChuc", "Đơn vị tổ chức", x => x.DonViToChuc),
                ("hinhThuc", "Hình thức", x => x.HinhThuc),
                ("soLuongHv", "Số học viên", x => x.SoLuongHv),
                ("thoiGianTu", "Từ ngày", x => x.ThoiGianTu),
                ("thoiGianDen", "Đến ngày", x => x.ThoiGianDen)),
            "DAO_TAO_HOC_VIEN" => await ProjectAsync(_db.DaoTaoHocViens, donViId, ct,
                ("nam", "Năm", x => x.Nam),
                ("noiDungDaoTao", "Nội dung đào tạo", x => x.NoiDungDaoTao),
                ("soTienSi", "Tiến sĩ", x => x.SoTienSi),
                ("soThacSi", "Thạc sĩ", x => x.SoThacSi),
                ("soDaiHoc", "Đại học", x => x.SoDaiHoc),
                ("soCaoDang", "Cao đẳng", x => x.SoCaoDang),
                ("soTrungCap", "Trung cấp", x => x.SoTrungCap)),
            "HE_THONG_THONG_TIN" => await ProjectAsync(_db.HeThongThongTins, donViId, ct,
                ("tenPhanMem", "Tên phần mềm", x => x.TenPhanMem),
                ("donViPhatTrien", "Đơn vị phát triển", x => x.DonViPhatTrien),
                ("donViQuanLy", "Đơn vị quản lý", x => x.DonViQuanLy),
                ("namTrienKhai", "Năm triển khai", x => x.NamTrienKhai),
                ("phamViHoatDong", "Phạm vi", x => x.PhamViHoatDong)),
            "HTTT_TIEU_CHUAN" => await ProjectAsync(_db.HtttTieuChuans, donViId, ct,
                ("tenHeThong", "Tên hệ thống", x => x.TenHeThong),
                ("dvt", "ĐVT", x => x.Dvt),
                ("soH05", "Số H05", x => x.SoH05),
                ("soTinh", "Số tỉnh", x => x.SoTinh),
                ("soXa", "Số xã", x => x.SoXa)),
            "DU_AN_CNTT" => await ProjectAsync(_db.DuAnCntts, donViId, ct,
                ("tenDuAn", "Tên dự án", x => x.TenDuAn),
                ("donViChuTri", "Đơn vị chủ trì", x => x.DonViChuTri),
                ("namTrienKhai", "Năm triển khai", x => x.NamTrienKhai),
                ("tongKinhPhi", "Tổng kinh phí", x => x.TongKinhPhi),
                ("nguonVon", "Nguồn vốn", x => x.NguonVon)),
            "THIET_BI_CNTT" => await ProjectAsync(_db.ThietBiCntts, donViId, ct,
                ("tenThietBi", "Tên thiết bị", x => x.TenThietBi),
                ("hangSanXuat", "Hãng SX", x => x.HangSanXuat),
                ("model", "Model", x => x.Model),
                ("soLuongTong", "SL tổng", x => x.SoLuongTong),
                ("soLuongHienDung", "SL hiện dùng", x => x.SoLuongHienDung),
                ("soLuongHong", "SL hỏng", x => x.SoLuongHong),
                ("tinhTrang", "Tình trạng", x => x.TinhTrang)),
            "HA_TANG_MANG" => await ProjectAsync(_db.HaTangMangs, donViId, ct,
                ("soDonViTrucThuoc", "ĐV trực thuộc", x => x.SoDonViTrucThuoc),
                ("soDaKetNoiBcanet", "Kết nối BCANET", x => x.SoDaKetNoiBcanet),
                ("soDuongTruyenVnpt", "Đường truyền VNPT", x => x.SoDuongTruyenVnpt),
                ("soDuongTruyenKhac", "Đường truyền khác", x => x.SoDuongTruyenKhac),
                ("soKetNoiInternet", "Kết nối Internet", x => x.SoKetNoiInternet)),
            "GIAM_SAT_NOC" => await ProjectAsync(_db.GiamSatNocs, donViId, ct,
                ("lopGiamSat", "Lớp giám sát", x => x.LopGiamSat),
                ("coNoc", "Có NOC", x => x.CoNoc ? "Có" : "Không"),
                ("thucTrang", "Thực trạng", x => x.ThucTrang),
                ("tongSoDoiTuong", "Tổng đối tượng", x => x.TongSoDoiTuong),
                ("soDaGiamSat", "Đã giám sát", x => x.SoDaGiamSat)),
            "CAMERA_QUAN_LY" => await ProjectAsync(_db.CameraQuanLies, donViId, ct,
                ("nhomCamera", "Nhóm camera", x => x.NhomCamera),
                ("tenDonViDiaChi", "Đơn vị/địa chỉ", x => x.TenDonViDiaChi),
                ("buongGiamTrangBiSl", "Buồng giam trang bị", x => x.BuongGiamTrangBiSl),
                ("nhuCauDauTu", "Nhu cầu đầu tư", x => x.NhuCauDauTu),
                ("soLanViPham", "Số lần vi phạm", x => x.SoLanViPham)),
            "CAMERA_THUC_TRANG" => await ProjectAsync(_db.CameraThucTrangs, donViId, ct,
                ("nhomCamera", "Nhóm camera", x => x.NhomCamera),
                ("tenHeThong", "Tên hệ thống", x => x.TenHeThong),
                ("thucTrangIp", "Thực trạng IP", x => x.ThucTrangIp),
                ("thucTrangAnalog", "Thực trạng Analog", x => x.ThucTrangAnalog),
                ("chuDauTu", "Chủ đầu tư", x => x.ChuDauTu),
                ("namDauTu", "Năm đầu tư", x => x.NamDauTu)),
            "GIAM_SAT_SOC" => await ProjectAsync(_db.GiamSatSocs, donViId, ct,
                ("loaiMang", "Loại mạng", x => x.LoaiMang),
                ("lopGiamSat", "Lớp giám sát", x => x.LopGiamSat),
                ("tongSoDoiTuong", "Tổng đối tượng", x => x.TongSoDoiTuong),
                ("soGiamSatDayDu", "GS đầy đủ", x => x.SoGiamSatDayDu),
                ("soSuCo", "Số sự cố", x => x.SoSuCo),
                ("soSuCoDaKhacPhuc", "Đã khắc phục", x => x.SoSuCoDaKhacPhuc)),
            "ATTT_HTTT_VAN_HANH" => await ProjectAsync(_db.AtttHtttVanHanhs, donViId, ct,
                ("loaiHaTang", "Loại hạ tầng", x => x.LoaiHaTang),
                ("chuQuan", "Chủ quản", x => x.ChuQuan),
                ("donViVanHanh", "Đơn vị vận hành", x => x.DonViVanHanh),
                ("capDoDeXuat", "Cấp độ đề xuất", x => x.CapDoDeXuat),
                ("tinhTrangPheDuyet", "Tình trạng phê duyệt", x => x.TinhTrangPheDuyet)),
            "ATTT_HTTT_DAU_TU" => await ProjectAsync(_db.AtttHtttDauTus, donViId, ct,
                ("chuQuan", "Chủ quản", x => x.ChuQuan),
                ("donViVanHanh", "Đơn vị vận hành", x => x.DonViVanHanh),
                ("capDoDeXuat", "Cấp độ đề xuất", x => x.CapDoDeXuat),
                ("quyetDinhPheDuyet", "QĐ phê duyệt", x => x.QuyetDinhPheDuyet)),
            "ATTT_GIAI_PHAP" => await ProjectAsync(_db.GiaiPhapAttts, donViId, ct,
                ("tenGiaiPhap", "Tên giải pháp", x => x.TenGiaiPhap),
                ("mayTinhBcanetSl", "Máy tính BCANET", x => x.MayTinhBcanetSl),
                ("mayTinhInternetSl", "Máy tính Internet", x => x.MayTinhInternetSl),
                ("mayChuBcanetSl", "Máy chủ BCANET", x => x.MayChuBcanetSl),
                ("ghiChu", "Ghi chú", x => x.GhiChu)),
            "VAN_BAN_QPPL" => await ProjectAsync(_db.VanBanQppls, donViId, ct,
                ("soHieu", "Số hiệu", x => x.SoHieu),
                ("tenVanBan", "Tên văn bản", x => x.TenVanBan),
                ("loaiVanBan", "Loại văn bản", x => x.LoaiVanBan),
                ("coQuanBanHanh", "Cơ quan ban hành", x => x.CoQuanBanHanh),
                ("ngayBanHanh", "Ngày ban hành", x => x.NgayBanHanh),
                ("tinhTrangTrienKhai", "Tình trạng triển khai", x => x.TinhTrangTrienKhai)),
            _ => throw new AppException("MODULE_NOT_SUPPORTED", "Module không được hỗ trợ xem chi tiết.", 400),
        };

        await TranslateCodedColumnsAsync(normalizedModule, rows, ct);

        return new ChiTietModuleDto
        {
            DonViId = donViId,
            TenDonVi = tenDonVi,
            ModuleCode = normalizedModule,
            TotalRows = total,
            Columns = columns,
            Rows = rows,
        };
    }

    /// <summary>Cột nào lưu mã danh mục (SYS_CODES) → dịch sang tên hiển thị.</summary>
    private static readonly Dictionary<string, Dictionary<string, string>> ModuleCodedColumns = new()
    {
        ["NHAN_LUC_CNTT"] = new()
        {
            ["capBac"] = "CAP_BAC_CONG_AN",
            ["loaiNhanLuc"] = "LOAI_NHAN_LUC",
            ["trinhDoCntt"] = "TRINH_DO_CNTT",
        },
        ["NANG_LUC_SO"] = new() { ["nhomViTri"] = "NHOM_NANG_LUC_SO" },
        ["GIAM_SAT_NOC"] = new()
        {
            ["lopGiamSat"] = "LOP_GIAM_SAT",
            ["thucTrang"] = "THUC_TRANG_GIAM_SAT",
        },
        ["GIAM_SAT_SOC"] = new()
        {
            ["loaiMang"] = "LOAI_MANG_GIAM_SAT",
            ["lopGiamSat"] = "LOP_GIAM_SAT",
        },
        ["CAMERA_QUAN_LY"] = new() { ["nhomCamera"] = "NHOM_CAMERA" },
        ["CAMERA_THUC_TRANG"] = new() { ["nhomCamera"] = "NHOM_CAMERA" },
        ["DU_AN_CNTT"] = new() { ["nguonVon"] = "NGUON_VON_DU_AN" },
        ["VAN_BAN_QPPL"] = new()
        {
            ["loaiVanBan"] = "LOAI_VAN_BAN",
            ["coQuanBanHanh"] = "CO_QUAN_BAN_HANH",
        },
        ["ATTT_GIAI_PHAP"] = new() { ["tenGiaiPhap"] = "GIAI_PHAP_ATTT" },
    };

    private async Task TranslateCodedColumnsAsync(
        string moduleCode,
        List<Dictionary<string, object?>> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0
            || !ModuleCodedColumns.TryGetValue(moduleCode, out var codedColumns))
        {
            return;
        }

        var codeKeys = codedColumns.Values.Distinct().ToList();
        var codeValues = await _db.CodeValues
            .AsNoTracking()
            .Where(v => v.Code != null && codeKeys.Contains(v.Code.CodeKey))
            .Select(v => new { v.Code!.CodeKey, v.Value, v.Name })
            .ToListAsync(ct);

        var byGroup = codeValues
            .GroupBy(x => x.CodeKey)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First().Name, StringComparer.OrdinalIgnoreCase));

        foreach (var row in rows)
        {
            foreach (var (colKey, codeKey) in codedColumns)
            {
                if (row.TryGetValue(colKey, out var raw)
                    && raw is string rawValue
                    && rawValue.Length > 0
                    && byGroup.TryGetValue(codeKey, out var map)
                    && map.TryGetValue(rawValue, out var name))
                {
                    row[colKey] = name;
                }
            }
        }
    }

    private static async Task<(List<ChiTietColumnDto> Columns, List<Dictionary<string, object?>> Rows, int Total)> ProjectAsync<T>(
        IQueryable<T> source,
        long donViId,
        CancellationToken ct,
        params (string Key, string Label, Func<T, object?> Get)[] cols) where T : class
    {
        var scoped = source
            .AsNoTracking()
            .Where(x => EF.Property<long>(x, "DonViId") == donViId);

        var total = await scoped.CountAsync(ct);
        var items = await scoped
            .OrderByDescending(x => EF.Property<DateTime>(x, "UpdatedAt"))
            .Take(300)
            .ToListAsync(ct);

        var columns = cols
            .Select(c => new ChiTietColumnDto { Key = c.Key, Label = c.Label })
            .ToList();
        var rows = items
            .Select(item => cols.ToDictionary(c => c.Key, c => c.Get(item)))
            .ToList();

        return (columns, rows, total);
    }

    private static Task<Dictionary<long, int>> GroupCount<T>(
        IQueryable<T> source,
        List<long> ids,
        CancellationToken ct) where T : class
        => source
            .Where(x => ids.Contains(EF.Property<long>(x, "DonViId")))
            .GroupBy(x => EF.Property<long>(x, "DonViId"))
            .Select(g => new { DonViId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DonViId, x => x.Count, ct);

    // IgnoreQueryFilters: soft-delete cũng là "số liệu thay đổi" nên phải tính cả bản ghi đã xóa.
    private static Task<Dictionary<long, DateTime?>> GroupMaxUpdated<T>(
        IQueryable<T> source,
        List<long> ids,
        CancellationToken ct) where T : class
        => source
            .IgnoreQueryFilters()
            .Where(x => ids.Contains(EF.Property<long>(x, "DonViId")))
            .GroupBy(x => EF.Property<long>(x, "DonViId"))
            .Select(g => new
            {
                DonViId = g.Key,
                Max = (DateTime?)g.Max(x => EF.Property<DateTime>(x, "UpdatedAt")),
            })
            .ToDictionaryAsync(x => x.DonViId, x => x.Max, ct);

    private static Task<DateTime?> MaxUpdatedAt<T>(
        IQueryable<T> source,
        long donViId,
        CancellationToken ct) where T : class
        => source
            .IgnoreQueryFilters()
            .Where(x => EF.Property<long>(x, "DonViId") == donViId)
            .MaxAsync(x => (DateTime?)EF.Property<DateTime>(x, "UpdatedAt"), ct);

    private static DateTime? MergeMax(params DateTime?[] values)
        => values.Where(x => x.HasValue).DefaultIfEmpty(null).Max();

    private static Dictionary<long, DateTime?> MergeMax(
        params Dictionary<long, DateTime?>[] maps)
    {
        var merged = new Dictionary<long, DateTime?>();
        foreach (var map in maps)
        {
            foreach (var (key, value) in map)
            {
                if (!merged.TryGetValue(key, out var existing) || value > existing)
                {
                    merged[key] = value;
                }
            }
        }

        return merged;
    }
}
