using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Entities.Business;
using HtttTieuChuanEntity = ThucLuc.Domain.Entities.Business.HtttTieuChuan;

namespace ThucLuc.Application.Features.HtttTieuChuan;

public sealed class HtttTieuChuanDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public string TenHeThong { get; set; } = string.Empty;
    public string? Dvt { get; set; }
    public int SoH05 { get; set; }
    public int SoTinh { get; set; }
    public int SoXa { get; set; }
    public int SoDvTrucThuocBo { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class UpsertHtttTieuChuanRequest
{
    public long DonViId { get; set; }
    public string TenHeThong { get; set; } = string.Empty;
    public string? Dvt { get; set; }
    public int SoH05 { get; set; }
    public int SoTinh { get; set; }
    public int SoXa { get; set; }
    public int SoDvTrucThuocBo { get; set; }
    public string? GhiChu { get; set; }
}

public interface IHtttTieuChuanService
{
    Task<IReadOnlyCollection<HtttTieuChuanDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<HtttTieuChuanDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<HtttTieuChuanDto> UpsertAsync(long? id, UpsertHtttTieuChuanRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class HtttTieuChuanService : IHtttTieuChuanService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public HtttTieuChuanService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<HtttTieuChuanDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.HtttTieuChuans)
            .OrderBy(x => x.TenHeThong)
            .Select(MapToDto())
            .ToListAsync(cancellationToken);

    public async Task<HtttTieuChuanDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.HtttTieuChuans)
            .Where(x => x.Id == id)
            .Select(MapToDto())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<HtttTieuChuanDto> UpsertAsync(long? id, UpsertHtttTieuChuanRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        HtttTieuChuanEntity entity;
        bool isNew = !id.HasValue;

        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.HtttTieuChuans)
                .FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("HTTTTC_NOT_FOUND", "Không tìm thấy bản ghi HTTT tiêu chuẩn.", 404);
        }
        else
        {
            entity = new HtttTieuChuanEntity { ValidFrom = _dateTimeProvider.Now, VersionNo = 1 };
            await _dbContext.HtttTieuChuans.AddAsync(entity, cancellationToken);
        }

        if (!isNew)
        {
            bool shouldVersion =
                request.SoH05 != entity.SoH05 ||
                request.SoTinh != entity.SoTinh ||
                request.SoXa != entity.SoXa ||
                request.SoDvTrucThuocBo != entity.SoDvTrucThuocBo;

            if (shouldVersion)
            {
                var now = _dateTimeProvider.Now;
                await _dbContext.HtttTieuChuanHis.AddAsync(new HtttTieuChuanHis
                {
                    SourceId = entity.Id,
                    DonViId = entity.DonViId,
                    TenHeThong = entity.TenHeThong,
                    Dvt = entity.Dvt,
                    SoH05 = entity.SoH05,
                    SoTinh = entity.SoTinh,
                    SoXa = entity.SoXa,
                    SoDvTrucThuocBo = entity.SoDvTrucThuocBo,
                    GhiChu = entity.GhiChu,
                    ValidFrom = entity.ValidFrom,
                    ValidTo = now,
                    VersionNo = entity.VersionNo,
                }, cancellationToken);
                entity.ValidFrom = now;
                entity.VersionNo++;
            }
        }

        entity.DonViId = request.DonViId;
        entity.TenHeThong = NormalizeRequiredText(request.TenHeThong);
        entity.Dvt = NormalizeText(request.Dvt);
        entity.SoH05 = request.SoH05;
        entity.SoTinh = request.SoTinh;
        entity.SoXa = request.SoXa;
        entity.SoDvTrucThuocBo = request.SoDvTrucThuocBo;
        entity.GhiChu = NormalizeText(request.GhiChu);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.HtttTieuChuans)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("HTTTTC_NOT_FOUND", "Không tìm thấy bản ghi HTTT tiêu chuẩn.", 404);

        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<HtttTieuChuanEntity> ApplyReadScope(IQueryable<HtttTieuChuanEntity> query)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0)
            query = query.Where(x => x.DonViId == currentUser.DonViId);

        return query;
    }

    private async Task EnsureValidScopeAsync(long donViId, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0 && currentUser.DonViId != donViId)
            throw new AppException("HTTTTC_SCOPE_DENIED", "Không có quyền thao tác dữ liệu của đơn vị khác.", 403);

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
            throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
    }

    private static System.Linq.Expressions.Expression<Func<HtttTieuChuanEntity, HtttTieuChuanDto>> MapToDto()
        => x => new HtttTieuChuanDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            TenHeThong = x.TenHeThong,
            Dvt = x.Dvt,
            SoH05 = x.SoH05,
            SoTinh = x.SoTinh,
            SoXa = x.SoXa,
            SoDvTrucThuocBo = x.SoDvTrucThuocBo,
            GhiChu = x.GhiChu
        };

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);

    private static string NormalizeRequiredText(string value)
    {
        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new AppException("HTTTTC_TEN_REQUIRED", "Tên hệ thống không được để trống.", 400)
            : normalized;
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
