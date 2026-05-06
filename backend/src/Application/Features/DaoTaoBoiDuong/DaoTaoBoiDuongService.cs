using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using DaoTaoBoiDuongEntity = ThucLuc.Domain.Entities.Business.DaoTaoBoiDuong;
using DaoTaoBoiDuongHisEntity = ThucLuc.Domain.Entities.Business.DaoTaoBoiDuongHis;

namespace ThucLuc.Application.Features.DaoTaoBoiDuong;

public interface IDaoTaoBoiDuongService
{
    Task<IReadOnlyCollection<DaoTaoBoiDuongDto>> GetAllAsync(DaoTaoBoiDuongQuery query, CancellationToken cancellationToken = default);

    Task<DaoTaoBoiDuongDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<DaoTaoBoiDuongDto> UpsertAsync(long? id, UpsertDaoTaoBoiDuongRequest request, CancellationToken cancellationToken = default);

    Task<FinalizeDaoTaoBoiDuongResult> FinalizeAsync(FinalizeDaoTaoBoiDuongRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class DaoTaoBoiDuongService : IDaoTaoBoiDuongService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<UpsertDaoTaoBoiDuongRequest> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DaoTaoBoiDuongService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IValidator<UpsertDaoTaoBoiDuongRequest> validator,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<DaoTaoBoiDuongDto>> GetAllAsync(DaoTaoBoiDuongQuery query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeQuery(query);

        if (!string.IsNullOrWhiteSpace(normalizedQuery.KyBaoCaoCode))
        {
            return await _dbContext.DaoTaoBoiDuongHis
                .AsNoTracking()
                .Where(x => x.KyBaoCaoCode == normalizedQuery.KyBaoCaoCode)
                .Where(x => ApplyDonViScopePredicate(x.DonViId))
                .Select(MapHisToDto())
                .ToListAsync(cancellationToken);
        }

        return await ApplyReadScope(_dbContext.DaoTaoBoiDuongs)
            .AsNoTracking()
            .Select(MapLiveToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<DaoTaoBoiDuongDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.DaoTaoBoiDuongs)
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(MapLiveToDto())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<DaoTaoBoiDuongDto> UpsertAsync(long? id, UpsertDaoTaoBoiDuongRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var currentUser = _currentUserService.GetCurrentUser();
        var donViId = currentUser.DonViId;

        await EnsureValidScopeAsync(donViId, cancellationToken);

        DaoTaoBoiDuongEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.DaoTaoBoiDuongs).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("DTBD_NOT_FOUND", "Không tìm thấy bản ghi đào tạo bồi dưỡng.", 404);
        }
        else
        {
            entity = new DaoTaoBoiDuongEntity();
            await _dbContext.DaoTaoBoiDuongs.AddAsync(entity, cancellationToken);
        }

        entity.DonViId = donViId;
        entity.TenKhoaHoc = request.TenKhoaHoc.Trim();
        entity.DonViToChuc = request.DonViToChuc.Trim();
        entity.HinhThuc = NormalizeOptional(request.HinhThuc);
        entity.SoLuongHv = request.SoLuongHv;
        entity.ThoiGianTu = request.ThoiGianTu;
        entity.ThoiGianDen = request.ThoiGianDen;
        entity.GhiChu = NormalizeOptional(request.GhiChu);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.DaoTaoBoiDuongs).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("DTBD_NOT_FOUND", "Không tìm thấy bản ghi đào tạo bồi dưỡng.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<FinalizeDaoTaoBoiDuongResult> FinalizeAsync(FinalizeDaoTaoBoiDuongRequest request, CancellationToken cancellationToken = default)
    {
        var kyBaoCaoCode = NormalizeRequiredCode(request.KyBaoCaoCode);
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        var kyExists = await _dbContext.KyBaoCaos
            .AsNoTracking()
            .AnyAsync(x => x.KyCode == kyBaoCaoCode, cancellationToken);
        if (!kyExists)
        {
            throw new AppException("KY_BAO_CAO_NOT_FOUND", "Không tìm thấy kỳ báo cáo.", 404);
        }

        var now = _dateTimeProvider.Now;
        var batchIdText = Guid.NewGuid().ToString("N");
        var batchIdNumeric = Math.Abs(new Guid(batchIdText).GetHashCode());

        var liveRecords = await ApplyReadScope(_dbContext.DaoTaoBoiDuongs)
            .Where(x => x.DonViId == request.DonViId)
            .ToListAsync(cancellationToken);

        var hisRows = liveRecords.Select(x => new DaoTaoBoiDuongHisEntity
        {
            SourceId = x.Id,
            DonViId = x.DonViId,
            TenKhoaHoc = x.TenKhoaHoc,
            DonViToChuc = x.DonViToChuc,
            HinhThuc = x.HinhThuc,
            SoLuongHv = x.SoLuongHv,
            ThoiGianTu = x.ThoiGianTu,
            ThoiGianDen = x.ThoiGianDen,
            GhiChu = x.GhiChu,
            KyBaoCaoCode = kyBaoCaoCode,
            SnapshotBatchId = batchIdNumeric,
            SnapshotCreatedAt = now,
        }).ToList();

        if (hisRows.Count > 0)
        {
            await _dbContext.DaoTaoBoiDuongHis.AddRangeAsync(hisRows, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new FinalizeDaoTaoBoiDuongResult
        {
            SnapshotBatchId = batchIdText,
            DonViId = request.DonViId,
            KyBaoCaoCode = kyBaoCaoCode,
            AffectedRows = hisRows.Count,
            FinishedAt = now
        };
    }

    private IQueryable<DaoTaoBoiDuongEntity> ApplyReadScope(IQueryable<DaoTaoBoiDuongEntity> query)
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
            throw new AppException("DTBD_SCOPE_DENIED", "Không có quyền thao tác dữ liệu đào tạo bồi dưỡng của đơn vị khác.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        }
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRequiredCode(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new AppException("KY_BAO_CAO_CODE_REQUIRED", "KyBaoCaoCode là bắt buộc.", 400);
        }

        return normalized.Trim().ToUpperInvariant();
    }

    private static DaoTaoBoiDuongQuery NormalizeQuery(DaoTaoBoiDuongQuery? query)
        => new()
        {
            KyBaoCaoCode = NormalizeOptional(query?.KyBaoCaoCode)
        };

    private bool ApplyDonViScopePredicate(long donViId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        return HasCrossDonViPermission(currentUser) || currentUser.DonViId <= 0 || currentUser.DonViId == donViId;
    }

    private static System.Linq.Expressions.Expression<Func<DaoTaoBoiDuongEntity, DaoTaoBoiDuongDto>> MapLiveToDto()
        => x => new DaoTaoBoiDuongDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            KyBaoCaoCode = null,
            TenKhoaHoc = x.TenKhoaHoc,
            DonViToChuc = x.DonViToChuc,
            HinhThuc = x.HinhThuc,
            SoLuongHv = x.SoLuongHv,
            ThoiGianTu = x.ThoiGianTu,
            ThoiGianDen = x.ThoiGianDen,
            GhiChu = x.GhiChu
        };

    private static System.Linq.Expressions.Expression<Func<DaoTaoBoiDuongHisEntity, DaoTaoBoiDuongDto>> MapHisToDto()
        => x => new DaoTaoBoiDuongDto
        {
            Id = x.SourceId,
            DonViId = x.DonViId,
            KyBaoCaoCode = x.KyBaoCaoCode,
            TenKhoaHoc = x.TenKhoaHoc,
            DonViToChuc = x.DonViToChuc,
            HinhThuc = x.HinhThuc,
            SoLuongHv = x.SoLuongHv,
            ThoiGianTu = x.ThoiGianTu,
            ThoiGianDen = x.ThoiGianDen,
            GhiChu = x.GhiChu
        };

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}

public sealed class UpsertDaoTaoBoiDuongRequestValidator : AbstractValidator<UpsertDaoTaoBoiDuongRequest>
{
    public UpsertDaoTaoBoiDuongRequestValidator()
    {
        RuleFor(x => x.TenKhoaHoc).NotEmpty().MaximumLength(300);
        RuleFor(x => x.DonViToChuc).NotEmpty().MaximumLength(200);
        RuleFor(x => x.HinhThuc).MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.HinhThuc));
        RuleFor(x => x.SoLuongHv).GreaterThanOrEqualTo(0)
            .When(x => x.SoLuongHv.HasValue);
        RuleFor(x => x.ThoiGianDen)
            .GreaterThanOrEqualTo(x => x.ThoiGianTu)
            .When(x => x.ThoiGianTu.HasValue && x.ThoiGianDen.HasValue);
        RuleFor(x => x.GhiChu).MaximumLength(500);
    }
}
