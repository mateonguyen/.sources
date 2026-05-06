using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using AtttHtttDauTuEntity = ThucLuc.Domain.Entities.Business.AtttHtttDauTu;
using AtttHtttDauTuHisEntity = ThucLuc.Domain.Entities.Business.AtttHtttDauTuHis;

namespace ThucLuc.Application.Features.AtttHtttDauTu;

public interface IAtttHtttDauTuService
{
    Task<IReadOnlyCollection<AtttHtttDauTuDto>> GetAllAsync(AtttHtttDauTuQuery query, CancellationToken cancellationToken = default);

    Task<AtttHtttDauTuDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<AtttHtttDauTuDto> UpsertAsync(long? id, UpsertAtttHtttDauTuRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class AtttHtttDauTuService : IAtttHtttDauTuService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AtttHtttDauTuService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<AtttHtttDauTuDto>> GetAllAsync(AtttHtttDauTuQuery query, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(query.KyBaoCaoCode))
        {
            var hisQuery = _dbContext.AtttHtttDauTuHis.AsNoTracking()
                .Where(x => x.KyBaoCaoCode == query.KyBaoCaoCode)
                .Where(x => ApplyDonViScopePredicate(x.DonViId));

            if (query.DonViId.HasValue)
            {
                hisQuery = hisQuery.Where(x => x.DonViId == query.DonViId.Value);
            }

            return await hisQuery.Select(MapHisToDto()).ToListAsync(cancellationToken);
        }

        var liveQuery = ApplyReadScope(_dbContext.AtttHtttDauTus);
        if (query.DonViId.HasValue)
        {
            liveQuery = liveQuery.Where(x => x.DonViId == query.DonViId.Value);
        }

        return await liveQuery.Select(MapLiveToDto()).ToListAsync(cancellationToken);
    }

    public async Task<AtttHtttDauTuDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.AtttHtttDauTus)
            .Where(x => x.Id == id)
            .Select(MapLiveToDto())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<AtttHtttDauTuDto> UpsertAsync(long? id, UpsertAtttHtttDauTuRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        AtttHtttDauTuEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.AtttHtttDauTus).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("ATTTDT_NOT_FOUND", "Không tìm thấy bản ghi ATTT HTTT đầu tư.", 404);
        }
        else
        {
            entity = new AtttHtttDauTuEntity();
            await _dbContext.AtttHtttDauTus.AddAsync(entity, cancellationToken);
        }

        entity.DonViId = request.DonViId;
        entity.HtttId = request.HtttId;
        entity.ChuQuan = NormalizeOptional(request.ChuQuan);
        entity.DonViVanHanh = NormalizeOptional(request.DonViVanHanh);
        entity.CapDoDeXuat = NormalizeOptional(request.CapDoDeXuat);
        entity.NgayPheDuyetHsdxcd = request.NgayPheDuyetHsdxcd;
        entity.QuyetDinhPheDuyet = NormalizeOptional(request.QuyetDinhPheDuyet);
        entity.DaLongGhepThuyetMinh = request.DaLongGhepThuyetMinh;
        entity.GhiChu = request.GhiChu;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.AtttHtttDauTus).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("ATTTDT_NOT_FOUND", "Không tìm thấy bản ghi ATTT HTTT đầu tư.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<AtttHtttDauTuEntity> ApplyReadScope(IQueryable<AtttHtttDauTuEntity> query)
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
            throw new AppException("ATTTDT_SCOPE_DENIED", "Không có quyền thao tác dữ liệu ATTT HTTT đầu tư của đơn vị khác.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        }
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private bool ApplyDonViScopePredicate(long donViId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        return HasCrossDonViPermission(currentUser) || currentUser.DonViId <= 0 || currentUser.DonViId == donViId;
    }

    private static System.Linq.Expressions.Expression<Func<AtttHtttDauTuEntity, AtttHtttDauTuDto>> MapLiveToDto()
        => x => new AtttHtttDauTuDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            KyBaoCaoCode = null,
            HtttId = x.HtttId,
            ChuQuan = x.ChuQuan,
            DonViVanHanh = x.DonViVanHanh,
            CapDoDeXuat = x.CapDoDeXuat,
            NgayPheDuyetHsdxcd = x.NgayPheDuyetHsdxcd,
            QuyetDinhPheDuyet = x.QuyetDinhPheDuyet,
            DaLongGhepThuyetMinh = x.DaLongGhepThuyetMinh,
            GhiChu = x.GhiChu
        };

    private static System.Linq.Expressions.Expression<Func<AtttHtttDauTuHisEntity, AtttHtttDauTuDto>> MapHisToDto()
        => x => new AtttHtttDauTuDto
        {
            Id = x.SourceId,
            DonViId = x.DonViId,
            KyBaoCaoCode = x.KyBaoCaoCode,
            HtttId = x.HtttId,
            ChuQuan = x.ChuQuan,
            DonViVanHanh = x.DonViVanHanh,
            CapDoDeXuat = x.CapDoDeXuat,
            NgayPheDuyetHsdxcd = x.NgayPheDuyetHsdxcd,
            QuyetDinhPheDuyet = x.QuyetDinhPheDuyet,
            DaLongGhepThuyetMinh = x.DaLongGhepThuyetMinh,
            GhiChu = x.GhiChu
        };

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}
