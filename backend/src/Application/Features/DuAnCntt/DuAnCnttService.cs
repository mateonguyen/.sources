using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using DuAnCnttEntity = ThucLuc.Domain.Entities.Business.DuAnCntt;
using DuAnCnttHisEntity = ThucLuc.Domain.Entities.Business.DuAnCnttHis;

namespace ThucLuc.Application.Features.DuAnCntt;

public interface IDuAnCnttService
{
    Task<IReadOnlyCollection<DuAnCnttDto>> GetAllAsync(GetDuAnCnttQuery query, CancellationToken cancellationToken = default);

    Task<DuAnCnttDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<DuAnCnttDto> UpsertAsync(long? id, UpsertDuAnCnttRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class DuAnCnttService : IDuAnCnttService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DuAnCnttService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<DuAnCnttDto>> GetAllAsync(GetDuAnCnttQuery query, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(query.KyBaoCaoCode))
        {
            var hisQuery = _dbContext.DuAnCnttHis.AsNoTracking()
                .Where(x => x.KyBaoCaoCode == query.KyBaoCaoCode)
                .Where(x => ApplyDonViScopePredicate(x.DonViId));

            if (query.DonViId.HasValue)
            {
                hisQuery = hisQuery.Where(x => x.DonViId == query.DonViId.Value);
            }

            return await hisQuery.Select(MapHisToDto()).ToListAsync(cancellationToken);
        }

        var liveQuery = ApplyReadScope(_dbContext.DuAnCntts);
        if (query.DonViId.HasValue)
        {
            liveQuery = liveQuery.Where(x => x.DonViId == query.DonViId.Value);
        }

        return await liveQuery.Select(MapLiveToDto()).ToListAsync(cancellationToken);
    }

    public async Task<DuAnCnttDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.DuAnCntts)
            .Where(x => x.Id == id)
            .Select(MapLiveToDto())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<DuAnCnttDto> UpsertAsync(long? id, UpsertDuAnCnttRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        DuAnCnttEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.DuAnCntts).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("DU_AN_NOT_FOUND", "Không tìm thấy bản ghi dự án CNTT.", 404);
        }
        else
        {
            entity = new DuAnCnttEntity();
            await _dbContext.DuAnCntts.AddAsync(entity, cancellationToken);
        }

        entity.DonViId = request.DonViId;
        entity.TenDuAn = NormalizeRequired(request.TenDuAn, nameof(request.TenDuAn));
        entity.DonViChuTri = NormalizeNullable(request.DonViChuTri);
        entity.NamTrienKhai = request.NamTrienKhai;
        entity.NamDuaVaoSuDung = request.NamDuaVaoSuDung;
        entity.TongKinhPhi = request.TongKinhPhi;
        entity.NguonVon = NormalizeCode(request.NguonVon);
        entity.GhiChu = request.GhiChu;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.DuAnCntts).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("DU_AN_NOT_FOUND", "Không tìm thấy bản ghi dự án CNTT.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<DuAnCnttEntity> ApplyReadScope(IQueryable<DuAnCnttEntity> query)
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
            throw new AppException("DU_AN_SCOPE_DENIED", "Không có quyền thao tác dữ liệu dự án CNTT của đơn vị khác.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        }
    }


    private static string NormalizeRequired(string? value, string fieldName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new AppException("DU_AN_REQUIRED", $"{fieldName} là bắt buộc.", 400)
            : value.Trim();

    private bool ApplyDonViScopePredicate(long donViId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        return HasCrossDonViPermission(currentUser) || currentUser.DonViId <= 0 || currentUser.DonViId == donViId;
    }

    private static System.Linq.Expressions.Expression<Func<DuAnCnttEntity, DuAnCnttDto>> MapLiveToDto()
        => x => new DuAnCnttDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            KyBaoCaoCode = null,
            TenDuAn = x.TenDuAn,
            DonViChuTri = x.DonViChuTri,
            NamTrienKhai = x.NamTrienKhai,
            NamDuaVaoSuDung = x.NamDuaVaoSuDung,
            TongKinhPhi = x.TongKinhPhi,
            NguonVon = x.NguonVon,
            GhiChu = x.GhiChu
        };

    private static System.Linq.Expressions.Expression<Func<DuAnCnttHisEntity, DuAnCnttDto>> MapHisToDto()
        => x => new DuAnCnttDto
        {
            Id = x.SourceId,
            DonViId = x.DonViId,
            KyBaoCaoCode = x.KyBaoCaoCode,
            TenDuAn = x.TenDuAn,
            DonViChuTri = x.DonViChuTri,
            NamTrienKhai = x.NamTrienKhai,
            NamDuaVaoSuDung = x.NamDuaVaoSuDung,
            TongKinhPhi = x.TongKinhPhi,
            NguonVon = x.NguonVon,
            GhiChu = x.GhiChu
        };

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}