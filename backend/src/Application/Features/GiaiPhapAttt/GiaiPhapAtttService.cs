using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using GiaiPhapAtttEntity = ThucLuc.Domain.Entities.Business.GiaiPhapAttt;

namespace ThucLuc.Application.Features.GiaiPhapAttt;

public interface IGiaiPhapAtttService
{
    Task<IReadOnlyCollection<GiaiPhapAtttDto>> GetAllAsync(GiaiPhapAtttQuery query, CancellationToken cancellationToken = default);

    Task<GiaiPhapAtttDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<GiaiPhapAtttDto> UpsertAsync(long? id, UpsertGiaiPhapAtttRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GiaiPhapAtttDto>> SaveMatrixAsync(SaveGiaiPhapAtttMatrixRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class GiaiPhapAtttService : IGiaiPhapAtttService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GiaiPhapAtttService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<GiaiPhapAtttDto>> GetAllAsync(GiaiPhapAtttQuery query, CancellationToken cancellationToken = default)
    {
        var scopedQuery = ApplyReadScope(_dbContext.GiaiPhapAttts);
        if (query.DonViId.HasValue)
        {
            scopedQuery = scopedQuery.Where(x => x.DonViId == query.DonViId.Value);
        }

        return await scopedQuery.Select(x => new GiaiPhapAtttDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            TenGiaiPhap = x.TenGiaiPhap,
            MayTinhBcanetSl = x.MayTinhBcanetSl,
            MayTinhBcanetTs = x.MayTinhBcanetTs,
            MayTinhInternetSl = x.MayTinhInternetSl,
            MayTinhInternetTs = x.MayTinhInternetTs,
            MayTinhLocalSl = x.MayTinhLocalSl,
            MayTinhLocalTs = x.MayTinhLocalTs,
            MayChuBcanetSl = x.MayChuBcanetSl,
            MayChuBcanetTs = x.MayChuBcanetTs,
            MayChuInternetSl = x.MayChuInternetSl,
            MayChuInternetTs = x.MayChuInternetTs,
            MayChuLocalSl = x.MayChuLocalSl,
            MayChuLocalTs = x.MayChuLocalTs,
            GhiChu = x.GhiChu
        }).ToListAsync(cancellationToken);
    }

    public async Task<GiaiPhapAtttDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.GiaiPhapAttts).Where(x => x.Id == id).Select(x => new GiaiPhapAtttDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            TenGiaiPhap = x.TenGiaiPhap,
            MayTinhBcanetSl = x.MayTinhBcanetSl,
            MayTinhBcanetTs = x.MayTinhBcanetTs,
            MayTinhInternetSl = x.MayTinhInternetSl,
            MayTinhInternetTs = x.MayTinhInternetTs,
            MayTinhLocalSl = x.MayTinhLocalSl,
            MayTinhLocalTs = x.MayTinhLocalTs,
            MayChuBcanetSl = x.MayChuBcanetSl,
            MayChuBcanetTs = x.MayChuBcanetTs,
            MayChuInternetSl = x.MayChuInternetSl,
            MayChuInternetTs = x.MayChuInternetTs,
            MayChuLocalSl = x.MayChuLocalSl,
            MayChuLocalTs = x.MayChuLocalTs,
            GhiChu = x.GhiChu
        }).FirstOrDefaultAsync(cancellationToken);

    public async Task<GiaiPhapAtttDto> UpsertAsync(long? id, UpsertGiaiPhapAtttRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        GiaiPhapAtttEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.GiaiPhapAttts).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("GPATTT_NOT_FOUND", "Không tìm thấy bản ghi giải pháp ATTT.", 404);
        }
        else
        {
            entity = new GiaiPhapAtttEntity();
            await _dbContext.GiaiPhapAttts.AddAsync(entity, cancellationToken);
        }

        entity.DonViId = request.DonViId;
        entity.TenGiaiPhap = NormalizeRequired(request.TenGiaiPhap, "TenGiaiPhap").ToUpperInvariant();
        entity.MayTinhBcanetSl = request.MayTinhBcanetSl;
        entity.MayTinhBcanetTs = request.MayTinhBcanetTs;
        entity.MayTinhInternetSl = request.MayTinhInternetSl;
        entity.MayTinhInternetTs = request.MayTinhInternetTs;
        entity.MayTinhLocalSl = request.MayTinhLocalSl;
        entity.MayTinhLocalTs = request.MayTinhLocalTs;
        entity.MayChuBcanetSl = request.MayChuBcanetSl;
        entity.MayChuBcanetTs = request.MayChuBcanetTs;
        entity.MayChuInternetSl = request.MayChuInternetSl;
        entity.MayChuInternetTs = request.MayChuInternetTs;
        entity.MayChuLocalSl = request.MayChuLocalSl;
        entity.MayChuLocalTs = request.MayChuLocalTs;
        entity.GhiChu = request.GhiChu;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<IReadOnlyCollection<GiaiPhapAtttDto>> SaveMatrixAsync(SaveGiaiPhapAtttMatrixRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        var items = (request.Items ?? [])
            .Select(item => new UpsertGiaiPhapAtttRequest
            {
                DonViId = request.DonViId,
                TenGiaiPhap = NormalizeRequired(item.TenGiaiPhap, "TenGiaiPhap").ToUpperInvariant(),
                MayTinhBcanetSl = item.MayTinhBcanetSl,
                MayTinhBcanetTs = item.MayTinhBcanetTs,
                MayTinhInternetSl = item.MayTinhInternetSl,
                MayTinhInternetTs = item.MayTinhInternetTs,
                MayTinhLocalSl = item.MayTinhLocalSl,
                MayTinhLocalTs = item.MayTinhLocalTs,
                MayChuBcanetSl = item.MayChuBcanetSl,
                MayChuBcanetTs = item.MayChuBcanetTs,
                MayChuInternetSl = item.MayChuInternetSl,
                MayChuInternetTs = item.MayChuInternetTs,
                MayChuLocalSl = item.MayChuLocalSl,
                MayChuLocalTs = item.MayChuLocalTs,
                GhiChu = item.GhiChu
            })
            .ToList();

        var duplicate = items.GroupBy(x => x.TenGiaiPhap, StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
        {
            throw new AppException("GPATTT_DUPLICATE_MATRIX", "Dữ liệu giải pháp ATTT bị trùng theo tên giải pháp.", 400);
        }

        var currentItems = await ApplyReadScope(_dbContext.GiaiPhapAttts)
            .Where(x => x.DonViId == request.DonViId)
            .ToListAsync(cancellationToken);

        foreach (var current in currentItems)
        {
            current.DeletedAt = _dateTimeProvider.Now;
        }

        foreach (var item in items)
        {
            var entity = currentItems.FirstOrDefault(x => string.Equals(x.TenGiaiPhap, item.TenGiaiPhap, StringComparison.OrdinalIgnoreCase));
            if (entity is null)
            {
                entity = new GiaiPhapAtttEntity();
                await _dbContext.GiaiPhapAttts.AddAsync(entity, cancellationToken);
            }

            entity.DonViId = request.DonViId;
            entity.TenGiaiPhap = item.TenGiaiPhap;
            entity.MayTinhBcanetSl = item.MayTinhBcanetSl;
            entity.MayTinhBcanetTs = item.MayTinhBcanetTs;
            entity.MayTinhInternetSl = item.MayTinhInternetSl;
            entity.MayTinhInternetTs = item.MayTinhInternetTs;
            entity.MayTinhLocalSl = item.MayTinhLocalSl;
            entity.MayTinhLocalTs = item.MayTinhLocalTs;
            entity.MayChuBcanetSl = item.MayChuBcanetSl;
            entity.MayChuBcanetTs = item.MayChuBcanetTs;
            entity.MayChuInternetSl = item.MayChuInternetSl;
            entity.MayChuInternetTs = item.MayChuInternetTs;
            entity.MayChuLocalSl = item.MayChuLocalSl;
            entity.MayChuLocalTs = item.MayChuLocalTs;
            entity.GhiChu = item.GhiChu;
            entity.DeletedAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAllAsync(new GiaiPhapAtttQuery { DonViId = request.DonViId }, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.GiaiPhapAttts).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("GPATTT_NOT_FOUND", "Không tìm thấy bản ghi giải pháp ATTT.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<GiaiPhapAtttEntity> ApplyReadScope(IQueryable<GiaiPhapAtttEntity> query)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0)
        {
            query = query.Where(x => x.DonViId == currentUser.DonViId);
        }

        return query;
    }

    private async Task EnsureValidScopeAsync(long donViId, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0 && currentUser.DonViId != donViId)
        {
            throw new AppException("GPATTT_SCOPE_DENIED", "Không có quyền thao tác dữ liệu giải pháp ATTT của đơn vị khác.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        }
    }

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);

    private static string NormalizeRequired(string? value, string fieldName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new AppException("GPATTT_REQUIRED", $"{fieldName} là bắt buộc.", 400)
            : value.Trim();
}
