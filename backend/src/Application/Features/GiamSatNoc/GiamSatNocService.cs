using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using GiamSatNocEntity = ThucLuc.Domain.Entities.Business.GiamSatNoc;
using GiamSatNocHisEntity = ThucLuc.Domain.Entities.Business.GiamSatNocHis;

namespace ThucLuc.Application.Features.GiamSatNoc;

public interface IGiamSatNocService
{
    Task<IReadOnlyCollection<GiamSatNocDto>> GetAllAsync(GiamSatNocQuery query, CancellationToken cancellationToken = default);

    Task<GiamSatNocDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<GiamSatNocDto> UpsertAsync(long? id, UpsertGiamSatNocRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GiamSatNocDto>> SaveMatrixAsync(SaveGiamSatNocMatrixRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class GiamSatNocService : IGiamSatNocService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GiamSatNocService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<GiamSatNocDto>> GetAllAsync(GiamSatNocQuery query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeQuery(query);

        if (!string.IsNullOrWhiteSpace(normalizedQuery.KyBaoCaoCode))
        {
            var hisQuery = _dbContext.GiamSatNocHis.AsNoTracking()
                .Where(x => x.KyBaoCaoCode == normalizedQuery.KyBaoCaoCode)
                .Where(x => ApplyDonViScopePredicate(x.DonViId));

            if (normalizedQuery.DonViId.HasValue)
            {
                hisQuery = hisQuery.Where(x => x.DonViId == normalizedQuery.DonViId.Value);
            }

            if (!string.IsNullOrWhiteSpace(normalizedQuery.LopGiamSat))
            {
                hisQuery = hisQuery.Where(x => x.LopGiamSat == normalizedQuery.LopGiamSat);
            }

            return await hisQuery
                .OrderBy(x => x.DonViId)
                .ThenBy(x => x.LopGiamSat)
                .Select(ToHisDto())
                .ToListAsync(cancellationToken);
        }

        var liveQuery = ApplyReadScope(_dbContext.GiamSatNocs);
        if (normalizedQuery.DonViId.HasValue)
        {
            liveQuery = liveQuery.Where(x => x.DonViId == normalizedQuery.DonViId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery.LopGiamSat))
        {
            liveQuery = liveQuery.Where(x => x.LopGiamSat == normalizedQuery.LopGiamSat);
        }

        return await liveQuery
            .OrderBy(x => x.DonViId)
            .ThenBy(x => x.LopGiamSat)
            .Select(ToLiveDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<GiamSatNocDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.GiamSatNocs)
            .Where(x => x.Id == id)
            .Select(ToLiveDto())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<GiamSatNocDto> UpsertAsync(long? id, UpsertGiamSatNocRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedRequest = NormalizeRequest(request);
        await EnsureValidScopeAsync(normalizedRequest.DonViId, cancellationToken);

        GiamSatNocEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.GiamSatNocs).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("GSN_NOT_FOUND", "Khong tim thay ban ghi giam sat NOC.", 404);
        }
        else
        {
            entity = new GiamSatNocEntity();
            await _dbContext.GiamSatNocs.AddAsync(entity, cancellationToken);
        }

        ApplyValues(entity, normalizedRequest);
        await SyncCoNocAsync(entity, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<IReadOnlyCollection<GiamSatNocDto>> SaveMatrixAsync(SaveGiamSatNocMatrixRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        var items = (request.Items ?? [])
            .Select(NormalizeRequest)
            .ToList();

        ValidateMatrix(request.DonViId, items);

        var currentItems = await _dbContext.GiamSatNocs
            .IgnoreQueryFilters()
            .Where(x => x.DonViId == request.DonViId)
            .ToListAsync(cancellationToken);

        foreach (var item in currentItems)
        {
            item.DeletedAt = _dateTimeProvider.Now;
        }

        var coNoc = items.FirstOrDefault()?.CoNoc ?? false;

        foreach (var item in items)
        {
            var entity = currentItems.FirstOrDefault(x => string.Equals(x.LopGiamSat, item.LopGiamSat, StringComparison.OrdinalIgnoreCase));
            if (entity is null)
            {
                entity = new GiamSatNocEntity();
                await _dbContext.GiamSatNocs.AddAsync(entity, cancellationToken);
            }

            ApplyValues(entity, new UpsertGiamSatNocRequest
            {
                DonViId = item.DonViId,
                LopGiamSat = item.LopGiamSat,
                CoNoc = coNoc,
                ThucTrang = item.ThucTrang,
                NamThanhLap = item.NamThanhLap,
                SoNhanSu = item.SoNhanSu,
                GhiChu = item.GhiChu
            });
            entity.DeletedAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAllAsync(new GiamSatNocQuery { DonViId = request.DonViId }, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.GiamSatNocs).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("GSN_NOT_FOUND", "Khong tim thay ban ghi giam sat NOC.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<GiamSatNocEntity> ApplyReadScope(IQueryable<GiamSatNocEntity> query)
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
            throw new AppException("GSN_SCOPE_DENIED", "Khong co quyen thao tac du lieu giam sat NOC cua don vi khac.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Khong tim thay don vi.", 404);
        }
    }

    private async Task SyncCoNocAsync(GiamSatNocEntity entity, CancellationToken cancellationToken)
    {
        var siblings = await ApplyReadScope(_dbContext.GiamSatNocs)
            .Where(x => x.DonViId == entity.DonViId && x.Id != entity.Id)
            .ToListAsync(cancellationToken);

        foreach (var sibling in siblings)
        {
            sibling.CoNoc = entity.CoNoc;
        }
    }

    private static System.Linq.Expressions.Expression<Func<GiamSatNocEntity, GiamSatNocDto>> ToLiveDto()
        => x => new GiamSatNocDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            KyBaoCaoCode = null,
            LopGiamSat = x.LopGiamSat,
            CoNoc = x.CoNoc,
            ThucTrang = x.ThucTrang,
            NamThanhLap = x.NamThanhLap,
            SoNhanSu = x.SoNhanSu,
            GhiChu = x.GhiChu
        };

    private static System.Linq.Expressions.Expression<Func<GiamSatNocHisEntity, GiamSatNocDto>> ToHisDto()
        => x => new GiamSatNocDto
        {
            Id = x.SourceId,
            DonViId = x.DonViId,
            KyBaoCaoCode = x.KyBaoCaoCode,
            LopGiamSat = x.LopGiamSat,
            CoNoc = x.CoNoc,
            ThucTrang = x.ThucTrang,
            NamThanhLap = x.NamThanhLap,
            SoNhanSu = x.SoNhanSu,
            GhiChu = x.GhiChu
        };

    private static void ApplyValues(GiamSatNocEntity entity, UpsertGiamSatNocRequest request)
    {
        entity.DonViId = request.DonViId;
        entity.LopGiamSat = request.LopGiamSat;
        entity.CoNoc = request.CoNoc;
        entity.ThucTrang = request.ThucTrang;
        entity.NamThanhLap = request.NamThanhLap;
        entity.SoNhanSu = request.SoNhanSu;
        entity.GhiChu = request.GhiChu;
    }

    private static GiamSatNocQuery NormalizeQuery(GiamSatNocQuery query)
        => new()
        {
            DonViId = query.DonViId,
            LopGiamSat = NormalizeCode(query.LopGiamSat),
            KyBaoCaoCode = NormalizeCode(query.KyBaoCaoCode)
        };

    private static UpsertGiamSatNocRequest NormalizeRequest(UpsertGiamSatNocRequest request)
        => new()
        {
            DonViId = request.DonViId,
            LopGiamSat = NormalizeRequiredCode(request.LopGiamSat, nameof(request.LopGiamSat)),
            CoNoc = request.CoNoc,
            ThucTrang = NormalizeCode(request.ThucTrang),
            NamThanhLap = request.NamThanhLap,
            SoNhanSu = request.SoNhanSu,
            GhiChu = NormalizeText(request.GhiChu)
        };

    private static void ValidateMatrix(long donViId, List<UpsertGiamSatNocRequest> items)
    {
        var duplicateLayer = items
            .GroupBy(x => x.LopGiamSat, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;

        if (duplicateLayer is not null)
        {
            throw new AppException("GSN_DUPLICATE_MATRIX", "Du lieu giam sat NOC bi trung lop giam sat.", 400);
        }

        var invalidScope = items.Any(x => x.DonViId != donViId);
        if (invalidScope)
        {
            throw new AppException("GSN_INVALID_SCOPE", "Cac dong giam sat NOC phai cung don vi.", 400);
        }
    }

    private static string NormalizeRequiredCode(string? value, string fieldName)
        => NormalizeCode(value) ?? throw new AppException("GSN_INVALID_REQUEST", $"Truong {fieldName} la bat buoc.", 400);

    private static string? NormalizeCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}
