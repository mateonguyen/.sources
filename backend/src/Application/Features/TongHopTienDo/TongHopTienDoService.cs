using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Domain.Entities.Reporting;

namespace ThucLuc.Application.Features.TongHopTienDo;

public sealed class TongHopTienDoService : ITongHopTienDoService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TongHopTienDoService(
        IApplicationDbContext db,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _db = db;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<TienDoDonViDto>> GetTienDoAsync(
        TienDoDonViQuery query, CancellationToken ct)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var parentId = query.ParentDonViId ?? currentUser.DonViId;

        var children = await _db.DonVis
            .Where(x => x.ParentId == parentId && x.IsActive)
            .Select(x => new { x.Id, x.TenDonVi, x.CapDonVi })
            .ToListAsync(ct);

        if (children.Count == 0)
            return Array.Empty<TienDoDonViDto>();

        var childIds = children.Select(x => x.Id).ToList();

        // Flag DaXacNhan và UpdatedAt từ KyTrangThaiDonVi
        var trangThaiMap = await _db.KyTrangThaiDonVis
            .Where(x => x.KyBaoCao!.KyCode == query.KyBaoCaoCode && childIds.Contains(x.DonViId))
            .Select(x => new { x.DonViId, x.DaXacNhan, x.UpdatedAt })
            .ToDictionaryAsync(x => x.DonViId, ct);

        // Đếm live từ BIZ_* — GroupBy để tránh N+1
        var nhanLucCounts = await _db.NhanLucCntts
            .Where(x => childIds.Contains(x.DonViId))
            .GroupBy(x => x.DonViId)
            .Select(g => new { DonViId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DonViId, x => x.Count, ct);

        var thietBiCounts = await _db.ThietBiCntts
            .Where(x => childIds.Contains(x.DonViId))
            .GroupBy(x => x.DonViId)
            .Select(g => new { DonViId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DonViId, x => x.Count, ct);

        var htttCounts = await _db.HeThongThongTins
            .Where(x => childIds.Contains(x.DonViId))
            .GroupBy(x => x.DonViId)
            .Select(g => new { DonViId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DonViId, x => x.Count, ct);

        var haTangCounts = await _db.HaTangMangs
            .Where(x => childIds.Contains(x.DonViId))
            .GroupBy(x => x.DonViId)
            .Select(g => new { DonViId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DonViId, x => x.Count, ct);

        var daoTaoCounts = await _db.DaoTaoBoiDuongs
            .Where(x => childIds.Contains(x.DonViId))
            .GroupBy(x => x.DonViId)
            .Select(g => new { DonViId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DonViId, x => x.Count, ct);

        var duAnCounts = await _db.DuAnCntts
            .Where(x => childIds.Contains(x.DonViId))
            .GroupBy(x => x.DonViId)
            .Select(g => new { DonViId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DonViId, x => x.Count, ct);

        return children.Select(c =>
        {
            trangThaiMap.TryGetValue(c.Id, out var tt);
            return new TienDoDonViDto
            {
                DonViId = c.Id,
                TenDonVi = c.TenDonVi,
                CapDonVi = c.CapDonVi ?? string.Empty,
                DaXacNhan = tt?.DaXacNhan ?? false,
                CapNhatLanCuoi = tt?.UpdatedAt,
                SoNhanLuc = nhanLucCounts.GetValueOrDefault(c.Id),
                SoThietBi = thietBiCounts.GetValueOrDefault(c.Id),
                SoHeThongThongTin = htttCounts.GetValueOrDefault(c.Id),
                SoHaTangMang = haTangCounts.GetValueOrDefault(c.Id),
                SoDaoTao = daoTaoCounts.GetValueOrDefault(c.Id),
                SoDuAn = duAnCounts.GetValueOrDefault(c.Id),
            };
        }).ToList();
    }

    public async Task<TienDoDonViDto> GetMyTienDoAsync(string kyBaoCaoCode, CancellationToken ct)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var donViId = currentUser.DonViId;

        var donVi = await _db.DonVis
            .Where(x => x.Id == donViId && x.IsActive)
            .Select(x => new { x.Id, x.TenDonVi, x.CapDonVi })
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException("DON_VI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);

        var tt = await _db.KyTrangThaiDonVis
            .Where(x => x.KyBaoCao!.KyCode == kyBaoCaoCode && x.DonViId == donViId)
            .Select(x => new { x.DaXacNhan, x.UpdatedAt })
            .FirstOrDefaultAsync(ct);

        var soNhanLuc = await _db.NhanLucCntts.CountAsync(x => x.DonViId == donViId, ct);
        var soThietBi = await _db.ThietBiCntts.CountAsync(x => x.DonViId == donViId, ct);
        var soHttt = await _db.HeThongThongTins.CountAsync(x => x.DonViId == donViId, ct);
        var soHaTang = await _db.HaTangMangs.CountAsync(x => x.DonViId == donViId, ct);
        var soDaoTao = await _db.DaoTaoBoiDuongs.CountAsync(x => x.DonViId == donViId, ct);
        var soDuAn = await _db.DuAnCntts.CountAsync(x => x.DonViId == donViId, ct);

        return new TienDoDonViDto
        {
            DonViId = donVi.Id,
            TenDonVi = donVi.TenDonVi,
            CapDonVi = donVi.CapDonVi ?? string.Empty,
            DaXacNhan = tt?.DaXacNhan ?? false,
            CapNhatLanCuoi = tt?.UpdatedAt,
            SoNhanLuc = soNhanLuc,
            SoThietBi = soThietBi,
            SoHeThongThongTin = soHttt,
            SoHaTangMang = soHaTang,
            SoDaoTao = soDaoTao,
            SoDuAn = soDuAn,
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
        await _db.SaveChangesAsync(ct);
    }
}
