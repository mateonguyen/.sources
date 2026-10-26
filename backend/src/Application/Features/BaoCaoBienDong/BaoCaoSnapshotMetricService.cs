using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Domain.Entities.Reporting;

namespace ThucLuc.Application.Features.BaoCaoBienDong;

public interface IBaoCaoSnapshotMetricService
{
    Task CaptureLiveAsync(
        long snapshotId,
        long snapshotBatchId,
        long kyBaoCaoId,
        long reportDonViId,
        IReadOnlyCollection<long> sourceDonViIds,
        IReadOnlyCollection<string> moduleCodes,
        CancellationToken cancellationToken = default);
}

public sealed class BaoCaoSnapshotMetricService : IBaoCaoSnapshotMetricService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public BaoCaoSnapshotMetricService(IApplicationDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task CaptureLiveAsync(
        long snapshotId,
        long snapshotBatchId,
        long kyBaoCaoId,
        long reportDonViId,
        IReadOnlyCollection<long> sourceDonViIds,
        IReadOnlyCollection<string> moduleCodes,
        CancellationToken cancellationToken = default)
    {
        var unitIds = sourceDonViIds.Distinct().ToArray();
        if (unitIds.Length == 0)
        {
            return;
        }

        var requestedModules = moduleCodes.Count > 0
            ? new HashSet<string>(moduleCodes, StringComparer.OrdinalIgnoreCase)
            : BaoCaoMetricCatalog.All.Select(x => x.ModuleCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new List<BaoCaoSnapshotMetric>();
        var userId = _currentUserService.GetCurrentUser().UserId;

        void Add(long unitId, string module, string metric, decimal value, decimal? numerator = null, decimal? denominator = null)
        {
            if (!requestedModules.Contains(module) || BaoCaoMetricCatalog.Find(module, metric) is null)
            {
                return;
            }

            rows.Add(new BaoCaoSnapshotMetric
            {
                SnapshotId = snapshotId,
                SnapshotBatchId = snapshotBatchId,
                KyBaoCaoId = kyBaoCaoId,
                ReportDonViId = reportDonViId,
                SourceDonViId = unitId,
                ModuleCode = module,
                MetricCode = metric,
                Value = value,
                Numerator = numerator,
                Denominator = denominator,
                MetricVersion = 1,
                CreatedBy = userId,
                UpdatedBy = userId,
            });
        }

        if (requestedModules.Contains("NHAN_LUC_CNTT"))
        {
            var data = await _db.NhanLucCntts.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Total = g.Count() }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) Add(unitId, "NHAN_LUC_CNTT", "TONG_NHAN_SU", data.FirstOrDefault(x => x.DonViId == unitId)?.Total ?? 0);
        }

        if (requestedModules.Contains("NANG_LUC_SO"))
        {
            var data = await _db.NangLucSos.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Total = g.Sum(x => x.TongSoDienDanhGia), Dat = g.Sum(x => x.TongSoDat), ChuaDat = g.Sum(x => x.TongSoChuaDat) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds)
            {
                var x = data.FirstOrDefault(v => v.DonViId == unitId);
                var total = x?.Total ?? 0; var dat = x?.Dat ?? 0; var chuaDat = x?.ChuaDat ?? 0;
                Add(unitId, "NANG_LUC_SO", "SO_DUOC_DANH_GIA", total);
                Add(unitId, "NANG_LUC_SO", "SO_DAT", dat);
                Add(unitId, "NANG_LUC_SO", "SO_CHUA_DAT", chuaDat);
                AddRatio(unitId, "NANG_LUC_SO", "TY_LE_DAT", dat, total, Add);
            }
        }

        if (requestedModules.Contains("DAO_TAO_BOI_DUONG"))
        {
            var data = await _db.DaoTaoBoiDuongs.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Courses = g.Count(), Students = g.Sum(x => x.SoLuongHv ?? 0) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) { var x = data.FirstOrDefault(v => v.DonViId == unitId); Add(unitId, "DAO_TAO_BOI_DUONG", "SO_KHOA", x?.Courses ?? 0); Add(unitId, "DAO_TAO_BOI_DUONG", "SO_HOC_VIEN", x?.Students ?? 0); }
        }

        if (requestedModules.Contains("DAO_TAO_HOC_VIEN"))
        {
            var data = await _db.DaoTaoHocViens.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Total = g.Sum(x => x.SoTienSi + x.SoThacSi + x.SoDaiHoc + x.SoCaoDang + x.SoTrungCap) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) Add(unitId, "DAO_TAO_HOC_VIEN", "TONG_HOC_VIEN", data.FirstOrDefault(x => x.DonViId == unitId)?.Total ?? 0);
        }

        if (requestedModules.Contains("HE_THONG_THONG_TIN"))
        {
            var sangKienFlag = true;
            var data = await _db.HeThongThongTins.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Total = g.Count(), Innovations = g.Count(x => x.DaCongNhanSangKien == sangKienFlag) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) { var x = data.FirstOrDefault(v => v.DonViId == unitId); Add(unitId, "HE_THONG_THONG_TIN", "SO_HE_THONG", x?.Total ?? 0); Add(unitId, "HE_THONG_THONG_TIN", "SO_SANG_KIEN", x?.Innovations ?? 0); }
        }

        if (requestedModules.Contains("HTTT_TIEU_CHUAN"))
        {
            var data = await _db.HtttTieuChuans.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Total = g.Sum(x => x.SoH05 + x.SoTinh + x.SoXa + x.SoDvTrucThuocBo) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) Add(unitId, "HTTT_TIEU_CHUAN", "TONG_DIEM_TRIEN_KHAI", data.FirstOrDefault(x => x.DonViId == unitId)?.Total ?? 0);
        }

        if (requestedModules.Contains("DU_AN_CNTT"))
        {
            var data = await _db.DuAnCntts.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Total = g.Count(), Budget = g.Sum(x => x.TongKinhPhi ?? 0) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) { var x = data.FirstOrDefault(v => v.DonViId == unitId); Add(unitId, "DU_AN_CNTT", "SO_DU_AN", x?.Total ?? 0); Add(unitId, "DU_AN_CNTT", "TONG_KINH_PHI", x?.Budget ?? 0); }
        }

        if (requestedModules.Contains("THIET_BI_CNTT"))
        {
            var data = await _db.ThietBiCntts.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Total = g.Sum(x => x.SoLuongTong), Using = g.Sum(x => x.SoLuongHienDung), Broken = g.Sum(x => x.SoLuongHong) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) { var x = data.FirstOrDefault(v => v.DonViId == unitId); var total = x?.Total ?? 0; var broken = x?.Broken ?? 0; Add(unitId, "THIET_BI_CNTT", "TONG_THIET_BI", total); Add(unitId, "THIET_BI_CNTT", "DANG_SU_DUNG", x?.Using ?? 0); Add(unitId, "THIET_BI_CNTT", "THIET_BI_HONG", broken); AddRatio(unitId, "THIET_BI_CNTT", "TY_LE_HONG", broken, total, Add); }
        }

        if (requestedModules.Contains("HA_TANG_MANG"))
        {
            var data = await _db.HaTangMangs.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Bcanet = g.Sum(x => x.SoDaKetNoiBcanet), Lines = g.Sum(x => x.SoDuongTruyenVnpt + x.SoDuongTruyenKhac), Internet = g.Sum(x => x.SoKetNoiInternet) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) { var x = data.FirstOrDefault(v => v.DonViId == unitId); Add(unitId, "HA_TANG_MANG", "SO_DON_VI_KET_NOI_BCANET", x?.Bcanet ?? 0); Add(unitId, "HA_TANG_MANG", "SO_DUONG_TRUYEN", x?.Lines ?? 0); Add(unitId, "HA_TANG_MANG", "SO_KET_NOI_INTERNET", x?.Internet ?? 0); }
        }

        if (requestedModules.Contains("GIAM_SAT_NOC"))
        {
            var data = await _db.GiamSatNocs.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Total = g.Sum(x => x.TongSoDoiTuong), Monitored = g.Sum(x => x.SoDaGiamSat) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) { var x = data.FirstOrDefault(v => v.DonViId == unitId); var total = x?.Total ?? 0; var monitored = x?.Monitored ?? 0; Add(unitId, "GIAM_SAT_NOC", "TONG_DOI_TUONG", total); Add(unitId, "GIAM_SAT_NOC", "DA_GIAM_SAT", monitored); AddRatio(unitId, "GIAM_SAT_NOC", "TY_LE_GIAM_SAT", monitored, total, Add); }
        }

        if (requestedModules.Contains("GIAM_SAT_SOC"))
        {
            var data = await _db.GiamSatSocs.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Total = g.Sum(x => x.TongSoDoiTuong), Monitored = g.Sum(x => x.SoGiamSatMotPhan + x.SoGiamSatCoBan + x.SoGiamSatDayDu), Incidents = g.Sum(x => x.SoSuCo), Resolved = g.Sum(x => x.SoSuCoDaKhacPhuc) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) { var x = data.FirstOrDefault(v => v.DonViId == unitId); var incidents = x?.Incidents ?? 0; var resolved = x?.Resolved ?? 0; Add(unitId, "GIAM_SAT_SOC", "TONG_DOI_TUONG", x?.Total ?? 0); Add(unitId, "GIAM_SAT_SOC", "DA_GIAM_SAT", x?.Monitored ?? 0); Add(unitId, "GIAM_SAT_SOC", "SO_SU_CO", incidents); AddRatio(unitId, "GIAM_SAT_SOC", "TY_LE_KHAC_PHUC", resolved, incidents, Add); }
        }

        if (requestedModules.Contains("CAMERA_QUAN_LY"))
        {
            var data = await _db.CameraQuanLies.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Equipped = g.Sum(x => x.BuongGiamTrangBiSl), Need = g.Sum(x => x.NhuCauDauTu), Repair = g.Sum(x => x.BaoTri + x.SuaChua) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) { var x = data.FirstOrDefault(v => v.DonViId == unitId); Add(unitId, "CAMERA_QUAN_LY", "SO_CAMERA_TRANG_BI", x?.Equipped ?? 0); Add(unitId, "CAMERA_QUAN_LY", "NHU_CAU_DAU_TU", x?.Need ?? 0); Add(unitId, "CAMERA_QUAN_LY", "CAN_BAO_TRI_SUA_CHUA", x?.Repair ?? 0); }
        }

        if (requestedModules.Contains("CAMERA_THUC_TRANG"))
        {
            var data = await _db.CameraThucTrangs.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Configured = g.Sum(x => x.CauHinhIp + x.CauHinhAnalog), Operating = g.Sum(x => x.ThucTrangIp + x.ThucTrangAnalog) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) { var x = data.FirstOrDefault(v => v.DonViId == unitId); Add(unitId, "CAMERA_THUC_TRANG", "SO_CAMERA_CAU_HINH", x?.Configured ?? 0); Add(unitId, "CAMERA_THUC_TRANG", "SO_CAMERA_HOAT_DONG", x?.Operating ?? 0); }
        }

        if (requestedModules.Contains("ATTT_HTTT_VAN_HANH"))
        {
            var trienKhaiFlag = true;
            var data = await _db.AtttHtttVanHanhs.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Total = g.Count(), Implemented = g.Count(x => x.DaTrienKhaiPhuongAn == trienKhaiFlag) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) { var x = data.FirstOrDefault(v => v.DonViId == unitId); Add(unitId, "ATTT_HTTT_VAN_HANH", "SO_HE_THONG", x?.Total ?? 0); Add(unitId, "ATTT_HTTT_VAN_HANH", "DA_TRIEN_KHAI_PHUONG_AN", x?.Implemented ?? 0); }
        }

        if (requestedModules.Contains("ATTT_HTTT_DAU_TU"))
        {
            var longGhepFlag = true;
            var data = await _db.AtttHtttDauTus.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Total = g.Count(), Approved = g.Count(x => x.NgayPheDuyetHsdxcd != null), Embedded = g.Count(x => x.DaLongGhepThuyetMinh == longGhepFlag) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) { var x = data.FirstOrDefault(v => v.DonViId == unitId); Add(unitId, "ATTT_HTTT_DAU_TU", "SO_HE_THONG", x?.Total ?? 0); Add(unitId, "ATTT_HTTT_DAU_TU", "DA_PHE_DUYET_HO_SO", x?.Approved ?? 0); Add(unitId, "ATTT_HTTT_DAU_TU", "DA_LONG_GHEP_THUYET_MINH", x?.Embedded ?? 0); }
        }

        if (requestedModules.Contains("ATTT_GIAI_PHAP"))
        {
            var data = await _db.GiaiPhapAttts.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Protected = g.Sum(x => x.MayTinhBcanetSl + x.MayTinhInternetSl + x.MayTinhLocalSl + x.MayChuBcanetSl + x.MayChuInternetSl + x.MayChuLocalSl), Total = g.Sum(x => x.MayTinhBcanetTs + x.MayTinhInternetTs + x.MayTinhLocalTs + x.MayChuBcanetTs + x.MayChuInternetTs + x.MayChuLocalTs) }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) { var x = data.FirstOrDefault(v => v.DonViId == unitId); var total = x?.Total ?? 0; var protectedCount = x?.Protected ?? 0; Add(unitId, "ATTT_GIAI_PHAP", "TONG_DOI_TUONG", total); Add(unitId, "ATTT_GIAI_PHAP", "DA_DUOC_BAO_VE", protectedCount); AddRatio(unitId, "ATTT_GIAI_PHAP", "TY_LE_BAO_VE", protectedCount, total, Add); }
        }

        if (requestedModules.Contains("VAN_BAN_QPPL"))
        {
            var data = await _db.VanBanQppls.AsNoTracking().Where(x => unitIds.Contains(x.DonViId))
                .GroupBy(x => x.DonViId).Select(g => new { DonViId = g.Key, Total = g.Count() }).ToListAsync(cancellationToken);
            foreach (var unitId in unitIds) Add(unitId, "VAN_BAN_QPPL", "SO_VAN_BAN", data.FirstOrDefault(x => x.DonViId == unitId)?.Total ?? 0);
        }

        var existing = await _db.BaoCaoSnapshotMetrics.Where(x => x.SnapshotId == snapshotId).ToListAsync(cancellationToken);
        if (existing.Count > 0) _db.BaoCaoSnapshotMetrics.RemoveRange(existing);
        await _db.BaoCaoSnapshotMetrics.AddRangeAsync(rows, cancellationToken);
    }

    private static void AddRatio(
        long unitId,
        string module,
        string metric,
        decimal numerator,
        decimal denominator,
        Action<long, string, string, decimal, decimal?, decimal?> add)
    {
        var value = denominator <= 0 ? 0 : Math.Round(numerator * 100m / denominator, 2);
        add(unitId, module, metric, value, numerator, denominator);
    }
}
