using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using CameraThucTrangEntity = ThucLuc.Domain.Entities.Business.CameraThucTrang;
using CameraThucTrangHisEntity = ThucLuc.Domain.Entities.Business.CameraThucTrangHis;

namespace ThucLuc.Application.Features.CameraThucTrang;

public interface ICameraThucTrangService
{
    Task<IReadOnlyCollection<CameraThucTrangDto>> GetAllAsync(GetCameraThucTrangQuery query, CancellationToken cancellationToken = default);

    Task<CameraThucTrangDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<CameraThucTrangDto> UpsertAsync(long? id, UpsertCameraThucTrangRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class CameraThucTrangService : ICameraThucTrangService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CameraThucTrangService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<CameraThucTrangDto>> GetAllAsync(GetCameraThucTrangQuery query, CancellationToken cancellationToken = default)
    {
        var normalizedNhomCamera = NormalizeCode(query.NhomCamera);
        if (!string.IsNullOrWhiteSpace(query.KyBaoCaoCode))
        {
            var hisQuery = _dbContext.CameraThucTrangHis.AsNoTracking()
                .Where(x => x.KyBaoCaoCode == query.KyBaoCaoCode)
                .Where(x => ApplyDonViScopePredicate(x.DonViId));

            if (query.DonViId.HasValue)
            {
                hisQuery = hisQuery.Where(x => x.DonViId == query.DonViId.Value);
            }

            if (!string.IsNullOrWhiteSpace(normalizedNhomCamera))
            {
                hisQuery = hisQuery.Where(x => x.NhomCamera == normalizedNhomCamera);
            }

            return await hisQuery.Select(MapHisToDto()).ToListAsync(cancellationToken);
        }

        var liveQuery = ApplyReadScope(_dbContext.CameraThucTrangs);
        if (query.DonViId.HasValue)
        {
            liveQuery = liveQuery.Where(x => x.DonViId == query.DonViId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedNhomCamera))
        {
            liveQuery = liveQuery.Where(x => x.NhomCamera == normalizedNhomCamera);
        }

        return await liveQuery.Select(MapLiveToDto()).ToListAsync(cancellationToken);
    }

    public async Task<CameraThucTrangDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.CameraThucTrangs)
            .Where(x => x.Id == id)
            .Select(MapLiveToDto())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<CameraThucTrangDto> UpsertAsync(long? id, UpsertCameraThucTrangRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        CameraThucTrangEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.CameraThucTrangs).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("CAMERA_TT_NOT_FOUND", "Không tìm thấy bản ghi camera thực trạng.", 404);
        }
        else
        {
            entity = new CameraThucTrangEntity();
            await _dbContext.CameraThucTrangs.AddAsync(entity, cancellationToken);
        }

        entity.DonViId = request.DonViId;
        entity.NhomCamera = NormalizeCode(request.NhomCamera);
        entity.TenHeThong = NormalizeRequired(request.TenHeThong, nameof(request.TenHeThong));
        entity.CauHinhIp = request.CauHinhIp;
        entity.CauHinhAnalog = request.CauHinhAnalog;
        entity.ThucTrangIp = request.ThucTrangIp;
        entity.ThucTrangAnalog = request.ThucTrangAnalog;
        entity.ChuDauTu = request.ChuDauTu;
        entity.NamDauTu = request.NamDauTu;
        entity.DuongTruyen = NormalizeCode(request.DuongTruyen);
        entity.PhanMem = request.PhanMem;
        entity.LuuTru = request.LuuTru;
        entity.GhiChu = request.GhiChu;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.CameraThucTrangs).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("CAMERA_TT_NOT_FOUND", "Không tìm thấy bản ghi camera thực trạng.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<CameraThucTrangEntity> ApplyReadScope(IQueryable<CameraThucTrangEntity> query)
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
            throw new AppException("CAMERA_TT_SCOPE_DENIED", "Không có quyền thao tác dữ liệu camera thực trạng của đơn vị khác.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        }
    }

    private static string NormalizeRequired(string? value, string fieldName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new AppException("CAMERA_TT_REQUIRED", $"{fieldName} là bắt buộc.", 400)
            : value.Trim();

    private static string? NormalizeCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private bool ApplyDonViScopePredicate(long donViId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        return HasCrossDonViPermission(currentUser) || currentUser.DonViId <= 0 || currentUser.DonViId == donViId;
    }

    private static System.Linq.Expressions.Expression<Func<CameraThucTrangEntity, CameraThucTrangDto>> MapLiveToDto()
        => x => new CameraThucTrangDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            KyBaoCaoCode = null,
            NhomCamera = x.NhomCamera,
            TenHeThong = x.TenHeThong,
            CauHinhIp = x.CauHinhIp,
            CauHinhAnalog = x.CauHinhAnalog,
            ThucTrangIp = x.ThucTrangIp,
            ThucTrangAnalog = x.ThucTrangAnalog,
            ChuDauTu = x.ChuDauTu,
            NamDauTu = x.NamDauTu,
            DuongTruyen = x.DuongTruyen,
            PhanMem = x.PhanMem,
            LuuTru = x.LuuTru,
            GhiChu = x.GhiChu
        };

    private static System.Linq.Expressions.Expression<Func<CameraThucTrangHisEntity, CameraThucTrangDto>> MapHisToDto()
        => x => new CameraThucTrangDto
        {
            Id = x.SourceId,
            DonViId = x.DonViId,
            KyBaoCaoCode = x.KyBaoCaoCode,
            NhomCamera = x.NhomCamera,
            TenHeThong = x.TenHeThong,
            CauHinhIp = x.CauHinhIp,
            CauHinhAnalog = x.CauHinhAnalog,
            ThucTrangIp = x.ThucTrangIp,
            ThucTrangAnalog = x.ThucTrangAnalog,
            ChuDauTu = x.ChuDauTu,
            NamDauTu = x.NamDauTu,
            DuongTruyen = x.DuongTruyen,
            PhanMem = x.PhanMem,
            LuuTru = x.LuuTru,
            GhiChu = x.GhiChu
        };

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}
