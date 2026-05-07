using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using GiamSatSocEntity = ThucLuc.Domain.Entities.Business.GiamSatSoc;
using GiamSatSocHisEntity = ThucLuc.Domain.Entities.Business.GiamSatSocHis;

namespace ThucLuc.Application.Features.GiamSatSoc;

public interface IGiamSatSocService
{
    Task<IReadOnlyCollection<GiamSatSocDto>> GetAllAsync(GiamSatSocQuery query, CancellationToken cancellationToken = default);

    Task<GiamSatSocDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<GiamSatSocDto> UpsertAsync(long? id, UpsertGiamSatSocRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GiamSatSocDto>> SaveMatrixAsync(SaveGiamSatSocMatrixRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class GiamSatSocService : IGiamSatSocService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GiamSatSocService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<GiamSatSocDto>> GetAllAsync(GiamSatSocQuery query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeQuery(query);

        if (!string.IsNullOrWhiteSpace(normalizedQuery.KyBaoCaoCode))
        {
            var hisQuery = _dbContext.GiamSatSocHis.AsNoTracking()
                .Where(x => x.KyBaoCaoCode == normalizedQuery.KyBaoCaoCode)
                .Where(x => ApplyDonViScopePredicate(x.DonViId));

            if (normalizedQuery.DonViId.HasValue)
            {
                hisQuery = hisQuery.Where(x => x.DonViId == normalizedQuery.DonViId.Value);
            }

            if (!string.IsNullOrWhiteSpace(normalizedQuery.LoaiMang))
            {
                hisQuery = hisQuery.Where(x => x.LoaiMang == normalizedQuery.LoaiMang);
            }

            if (!string.IsNullOrWhiteSpace(normalizedQuery.LopGiamSat))
            {
                hisQuery = hisQuery.Where(x => x.LopGiamSat == normalizedQuery.LopGiamSat);
            }

            return await hisQuery
                .OrderBy(x => x.DonViId)
                .ThenBy(x => x.LoaiMang)
                .ThenBy(x => x.LopGiamSat)
                .Select(ToHisDto())
                .ToListAsync(cancellationToken);
        }

        var liveQuery = ApplyReadScope(_dbContext.GiamSatSocs);
        if (normalizedQuery.DonViId.HasValue)
        {
            liveQuery = liveQuery.Where(x => x.DonViId == normalizedQuery.DonViId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery.LoaiMang))
        {
            liveQuery = liveQuery.Where(x => x.LoaiMang == normalizedQuery.LoaiMang);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery.LopGiamSat))
        {
            liveQuery = liveQuery.Where(x => x.LopGiamSat == normalizedQuery.LopGiamSat);
        }

        return await liveQuery
            .OrderBy(x => x.DonViId)
            .ThenBy(x => x.LoaiMang)
            .ThenBy(x => x.LopGiamSat)
            .Select(ToLiveDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<GiamSatSocDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.GiamSatSocs)
            .Where(x => x.Id == id)
            .Select(ToLiveDto())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<GiamSatSocDto> UpsertAsync(long? id, UpsertGiamSatSocRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedRequest = NormalizeRequest(request);
        await EnsureValidScopeAsync(normalizedRequest.DonViId, cancellationToken);

        GiamSatSocEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.GiamSatSocs).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("GSS_NOT_FOUND", "Khong tim thay ban ghi giam sat SOC.", 404);
        }
        else
        {
            entity = new GiamSatSocEntity();
            await _dbContext.GiamSatSocs.AddAsync(entity, cancellationToken);
        }

        ApplyValues(entity, normalizedRequest);
        await SyncCoHeThongAsync(entity, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<IReadOnlyCollection<GiamSatSocDto>> SaveMatrixAsync(SaveGiamSatSocMatrixRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        var items = (request.Items ?? [])
            .Select(NormalizeRequest)
            .ToList();

        ValidateMatrix(request.DonViId, items);

        var currentItems = await _dbContext.GiamSatSocs
            .IgnoreQueryFilters()
            .Where(x => x.DonViId == request.DonViId)
            .ToListAsync(cancellationToken);

        foreach (var item in currentItems)
        {
            item.DeletedAt = _dateTimeProvider.Now;
        }

        var coHeThongByLoaiMang = items
            .GroupBy(x => x.LoaiMang, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().CoHeThong, StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var entity = currentItems.FirstOrDefault(x =>
                string.Equals(x.LoaiMang, item.LoaiMang, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.LopGiamSat, item.LopGiamSat, StringComparison.OrdinalIgnoreCase));

            if (entity is null)
            {
                entity = new GiamSatSocEntity();
                await _dbContext.GiamSatSocs.AddAsync(entity, cancellationToken);
            }

            ApplyValues(entity, new UpsertGiamSatSocRequest
            {
                DonViId = item.DonViId,
                LoaiMang = item.LoaiMang,
                LopGiamSat = item.LopGiamSat,
                CoHeThong = coHeThongByLoaiMang[item.LoaiMang],
                ThucTrang = item.ThucTrang,
                TongSoDoiTuong = item.TongSoDoiTuong,
                SoGiamSatMotPhan = item.SoGiamSatMotPhan,
                SoGiamSatCoBan = item.SoGiamSatCoBan,
                SoGiamSatDayDu = item.SoGiamSatDayDu,
                SoSuCo = item.SoSuCo,
                SoSuCoDaKhacPhuc = item.SoSuCoDaKhacPhuc,
                LucLuongUngCuu = item.LucLuongUngCuu,
                GhiChu = item.GhiChu
            });
            entity.DeletedAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAllAsync(new GiamSatSocQuery { DonViId = request.DonViId }, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.GiamSatSocs).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("GSS_NOT_FOUND", "Khong tim thay ban ghi giam sat SOC.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<GiamSatSocEntity> ApplyReadScope(IQueryable<GiamSatSocEntity> query)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0)
        {
            query = query.Where(x => x.DonViId == currentUser.DonViId);
        }

        return query;
    }

    private bool ApplyDonViScopePredicate(long donViId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        return HasCrossDonViPermission(currentUser) || currentUser.DonViId <= 0 || currentUser.DonViId == donViId;
    }

    private async Task EnsureValidScopeAsync(long donViId, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0 && currentUser.DonViId != donViId)
        {
            throw new AppException("GSS_SCOPE_DENIED", "Khong co quyen thao tac du lieu giam sat SOC cua don vi khac.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Khong tim thay don vi.", 404);
        }
    }

    private async Task SyncCoHeThongAsync(GiamSatSocEntity entity, CancellationToken cancellationToken)
    {
        var siblings = await ApplyReadScope(_dbContext.GiamSatSocs)
            .Where(x =>
                x.DonViId == entity.DonViId &&
                x.LoaiMang == entity.LoaiMang &&
                x.Id != entity.Id)
            .ToListAsync(cancellationToken);

        foreach (var sibling in siblings)
        {
            sibling.CoHeThong = entity.CoHeThong;
        }
    }

    private static System.Linq.Expressions.Expression<Func<GiamSatSocEntity, GiamSatSocDto>> ToLiveDto()
        => x => new GiamSatSocDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            KyBaoCaoCode = null,
            LoaiMang = x.LoaiMang,
            LopGiamSat = x.LopGiamSat,
            CoHeThong = x.CoHeThong,
            ThucTrang = x.ThucTrang,
            TongSoDoiTuong = x.TongSoDoiTuong,
            SoGiamSatMotPhan = x.SoGiamSatMotPhan,
            SoGiamSatCoBan = x.SoGiamSatCoBan,
            SoGiamSatDayDu = x.SoGiamSatDayDu,
            SoSuCo = x.SoSuCo,
            SoSuCoDaKhacPhuc = x.SoSuCoDaKhacPhuc,
            LucLuongUngCuu = x.LucLuongUngCuu,
            GhiChu = x.GhiChu
        };

    private static System.Linq.Expressions.Expression<Func<GiamSatSocHisEntity, GiamSatSocDto>> ToHisDto()
        => x => new GiamSatSocDto
        {
            Id = x.SourceId,
            DonViId = x.DonViId,
            KyBaoCaoCode = x.KyBaoCaoCode,
            LoaiMang = x.LoaiMang,
            LopGiamSat = x.LopGiamSat,
            CoHeThong = x.CoHeThong,
            ThucTrang = x.ThucTrang,
            TongSoDoiTuong = x.TongSoDoiTuong,
            SoGiamSatMotPhan = x.SoGiamSatMotPhan,
            SoGiamSatCoBan = x.SoGiamSatCoBan,
            SoGiamSatDayDu = x.SoGiamSatDayDu,
            SoSuCo = x.SoSuCo,
            SoSuCoDaKhacPhuc = x.SoSuCoDaKhacPhuc,
            LucLuongUngCuu = x.LucLuongUngCuu,
            GhiChu = x.GhiChu
        };

    private static void ApplyValues(GiamSatSocEntity entity, UpsertGiamSatSocRequest request)
    {
        entity.DonViId = request.DonViId;
        entity.LoaiMang = request.LoaiMang;
        entity.LopGiamSat = request.LopGiamSat;
        entity.CoHeThong = request.CoHeThong;
        entity.ThucTrang = request.ThucTrang;
        entity.TongSoDoiTuong = request.TongSoDoiTuong;
        entity.SoGiamSatMotPhan = request.SoGiamSatMotPhan;
        entity.SoGiamSatCoBan = request.SoGiamSatCoBan;
        entity.SoGiamSatDayDu = request.SoGiamSatDayDu;
        entity.SoSuCo = request.SoSuCo;
        entity.SoSuCoDaKhacPhuc = request.SoSuCoDaKhacPhuc;
        entity.LucLuongUngCuu = request.LucLuongUngCuu;
        entity.GhiChu = request.GhiChu;
    }

    private static GiamSatSocQuery NormalizeQuery(GiamSatSocQuery query)
        => new()
        {
            DonViId = query.DonViId,
            LoaiMang = NormalizeCode(query.LoaiMang),
            LopGiamSat = NormalizeCode(query.LopGiamSat),
            KyBaoCaoCode = NormalizeCode(query.KyBaoCaoCode)
        };

    private static UpsertGiamSatSocRequest NormalizeRequest(UpsertGiamSatSocRequest request)
        => new()
        {
            DonViId = request.DonViId,
            LoaiMang = NormalizeRequiredCode(request.LoaiMang, nameof(request.LoaiMang)),
            LopGiamSat = NormalizeRequiredCode(request.LopGiamSat, nameof(request.LopGiamSat)),
            CoHeThong = request.CoHeThong,
            ThucTrang = NormalizeCode(request.ThucTrang),
            TongSoDoiTuong = request.TongSoDoiTuong,
            SoGiamSatMotPhan = request.SoGiamSatMotPhan,
            SoGiamSatCoBan = request.SoGiamSatCoBan,
            SoGiamSatDayDu = request.SoGiamSatDayDu,
            SoSuCo = request.SoSuCo,
            SoSuCoDaKhacPhuc = request.SoSuCoDaKhacPhuc,
            LucLuongUngCuu = NormalizeText(request.LucLuongUngCuu),
            GhiChu = NormalizeText(request.GhiChu)
        };

    private static void ValidateMatrix(long donViId, List<UpsertGiamSatSocRequest> items)
    {
        var duplicateKey = items
            .GroupBy(x => $"{x.LoaiMang}::{x.LopGiamSat}", StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;

        if (duplicateKey is not null)
        {
            throw new AppException("GSS_DUPLICATE_MATRIX", "Du lieu giam sat SOC bi trung loai mang/lop giam sat.", 400);
        }

        var invalidScope = items.Any(x => x.DonViId != donViId);
        if (invalidScope)
        {
            throw new AppException("GSS_INVALID_SCOPE", "Cac dong giam sat SOC phai cung don vi.", 400);
        }
    }

    private static string NormalizeRequiredCode(string? value, string fieldName)
        => NormalizeCode(value) ?? throw new AppException("GSS_INVALID_REQUEST", $"Truong {fieldName} la bat buoc.", 400);

    private static string? NormalizeCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}
