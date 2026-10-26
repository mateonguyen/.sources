using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Features.BaoCaoSnapshot;
using ThucLuc.Application.Features.KyBaoCao;
using ThucLuc.Application.Features.MauBaoCao;
using ThucLuc.Application.Features.Users;
using ThucLuc.Domain.Entities.Business;
using ThucLuc.Domain.Entities.Identity;
using ThucLuc.Domain.Enums;
using MauBaoCaoEntity = ThucLuc.Domain.Entities.Reporting.MauBaoCao;

namespace ThucLuc.Infrastructure.Persistence.Seeding;

public interface IDemoReportingDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Seed du lieu demo/QA: 1 mau bao cao gom toan bo module nghiep vu, 4 ky
/// theo quy (~1 nam) tren 10 don vi cap tinh THAT (da import tu Thong tu
/// 58), moi ky duoc nop that qua IBaoCaoSnapshotService de _HIS va
/// BaoCaoSnapshotMetric duoc ghi dung nhu luong nghiep vu thuc, cong voi 10
/// tai khoan test (1 tai khoan/don vi). Chi chay khi Seed:SeedDemoReportingData=true.
/// Idempotent theo tung cap (don vi, ky): moi cap la 1 transaction rieng va se
/// bi bo qua neu da co snapshot Locked/Submitted, nen chay lai sau loi se tu
/// resume tiep phan con thieu ma khong tao du lieu mo coi.
/// </summary>
public sealed class DemoReportingDataSeeder : IDemoReportingDataSeeder
{
    private static readonly string[] TargetMaDonVi =
    [
        "G01.801.000", // Cong an TP Ha Noi
        "G01.899.000", // Cong an TP Ho Chi Minh
        "G01.863.000", // Cong an TP Da Nang
        "G01.839.000", // Cong an TP Hai Phong
        "G01.861.000", // Cong an TP Hue
        "G01.827.000", // Cong an tinh Quang Ninh
        "G01.851.000", // Cong an tinh Thanh Hoa
        "G01.853.000", // Cong an tinh Nghe An
        "G01.895.000", // Cong an tinh Dong Nai
        "G01.873.000", // Cong an tinh Khanh Hoa
        "G01.805.000", // Cong an tinh Cao Bang
        "G01.811.000", // Cong an tinh Lao Cai
        "G01.823.000", // Cong an tinh Thai Nguyen
        "G01.825.000", // Cong an tinh Lang Son
        "G01.835.000", // Cong an tinh Bac Ninh
        "G01.849.000", // Cong an tinh Ninh Binh
        "G01.855.000", // Cong an tinh Ha Tinh
        "G01.867.000", // Cong an tinh Quang Ngai
        "G01.883.000", // Cong an tinh Dak Lak
        "G01.887.000", // Cong an tinh Lam Dong
    ];

    private static readonly (int Nam, int Quy)[] Quarters =
    [
        (2025, 4),
        (2026, 1),
        (2026, 2),
        (2026, 3),
    ];

    // Id co san trong REF_LOAI_THIET_BI (xem db/flyway seed): may tinh ban,
    // may tinh xach tay, router - dai dien cho THIET_BI_CNTT.
    private const long LoaiThietBiMayTinh = 6;
    private const long LoaiThietBiLaptop = 7;
    private const long LoaiThietBiRouter = 11;

    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMauBaoCaoService _mauBaoCaoService;
    private readonly IKyBaoCaoService _kyBaoCaoService;
    private readonly IBaoCaoSnapshotService _snapshotService;
    private readonly IUserService _userService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<DemoReportingDataSeeder> _logger;

    public DemoReportingDataSeeder(
        AppDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        IMauBaoCaoService mauBaoCaoService,
        IKyBaoCaoService kyBaoCaoService,
        IBaoCaoSnapshotService snapshotService,
        IUserService userService,
        IDateTimeProvider dateTimeProvider,
        ILogger<DemoReportingDataSeeder> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _mauBaoCaoService = mauBaoCaoService;
        _kyBaoCaoService = kyBaoCaoService;
        _snapshotService = snapshotService;
        _userService = userService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var admin = await _dbContext.Users.FirstOrDefaultAsync(x => x.UserName == "admin", cancellationToken);
        if (admin is null)
        {
            _logger.LogWarning("Khong tim thay tai khoan 'admin' - bo qua seed demo reporting data.");
            return;
        }

        var units = await _dbContext.DonVis.AsNoTracking()
            .Where(x => TargetMaDonVi.Contains(x.MaDonVi))
            .ToListAsync(cancellationToken);
        if (units.Count != TargetMaDonVi.Length)
        {
            var missing = TargetMaDonVi.Except(units.Select(x => x.MaDonVi));
            _logger.LogWarning("Thieu don vi that de seed demo: {Missing} - bo qua seed demo reporting data.", string.Join(", ", missing));
            return;
        }

        var capTinhRole = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Name == "CAP_TINH", cancellationToken);
        if (capTinhRole is null)
        {
            _logger.LogWarning("Khong tim thay role 'CAP_TINH' - bo qua seed demo reporting data.");
            return;
        }

        var orderedUnits = TargetMaDonVi.Select(ma => units.First(u => u.MaDonVi == ma)).ToList();

        Impersonate(admin.Id, admin.DonViId);
        try
        {
            var mauBaoCao = await GetOrCreateMauBaoCaoAsync(cancellationToken);
            var kyList = await GetOrCreateKyBaoCaoListAsync(mauBaoCao.Id, cancellationToken);

            // Neu chay lai (them don vi moi), ky cuoi cung cua lan chay truoc van con
            // DangMo - phai dong het truoc de vong lap duoi tu do mo lai tung ky theo
            // dung thu tu (chi 1 ky duoc DangMo cung luc voi cung 1 mau bao cao).
            foreach (var openKy in kyList.Where(x => x.TrangThai == KyBaoCaoStatus.DangMo))
            {
                await _kyBaoCaoService.UpdateStatusAsync(openKy.Id, new UpdateKyBaoCaoStatusRequest { TrangThai = KyBaoCaoStatus.DaDong }, cancellationToken);
            }

            var state = orderedUnits.ToDictionary(x => x.Id, _ => new UnitSeedState());

            for (var q = 0; q < kyList.Count; q++)
            {
                var ky = kyList[q];
                if (ky.TrangThai != KyBaoCaoStatus.DangMo)
                {
                    await _kyBaoCaoService.UpdateStatusAsync(ky.Id, new UpdateKyBaoCaoStatusRequest { TrangThai = KyBaoCaoStatus.DangMo }, cancellationToken);
                }

                for (var u = 0; u < orderedUnits.Count; u++)
                {
                    var unit = orderedUnits[u];
                    var alreadySubmitted = await _dbContext.BaoCaoSnapshots.IgnoreQueryFilters().AnyAsync(
                        x => x.KyBaoCaoId == ky.Id && x.DonViId == unit.Id
                          && (x.TrangThai == SnapshotStatus.Locked || x.TrangThai == SnapshotStatus.Submitted),
                        cancellationToken);
                    if (alreadySubmitted)
                    {
                        continue;
                    }

                    // Khong tu mo transaction rieng o day: SubmitAsync/SubmitCurrentAsync
                    // ben duoi da tu quan ly transaction cho phan cua no, mo long tren
                    // cung connection se bi Oracle/EF tu choi. Cac upsert trong
                    // SeedUnitPeriodDataAsync deu nhan lai dong co san qua DonViId
                    // (xem cac cho ??=) nen an toan chay lai neu co loi giua chung.
                    await SeedUnitPeriodDataAsync(unit.Id, unit.TenDonVi, u, q, state[unit.Id], admin.Id, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    await _snapshotService.SubmitCurrentAsync(new SubmitCurrentSnapshotRequest
                    {
                        KyBaoCaoId = ky.Id,
                        DonViId = unit.Id,
                        GhiChu = "Du lieu demo/QA - seed tu dong",
                    }, cancellationToken);
                }

                if (q < kyList.Count - 1 && ky.TrangThai != KyBaoCaoStatus.DaDong)
                {
                    await _kyBaoCaoService.UpdateStatusAsync(ky.Id, new UpdateKyBaoCaoStatusRequest { TrangThai = KyBaoCaoStatus.DaDong }, cancellationToken);
                }
            }

            await SeedDemoAccountsAsync(orderedUnits, capTinhRole.Id, cancellationToken);
        }
        finally
        {
            Impersonate(null, null);
        }

        _logger.LogInformation("Demo reporting data da duoc seed thanh cong: {Units} don vi x {Quarters} ky.", orderedUnits.Count, Quarters.Length);
    }

    private void Impersonate(long? userId, long? donViId)
    {
        if (userId is null)
        {
            _httpContextAccessor.HttpContext = null;
            return;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.Value.ToString()),
            new(ClaimTypes.Name, "demo-seeder"),
            new(Identity.ClaimTypes.DonViId, (donViId ?? 0).ToString()),
            new(Identity.ClaimTypes.Permission, ThucLuc.Application.Security.Permissions.SystemAdmin),
            new(System.Security.Claims.ClaimTypes.Role, "SYSTEM_ADMIN"),
        };
        var identity = new ClaimsIdentity(claims, "DemoSeed");
        _httpContextAccessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    private async Task<MauBaoCaoEntity> GetOrCreateMauBaoCaoAsync(CancellationToken cancellationToken)
    {
        var existing = await _dbContext.MauBaoCaos.FirstOrDefaultAsync(x => x.MaMau == "DEMO_FULL", cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var catalog = await _mauBaoCaoService.GetModuleCatalogAsync(cancellationToken);
        var allModuleCodes = catalog.SelectMany(g => g.Items).Select(i => i.Code).Distinct().ToArray();

        var dto = await _mauBaoCaoService.CreateAsync(new UpsertMauBaoCaoRequest
        {
            MaMau = "DEMO_FULL",
            TenMau = "[DEMO] Bao cao day du nghiep vu",
            MoTa = "Mau bao cao demo/QA gom toan bo module, dung de test man Hien trang & bien dong tren nhieu don vi.",
            TanSuat = TanSuat.Quy,
            DanhSachModule = allModuleCodes,
            IsActive = true,
        }, cancellationToken);

        return await _dbContext.MauBaoCaos.FirstAsync(x => x.Id == dto.Id, cancellationToken);
    }

    private async Task<List<Domain.Entities.Reporting.KyBaoCao>> GetOrCreateKyBaoCaoListAsync(long mauBaoCaoId, CancellationToken cancellationToken)
    {
        var result = new List<Domain.Entities.Reporting.KyBaoCao>();
        foreach (var (nam, quy) in Quarters)
        {
            var existing = await _dbContext.KyBaoCaos.FirstOrDefaultAsync(x => x.MauBaoCaoId == mauBaoCaoId && x.Nam == nam && x.Quy == quy, cancellationToken);
            if (existing is not null)
            {
                result.Add(existing);
                continue;
            }

            var (ngayBatDau, ngayKetThuc) = QuarterRange(nam, quy);
            var dto = await _kyBaoCaoService.CreateAsync(new CreateKyBaoCaoRequest
            {
                MauBaoCaoId = mauBaoCaoId,
                Nam = nam,
                Quy = quy,
                NgayBatDau = ngayBatDau,
                NgayKetThuc = ngayKetThuc,
                TenKy = $"[DEMO] Bao cao day du - Quy {quy}/{nam}",
                GhiChu = "Ky demo/QA - seed tu dong",
            }, cancellationToken);
            result.Add(await _dbContext.KyBaoCaos.FirstAsync(x => x.Id == dto.Id, cancellationToken));
        }
        return result;
    }

    private static (DateOnly Start, DateOnly End) QuarterRange(int nam, int quy)
    {
        var startMonth = (quy - 1) * 3 + 1;
        var start = new DateOnly(nam, startMonth, 1);
        var end = start.AddMonths(3).AddDays(-1);
        return (start, end);
    }

    private async Task SeedDemoAccountsAsync(List<Domain.Entities.System.DonVi> units, long capTinhRoleId, CancellationToken cancellationToken)
    {
        var usernames = new Dictionary<string, string>
        {
            ["G01.801.000"] = "hanoi",
            ["G01.899.000"] = "hcm",
            ["G01.863.000"] = "danang",
            ["G01.839.000"] = "haiphong",
            ["G01.861.000"] = "hue",
            ["G01.827.000"] = "quangninh",
            ["G01.851.000"] = "thanhhoa",
            ["G01.853.000"] = "nghean",
            ["G01.895.000"] = "dongnai",
            ["G01.873.000"] = "khanhhoa",
            ["G01.805.000"] = "caobang",
            ["G01.811.000"] = "laocai",
            ["G01.823.000"] = "thainguyen",
            ["G01.825.000"] = "langson",
            ["G01.835.000"] = "bacninh",
            ["G01.849.000"] = "ninhbinh",
            ["G01.855.000"] = "hatinh",
            ["G01.867.000"] = "quangngai",
            ["G01.883.000"] = "daklak",
            ["G01.887.000"] = "lamdong",
        };

        foreach (var unit in units)
        {
            var username = usernames.GetValueOrDefault(unit.MaDonVi, unit.MaDonVi.ToLowerInvariant());
            var exists = await _dbContext.Users.AnyAsync(x => x.NormalizedUserName == username.ToUpperInvariant(), cancellationToken);
            if (exists)
            {
                continue;
            }

            await _userService.CreateAsync(new CreateUserRequest
            {
                Username = username,
                Password = "Test@123",
                HoTen = $"Tai khoan demo - {unit.TenDonVi}",
                Email = $"{username}@demo.local",
                DonViId = unit.Id,
                RoleIds = [capTinhRoleId],
            }, cancellationToken);
        }
    }

    private sealed class UnitSeedState
    {
        public List<long> HtttIds { get; } = [];
        public long? ThietBiMayTinhId { get; set; }
        public long? ThietBiLaptopId { get; set; }
        public long? ThietBiRouterId { get; set; }
        public long? HaTangMangId { get; set; }
        public long? GiamSatNocId { get; set; }
        public long? GiamSatSocId { get; set; }
        public long? CameraQuanLyId { get; set; }
        public long? CameraThucTrangId { get; set; }
        public long? HtttTieuChuanId { get; set; }
        public List<long> GiaiPhapIds { get; } = [];
        public List<long> AtttVanHanhIds { get; } = [];
        public List<long> AtttDauTuIds { get; } = [];
        public long? DaoTaoHocVienId { get; set; }
        public int NhanLucCount { get; set; }
    }

    private async Task SeedUnitPeriodDataAsync(long donViId, string tenDonVi, int u, int q, UnitSeedState state, long adminId, CancellationToken cancellationToken)
    {
        var nam = Quarters[q].Nam;
        var now = _dateTimeProvider.Now;

        // NHAN_LUC_CNTT: tang truong theo quy (them nhan su moi, khong xoa).
        var targetHeadcount = 15 + u * 3 + q * 2;
        var trinhDoOptions = new[] { "DAI_HOC", "CAO_DANG", "THAC_SI", "TRUNG_CAP" };
        for (var i = state.NhanLucCount; i < targetHeadcount; i++)
        {
            _dbContext.NhanLucCntts.Add(new NhanLucCntt
            {
                NhanSuKey = Guid.NewGuid().ToString("N")[..32],
                DonViId = donViId,
                ValidFrom = now,
                VersionNo = 1,
                HoTen = $"Can bo CNTT {u + 1}-{i + 1}",
                GioiTinh = i % 2 == 0 ? "NAM" : "NU",
                ChucVu = "Can bo CNTT",
                LoaiNhanLuc = i % 3 == 0 ? "KIEM_NHIEM" : "CHUYEN_TRACH",
                TrinhDoCntt = trinhDoOptions[i % trinhDoOptions.Length],
                TrinhDoLlct = "SO_CAP",
                CreatedBy = adminId,
                UpdatedBy = adminId,
            });
        }
        state.NhanLucCount = targetHeadcount;

        // NANG_LUC_SO: 3 nhom vi tri, cap nhat tai cho.
        foreach (var (nhom, idx) in new[] { ("NHOM_1", 0), ("NHOM_2", 1), ("NHOM_7", 2) })
        {
            var existing = await _dbContext.NangLucSos.FirstOrDefaultAsync(x => x.DonViId == donViId && x.NhomViTri == nhom, cancellationToken);
            var total = 8 + u + idx * 4 + q * 2;
            var dat = Math.Min(total, 3 + u + q * 2);
            if (existing is null)
            {
                _dbContext.NangLucSos.Add(new NangLucSo { DonViId = donViId, NhomViTri = nhom, TongSoDienDanhGia = total, TongSoDat = dat, TongSoChuaDat = total - dat, CreatedBy = adminId, UpdatedBy = adminId });
            }
            else
            {
                existing.TongSoDienDanhGia = total;
                existing.TongSoDat = dat;
                existing.TongSoChuaDat = total - dat;
                existing.UpdatedBy = adminId;
            }
        }

        // DAO_TAO_BOI_DUONG: them 1 khoa moi moi quy.
        var (quyStart, quyEnd) = QuarterRange(nam, Quarters[q].Quy);
        _dbContext.DaoTaoBoiDuongs.Add(new DaoTaoBoiDuong
        {
            DonViId = donViId,
            TenKhoaHoc = $"Boi duong CNTT Quy {Quarters[q].Quy}/{nam}",
            DonViToChuc = "Cuc Cong nghe thong tin",
            HinhThuc = q % 2 == 0 ? "Truc tuyen" : "Truc tiep",
            SoLuongHv = 10 + u + q * 2,
            ThoiGianTu = quyStart,
            ThoiGianDen = quyEnd,
            CreatedBy = adminId,
            UpdatedBy = adminId,
        });

        // DAO_TAO_HOC_VIEN: unique(DonViId, NoiDungDaoTao) - 1 dong "hien tai" duy
        // nhat, Nam chi la truong du lieu duoc cap nhat theo quy hien tai.
        state.DaoTaoHocVienId ??= await _dbContext.DaoTaoHocViens
            .Where(x => x.DonViId == donViId && x.NoiDungDaoTao == "CHUYEN_NGANH_CNTT")
            .Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (state.DaoTaoHocVienId is null)
        {
            var entity = new DaoTaoHocVien
            {
                DonViId = donViId,
                Nam = nam,
                NoiDungDaoTao = "CHUYEN_NGANH_CNTT",
                SoTienSi = 0,
                SoThacSi = 1 + (u % 3),
                SoDaiHoc = 5 + u,
                SoCaoDang = 2 + (u % 2),
                SoTrungCap = 1,
                CreatedBy = adminId,
                UpdatedBy = adminId,
            };
            _dbContext.DaoTaoHocViens.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            state.DaoTaoHocVienId = entity.Id;
        }
        else
        {
            var entity = await _dbContext.DaoTaoHocViens.FirstAsync(x => x.Id == state.DaoTaoHocVienId.Value, cancellationToken);
            entity.Nam = nam;
            entity.SoDaiHoc += 1;
            entity.UpdatedBy = adminId;
        }

        // HE_THONG_THONG_TIN: them 1 he thong moi moi quy, danh dau sang kien tu quy 2.
        var httt = new HeThongThongTin
        {
            DonViId = donViId,
            LoaiPhanMem = q % 2 == 0 ? Domain.Enums.LoaiPhanMemCodes.DungChung : Domain.Enums.LoaiPhanMemCodes.TuPhatTrien,
            TenPhanMem = $"He thong nghiep vu {u + 1}-{state.HtttIds.Count + 1}",
            DonViPhatTrien = "Cuc Cong nghe thong tin",
            DonViQuanLy = tenDonVi,
            NamTrienKhai = nam,
            DaCongNhanSangKien = state.HtttIds.Count == 0 && q >= 2,
            ValidFrom = now,
            VersionNo = 1,
            CreatedBy = adminId,
            UpdatedBy = adminId,
        };
        _dbContext.HeThongThongTins.Add(httt);
        await _dbContext.SaveChangesAsync(cancellationToken);
        state.HtttIds.Add(httt.Id);

        // HTTT_TIEU_CHUAN: 1 dong/don vi, cap nhat tai cho. Nhan lai dong co san
        // (neu don vi that da co du lieu tu truoc) de tranh vi pham unique(DonViId).
        state.HtttTieuChuanId ??= await _dbContext.HtttTieuChuans.Where(x => x.DonViId == donViId).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (state.HtttTieuChuanId is null)
        {
            var entity = new HtttTieuChuan { DonViId = donViId, TenHeThong = "He thong quan ly van ban va dieu hanh trong CAND", Dvt = "diem", SoH05 = 1, SoTinh = 1 + q, SoXa = 1 + q, SoDvTrucThuocBo = 0, ValidFrom = now, VersionNo = 1, CreatedBy = adminId, UpdatedBy = adminId };
            _dbContext.HtttTieuChuans.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            state.HtttTieuChuanId = entity.Id;
        }
        else
        {
            var entity = await _dbContext.HtttTieuChuans.FirstAsync(x => x.Id == state.HtttTieuChuanId.Value, cancellationToken);
            entity.SoTinh = 1 + q;
            entity.SoXa = 1 + q;
            entity.UpdatedBy = adminId;
        }

        // DU_AN_CNTT: them 1 du an moi moi quy.
        var nguonVonOptions = new[] { "NGAN_SACH_NHA_NUOC", "BO_CONG_AN", "DIA_PHUONG" };
        _dbContext.DuAnCntts.Add(new DuAnCntt
        {
            DonViId = donViId,
            TenDuAn = $"Du an nang cap ha tang CNTT {u + 1}-{q + 1}",
            DonViChuTri = tenDonVi,
            NamTrienKhai = nam,
            NamDuaVaoSuDung = q >= 2 ? nam : null,
            TongKinhPhi = 500_000_000m + u * 20_000_000m + q * 50_000_000m,
            NguonVon = nguonVonOptions[q % nguonVonOptions.Length],
            CreatedBy = adminId,
            UpdatedBy = adminId,
        });

        // THIET_BI_CNTT: 3 dong (may tinh/laptop/router), cap nhat tai cho, giam dan thiet bi hong.
        state.ThietBiMayTinhId = await UpsertThietBiAsync(state.ThietBiMayTinhId, donViId, LoaiThietBiMayTinh, "May tinh de ban", 40 + u * 3 + q * 2, Math.Max(0, 6 - q), adminId, cancellationToken);
        state.ThietBiLaptopId = await UpsertThietBiAsync(state.ThietBiLaptopId, donViId, LoaiThietBiLaptop, "May tinh xach tay", 15 + u * 2 + q, Math.Max(0, 3 - q), adminId, cancellationToken);
        state.ThietBiRouterId = await UpsertThietBiAsync(state.ThietBiRouterId, donViId, LoaiThietBiRouter, "Router", 5 + u, Math.Max(0, 2 - q), adminId, cancellationToken);

        // HA_TANG_MANG: 1 dong/don vi, cap nhat tai cho.
        state.HaTangMangId ??= await _dbContext.HaTangMangs.Where(x => x.DonViId == donViId).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (state.HaTangMangId is null)
        {
            var entity = new HaTangMang { DonViId = donViId, SoDonViTrucThuoc = 5 + u, SoDaKetNoiBcanet = 2 + q, SoDuongTruyenVnpt = 2, SoDuongTruyenKhac = 1, SoKetNoiInternet = 2 + q, CreatedBy = adminId, UpdatedBy = adminId };
            _dbContext.HaTangMangs.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            state.HaTangMangId = entity.Id;
        }
        else
        {
            var entity = await _dbContext.HaTangMangs.FirstAsync(x => x.Id == state.HaTangMangId.Value, cancellationToken);
            entity.SoDaKetNoiBcanet = 2 + q;
            entity.SoKetNoiInternet = 2 + q;
            entity.UpdatedBy = adminId;
        }

        // GIAM_SAT_NOC: 1 dong/don vi, ty le giam sat tang dan.
        state.GiamSatNocId ??= await _dbContext.GiamSatNocs.Where(x => x.DonViId == donViId).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (state.GiamSatNocId is null)
        {
            var total = 20 + u * 2;
            var entity = new GiamSatNoc { DonViId = donViId, LopGiamSat = "CAP_TINH", CoNoc = true, ThucTrang = "DANG_VAN_HANH", TongSoDoiTuong = total, SoDaGiamSat = Math.Min(total, 8 + u + q * 3), CreatedBy = adminId, UpdatedBy = adminId };
            _dbContext.GiamSatNocs.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            state.GiamSatNocId = entity.Id;
        }
        else
        {
            var entity = await _dbContext.GiamSatNocs.FirstAsync(x => x.Id == state.GiamSatNocId.Value, cancellationToken);
            entity.SoDaGiamSat = Math.Min(entity.TongSoDoiTuong, 8 + u + q * 3);
            entity.UpdatedBy = adminId;
        }

        // GIAM_SAT_SOC: 1 dong/don vi, su co giam dan (cai thien).
        state.GiamSatSocId ??= await _dbContext.GiamSatSocs.Where(x => x.DonViId == donViId).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (state.GiamSatSocId is null)
        {
            var total = 15 + u;
            var entity = new GiamSatSoc { DonViId = donViId, LoaiMang = "INTERNET", LopGiamSat = "CAP_TINH", CoHeThong = true, ThucTrang = "DANG_VAN_HANH", TongSoDoiTuong = total, SoGiamSatMotPhan = Math.Max(0, total - q * 3), SoGiamSatCoBan = q, SoGiamSatDayDu = Math.Min(total, q * 2), SoSuCo = Math.Max(0, 5 - q), SoSuCoDaKhacPhuc = Math.Max(0, 5 - q - 1), LucLuongUngCuu = "Doi ung cuu su co cap tinh", CreatedBy = adminId, UpdatedBy = adminId };
            _dbContext.GiamSatSocs.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            state.GiamSatSocId = entity.Id;
        }
        else
        {
            var entity = await _dbContext.GiamSatSocs.FirstAsync(x => x.Id == state.GiamSatSocId.Value, cancellationToken);
            entity.SoGiamSatDayDu = Math.Min(entity.TongSoDoiTuong, q * 2);
            entity.SoSuCo = Math.Max(0, 5 - q);
            entity.SoSuCoDaKhacPhuc = Math.Max(0, 5 - q - 1);
            entity.UpdatedBy = adminId;
        }

        // CAMERA_QUAN_LY: 1 dong/don vi, cap nhat tai cho.
        state.CameraQuanLyId ??= await _dbContext.CameraQuanLies.Where(x => x.DonViId == donViId).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (state.CameraQuanLyId is null)
        {
            var entity = new CameraQuanLy { DonViId = donViId, NhomCamera = "NHOM_1", TenDonViDiaChi = $"Tru so {tenDonVi}", BuongGiamTrangBiSl = 10 + u + q, BuongGiamTrangBiTs = 15 + u, NhuCauDauTu = Math.Max(0, 5 - q), BaoTri = 1, SuaChua = 1, SoLanViPham = 0, HoSoCapDoAttt = 1, CbChuyenTrach = 2, CbKiemNhiem = 1, CbDiaPhuong = 1, DaoTaoBo = 1, DaoTaoNhuCau = 1, CreatedBy = adminId, UpdatedBy = adminId };
            _dbContext.CameraQuanLies.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            state.CameraQuanLyId = entity.Id;
        }
        else
        {
            var entity = await _dbContext.CameraQuanLies.FirstAsync(x => x.Id == state.CameraQuanLyId.Value, cancellationToken);
            entity.BuongGiamTrangBiSl = 10 + u + q;
            entity.NhuCauDauTu = Math.Max(0, 5 - q);
            entity.UpdatedBy = adminId;
        }

        // CAMERA_THUC_TRANG: 1 dong/don vi, cap nhat tai cho.
        state.CameraThucTrangId ??= await _dbContext.CameraThucTrangs.Where(x => x.DonViId == donViId).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (state.CameraThucTrangId is null)
        {
            var entity = new CameraThucTrang { DonViId = donViId, NhomCamera = "NHOM_1", TenHeThong = $"He thong camera giam sat {tenDonVi}", CauHinhIp = 20 + u + q * 2, CauHinhAnalog = 5, ThucTrangIp = 18 + u + q * 2, ThucTrangAnalog = 4, ChuDauTu = "Cong an tinh", NamDauTu = 2024, DuongTruyen = "Cap quang", PhanMem = "VMS", LuuTru = "NAS", CreatedBy = adminId, UpdatedBy = adminId };
            _dbContext.CameraThucTrangs.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            state.CameraThucTrangId = entity.Id;
        }
        else
        {
            var entity = await _dbContext.CameraThucTrangs.FirstAsync(x => x.Id == state.CameraThucTrangId.Value, cancellationToken);
            entity.CauHinhIp = 20 + u + q * 2;
            entity.ThucTrangIp = 18 + u + q * 2;
            entity.UpdatedBy = adminId;
        }

        // ATTT_HTTT_VAN_HANH / ATTT_HTTT_DAU_TU: gan voi 2 he thong dau tien, trien khai dan qua cac quy.
        var linkedHtttIds = state.HtttIds.Take(2).ToArray();
        for (var i = 0; i < linkedHtttIds.Length; i++)
        {
            var htttId = linkedHtttIds[i];
            if (state.AtttVanHanhIds.Count <= i)
            {
                var entity = new AtttHtttVanHanh { DonViId = donViId, HtttId = htttId, LoaiHaTang = "BCANET", ChuQuan = tenDonVi, DonViVanHanh = tenDonVi, CapDoDeXuat = "CAP_2", TinhTrangPheDuyet = "DA_PHE_DUYET", DaTrienKhaiPhuongAn = false, CreatedBy = adminId, UpdatedBy = adminId };
                _dbContext.AtttHtttVanHanhs.Add(entity);
                await _dbContext.SaveChangesAsync(cancellationToken);
                state.AtttVanHanhIds.Add(entity.Id);
            }
            if (q >= 2 - i)
            {
                var entity = await _dbContext.AtttHtttVanHanhs.FirstAsync(x => x.Id == state.AtttVanHanhIds[i], cancellationToken);
                entity.DaTrienKhaiPhuongAn = true;
                entity.TrangThaiTrienKhaiPhuongAn = "HOAN_THANH";
                entity.UpdatedBy = adminId;
            }

            if (state.AtttDauTuIds.Count <= i)
            {
                var entity = new AtttHtttDauTu { DonViId = donViId, HtttId = htttId, LoaiHaTang = "BCANET", ChuQuan = tenDonVi, DonViVanHanh = tenDonVi, CapDoDeXuat = "CAP_2", DaLongGhepThuyetMinh = false, CreatedBy = adminId, UpdatedBy = adminId };
                _dbContext.AtttHtttDauTus.Add(entity);
                await _dbContext.SaveChangesAsync(cancellationToken);
                state.AtttDauTuIds.Add(entity.Id);
            }
            if (q >= 1)
            {
                var entity = await _dbContext.AtttHtttDauTus.FirstAsync(x => x.Id == state.AtttDauTuIds[i], cancellationToken);
                entity.DaLongGhepThuyetMinh = true;
                entity.NgayPheDuyetHsdxcd ??= q >= 2 ? quyStart : null;
                entity.UpdatedBy = adminId;
            }
        }

        // ATTT_GIAI_PHAP: Antivirus + Firewall, ty le bao ve tang dan.
        var giaiPhapNames = new[] { "Phan mem Antivirus", "Tuong lua (Firewall)" };
        for (var i = 0; i < giaiPhapNames.Length; i++)
        {
            var tsBcanet = 50 + u * 3;
            var slBcanet = Math.Min(tsBcanet, 20 + u * 2 + q * 8);
            if (state.GiaiPhapIds.Count <= i)
            {
                // unique(DonViId, TenGiaiPhap) - nhan lai dong co san neu don vi that da co.
                var existingId = await _dbContext.GiaiPhapAttts.Where(x => x.DonViId == donViId && x.TenGiaiPhap == giaiPhapNames[i]).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
                if (existingId.HasValue)
                {
                    state.GiaiPhapIds.Add(existingId.Value);
                    var existing = await _dbContext.GiaiPhapAttts.FirstAsync(x => x.Id == existingId.Value, cancellationToken);
                    existing.MayTinhBcanetSl = slBcanet;
                    existing.UpdatedBy = adminId;
                    continue;
                }
                var entity = new GiaiPhapAttt
                {
                    DonViId = donViId,
                    TenGiaiPhap = giaiPhapNames[i],
                    MayTinhBcanetTs = tsBcanet,
                    MayTinhBcanetSl = slBcanet,
                    MayTinhInternetTs = 10 + u,
                    MayTinhInternetSl = Math.Min(10 + u, 4 + q * 2),
                    MayTinhLocalTs = 5,
                    MayTinhLocalSl = Math.Min(5, q + 1),
                    MayChuBcanetTs = 4 + (u % 3),
                    MayChuBcanetSl = Math.Min(4 + (u % 3), q + 1),
                    MayChuInternetTs = 2,
                    MayChuInternetSl = Math.Min(2, q),
                    MayChuLocalTs = 1,
                    MayChuLocalSl = q >= 2 ? 1 : 0,
                    CreatedBy = adminId,
                    UpdatedBy = adminId,
                };
                _dbContext.GiaiPhapAttts.Add(entity);
                await _dbContext.SaveChangesAsync(cancellationToken);
                state.GiaiPhapIds.Add(entity.Id);
            }
            else
            {
                var entity = await _dbContext.GiaiPhapAttts.FirstAsync(x => x.Id == state.GiaiPhapIds[i], cancellationToken);
                entity.MayTinhBcanetSl = slBcanet;
                entity.MayTinhInternetSl = Math.Min(entity.MayTinhInternetTs, 4 + q * 2);
                entity.MayTinhLocalSl = Math.Min(entity.MayTinhLocalTs, q + 1);
                entity.MayChuBcanetSl = Math.Min(entity.MayChuBcanetTs, q + 1);
                entity.MayChuInternetSl = Math.Min(entity.MayChuInternetTs, q);
                entity.MayChuLocalSl = q >= 2 ? 1 : 0;
                entity.UpdatedBy = adminId;
            }
        }

        // VAN_BAN_QPPL: them 1 van ban moi moi quy.
        var loaiVanBanOptions = new[] { "QUYET_DINH", "THONG_TU", "CHI_THI" };
        var coQuanOptions = new[] { "BCA", "UBND_TINH", "BTTTT" };
        var linhVucOptions = new[] { "CNTT_VIEN_THONG", "AN_TOAN_TT", "CHUYEN_DOI_SO" };
        _dbContext.VanBanQppls.Add(new VanBanQppl
        {
            DonViId = donViId,
            SoHieu = $"{100 + u}-Q{Quarters[q].Quy}/QD-{nam}",
            TenVanBan = $"Quyet dinh ve cong tac CNTT Quy {Quarters[q].Quy}/{nam}",
            LoaiVanBan = loaiVanBanOptions[q % loaiVanBanOptions.Length],
            CoQuanBanHanh = coQuanOptions[q % coQuanOptions.Length],
            NgayBanHanh = quyStart,
            NgayHieuLuc = quyStart.AddDays(15),
            LinhVuc = linhVucOptions[q % linhVucOptions.Length],
            TrichYeu = "Chi dao cong tac CNTT va chuyen doi so",
            TinhTrangTrienKhai = "DANG_TRIEN_KHAI",
            CreatedBy = adminId,
            UpdatedBy = adminId,
        });
    }

    private async Task<long> UpsertThietBiAsync(long? existingId, long donViId, long loaiThietBiId, string ten, int total, int broken, long adminId, CancellationToken cancellationToken)
    {
        existingId ??= await _dbContext.ThietBiCntts.Where(x => x.DonViId == donViId && x.LoaiThietBiId == loaiThietBiId).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (existingId is null)
        {
            var entity = new ThietBiCntt
            {
                DonViId = donViId,
                LoaiThietBiId = loaiThietBiId,
                TenThietBi = ten,
                SoLuongTong = total,
                SoLuongHienDung = total - broken,
                SoLuongHong = broken,
                TinhTrang = "DANG_SU_DUNG",
                ValidFrom = _dateTimeProvider.Now,
                VersionNo = 1,
                CreatedBy = adminId,
                UpdatedBy = adminId,
            };
            _dbContext.ThietBiCntts.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return entity.Id;
        }

        var existing = await _dbContext.ThietBiCntts.FirstAsync(x => x.Id == existingId.Value, cancellationToken);
        existing.SoLuongTong = total;
        existing.SoLuongHong = broken;
        existing.SoLuongHienDung = total - broken;
        existing.UpdatedBy = adminId;
        return existing.Id;
    }
}
