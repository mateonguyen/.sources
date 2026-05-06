using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Entities.Reporting;
using DaoTaoHocVienEntity = ThucLuc.Domain.Entities.Business.DaoTaoHocVien;
using DaoTaoHocVienHisEntity = ThucLuc.Domain.Entities.Business.DaoTaoHocVienHis;

namespace ThucLuc.Application.Features.DaoTaoHocVien;

public interface IDaoTaoHocVienService
{
    Task<IReadOnlyCollection<DaoTaoHocVienDto>> GetAllAsync(DaoTaoHocVienQuery query, CancellationToken cancellationToken = default);

    Task<DaoTaoHocVienDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<DaoTaoHocVienDto> UpsertAsync(long? id, UpsertDaoTaoHocVienRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DaoTaoHocVienDto>> SaveMatrixAsync(SaveDaoTaoHocVienMatrixRequest request, CancellationToken cancellationToken = default);

    Task<FinalizeDaoTaoHocVienResult> FinalizeAsync(FinalizeDaoTaoHocVienRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class DaoTaoHocVienService : IDaoTaoHocVienService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<UpsertDaoTaoHocVienRequest> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DaoTaoHocVienService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IValidator<UpsertDaoTaoHocVienRequest> validator,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<DaoTaoHocVienDto>> GetAllAsync(DaoTaoHocVienQuery query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeQuery(query);

        if (!string.IsNullOrWhiteSpace(normalizedQuery.KyBaoCaoCode))
        {
            var hisQuery = _dbContext.DaoTaoHocVienHis
                .AsNoTracking()
                .Where(x => x.KyBaoCaoCode == normalizedQuery.KyBaoCaoCode)
                .Where(x => ApplyDonViScopePredicate(x.DonViId));

            if (normalizedQuery.DonViId.HasValue)
            {
                hisQuery = hisQuery.Where(x => x.DonViId == normalizedQuery.DonViId.Value);
            }

            if (normalizedQuery.Nam.HasValue)
            {
                hisQuery = hisQuery.Where(x => x.Nam == normalizedQuery.Nam.Value);
            }

            var hisEntities = await hisQuery
                .OrderByDescending(x => x.SnapshotCreatedAt)
                .ThenByDescending(x => x.Id)
                .ToListAsync(cancellationToken);

            return hisEntities
                .GroupBy(x => x.NoiDungDaoTao, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(x => x.NoiDungDaoTao)
                .Select(MapHisToDto)
                .ToList();
        }

        var liveQuery = ApplyReadScope(_dbContext.DaoTaoHocViens);
        if (normalizedQuery.DonViId.HasValue)
        {
            liveQuery = liveQuery.Where(x => x.DonViId == normalizedQuery.DonViId.Value);
        }

        if (normalizedQuery.Nam.HasValue)
        {
            liveQuery = liveQuery.Where(x => x.Nam == normalizedQuery.Nam.Value);
        }

        var liveEntities = await liveQuery
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return liveEntities
            .GroupBy(x => x.NoiDungDaoTao, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(x => x.NoiDungDaoTao)
            .Select(MapLiveToDto)
            .ToList();
    }

    public async Task<DaoTaoHocVienDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.DaoTaoHocViens)
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ToLiveDto())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<DaoTaoHocVienDto> UpsertAsync(long? id, UpsertDaoTaoHocVienRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedRequest = NormalizeRequest(request);
        await _validator.ValidateAndThrowAsync(normalizedRequest, cancellationToken);
        await EnsureValidScopeAsync(normalizedRequest.DonViId, cancellationToken);

        DaoTaoHocVienEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.DaoTaoHocViens)
                .FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("DTHV_NOT_FOUND", "Khong tim thay ban ghi dao tao hoc vien.", 404);
        }
        else
        {
            entity = new DaoTaoHocVienEntity();
            await _dbContext.DaoTaoHocViens.AddAsync(entity, cancellationToken);
        }

        ApplyValues(entity, normalizedRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<IReadOnlyCollection<DaoTaoHocVienDto>> SaveMatrixAsync(SaveDaoTaoHocVienMatrixRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        var items = (request.Items ?? [])
            .Select(item => NormalizeRequest(item, request.DonViId))
            .ToList();

        ValidateMatrix(request.DonViId, items);

        var allItems = await _dbContext.DaoTaoHocViens
            .IgnoreQueryFilters()
            .Where(x => x.DonViId == request.DonViId)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var item in allItems.Where(x => x.DeletedAt == null))
        {
            item.DeletedAt = _dateTimeProvider.Now;
        }

        foreach (var item in items)
        {
            var entity = allItems.FirstOrDefault(x => string.Equals(x.NoiDungDaoTao, item.NoiDungDaoTao, StringComparison.OrdinalIgnoreCase));
            if (entity is null)
            {
                entity = new DaoTaoHocVienEntity();
                allItems.Add(entity);
                await _dbContext.DaoTaoHocViens.AddAsync(entity, cancellationToken);
            }

            ApplyValues(entity, item);
            entity.DeletedAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAllAsync(
            new DaoTaoHocVienQuery
            {
                DonViId = request.DonViId,
            },
            cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.DaoTaoHocViens)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("DTHV_NOT_FOUND", "Khong tim thay ban ghi dao tao hoc vien.", 404);

        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<FinalizeDaoTaoHocVienResult> FinalizeAsync(FinalizeDaoTaoHocVienRequest request, CancellationToken cancellationToken = default)
    {
        var kyBaoCaoCode = NormalizeRequiredCode(request.KyBaoCaoCode);
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        var kyBaoCaoId = await _dbContext.KyBaoCaos
            .AsNoTracking()
            .Where(x => x.KyCode == kyBaoCaoCode)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!kyBaoCaoId.HasValue)
        {
            throw new AppException("KY_BAO_CAO_NOT_FOUND", "Khong tim thay ky bao cao.", 404);
        }

        var now = _dateTimeProvider.Now;
        var currentUser = _currentUserService.GetCurrentUser();
        var batch = new SnapshotBatch
        {
            KyBaoCaoId = kyBaoCaoId.Value,
            DonViId = request.DonViId,
            Status = "RUNNING",
            StartedAt = now,
            CreatedBy = currentUser.UserId,
            UpdatedBy = currentUser.UserId,
        };

        await _dbContext.SnapshotBatches.AddAsync(batch, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var liveItems = await ApplyReadScope(_dbContext.DaoTaoHocViens)
                .Where(x => x.DonViId == request.DonViId)
                .ToListAsync(cancellationToken);

            var hisRows = liveItems.Select(x => new DaoTaoHocVienHisEntity
            {
                SourceId = x.Id,
                DonViId = x.DonViId,
                Nam = x.Nam,
                NoiDungDaoTao = x.NoiDungDaoTao,
                SoTienSi = x.SoTienSi,
                SoThacSi = x.SoThacSi,
                SoDaiHoc = x.SoDaiHoc,
                SoCaoDang = x.SoCaoDang,
                SoTrungCap = x.SoTrungCap,
                GhiChu = x.GhiChu,
                KyBaoCaoCode = kyBaoCaoCode,
                SnapshotBatchId = batch.Id,
                SnapshotCreatedAt = now,
            }).ToList();

            if (hisRows.Count > 0)
            {
                await _dbContext.DaoTaoHocVienHis.AddRangeAsync(hisRows, cancellationToken);
            }

            batch.Status = "SUCCEEDED";
            batch.FinishedAt = _dateTimeProvider.Now;
            batch.UpdatedBy = currentUser.UserId;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            batch.Status = "FAILED";
            batch.FinishedAt = _dateTimeProvider.Now;
            batch.ErrorMessage = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
            batch.UpdatedBy = currentUser.UserId;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        return new FinalizeDaoTaoHocVienResult
        {
            BatchId = batch.Id,
            DonViId = request.DonViId,
            KyBaoCaoCode = kyBaoCaoCode,
            FinishedAt = batch.FinishedAt ?? _dateTimeProvider.Now,
        };
    }

    private IQueryable<DaoTaoHocVienEntity> ApplyReadScope(IQueryable<DaoTaoHocVienEntity> query)
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
            throw new AppException("DTHV_SCOPE_DENIED", "Khong co quyen thao tac du lieu dao tao hoc vien cua don vi khac.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Khong tim thay don vi.", 404);
        }
    }

    private static System.Linq.Expressions.Expression<Func<DaoTaoHocVienEntity, DaoTaoHocVienDto>> ToLiveDto()
        => x => new DaoTaoHocVienDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            Nam = x.Nam,
            NoiDungDaoTao = x.NoiDungDaoTao,
            SoTienSi = x.SoTienSi,
            SoThacSi = x.SoThacSi,
            SoDaiHoc = x.SoDaiHoc,
            SoCaoDang = x.SoCaoDang,
            SoTrungCap = x.SoTrungCap,
            KyBaoCaoCode = null,
            IsLatest = true,
            SnapshotVersion = 0,
            GhiChu = x.GhiChu,
        };

    private static DaoTaoHocVienDto MapLiveToDto(DaoTaoHocVienEntity entity)
        => new()
        {
            Id = entity.Id,
            DonViId = entity.DonViId,
            Nam = entity.Nam,
            NoiDungDaoTao = entity.NoiDungDaoTao,
            SoTienSi = entity.SoTienSi,
            SoThacSi = entity.SoThacSi,
            SoDaiHoc = entity.SoDaiHoc,
            SoCaoDang = entity.SoCaoDang,
            SoTrungCap = entity.SoTrungCap,
            KyBaoCaoCode = null,
            IsLatest = true,
            SnapshotVersion = 0,
            GhiChu = entity.GhiChu,
        };

    private static DaoTaoHocVienDto MapHisToDto(DaoTaoHocVienHisEntity entity)
        => new()
        {
            Id = entity.SourceId,
            DonViId = entity.DonViId,
            Nam = entity.Nam,
            NoiDungDaoTao = entity.NoiDungDaoTao,
            SoTienSi = entity.SoTienSi,
            SoThacSi = entity.SoThacSi,
            SoDaiHoc = entity.SoDaiHoc,
            SoCaoDang = entity.SoCaoDang,
            SoTrungCap = entity.SoTrungCap,
            KyBaoCaoCode = entity.KyBaoCaoCode,
            IsLatest = false,
            SnapshotVersion = 1,
            GhiChu = entity.GhiChu,
        };

    private static void ApplyValues(DaoTaoHocVienEntity entity, UpsertDaoTaoHocVienRequest request)
    {
        entity.DonViId = request.DonViId;
        entity.Nam = ResolveNam(request.Nam);
        entity.NoiDungDaoTao = request.NoiDungDaoTao.Trim();
        entity.SoTienSi = request.SoTienSi;
        entity.SoThacSi = request.SoThacSi;
        entity.SoDaiHoc = request.SoDaiHoc;
        entity.SoCaoDang = request.SoCaoDang;
        entity.SoTrungCap = request.SoTrungCap;
        entity.GhiChu = NormalizeText(request.GhiChu);
    }

    private static DaoTaoHocVienQuery NormalizeQuery(DaoTaoHocVienQuery query)
        => new()
        {
            DonViId = query.DonViId,
            Nam = query.Nam,
            KyBaoCaoCode = NormalizeText(query.KyBaoCaoCode),
        };

    private static UpsertDaoTaoHocVienRequest NormalizeRequest(UpsertDaoTaoHocVienRequest request, long? fallbackDonViId = null)
    {
        return new UpsertDaoTaoHocVienRequest
        {
            DonViId = request.DonViId > 0 ? request.DonViId : fallbackDonViId ?? 0,
            Nam = ResolveNam(request.Nam),
            NoiDungDaoTao = NormalizeRequiredCode(request.NoiDungDaoTao),
            SoTienSi = request.SoTienSi,
            SoThacSi = request.SoThacSi,
            SoDaiHoc = request.SoDaiHoc,
            SoCaoDang = request.SoCaoDang,
            SoTrungCap = request.SoTrungCap,
            GhiChu = NormalizeText(request.GhiChu),
        };
    }

    private static void ValidateMatrix(long donViId, List<UpsertDaoTaoHocVienRequest> items)
    {
        var duplicateContent = items
            .GroupBy(x => x.NoiDungDaoTao, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;

        if (duplicateContent is not null)
        {
            throw new AppException("DTHV_DUPLICATE_MATRIX", "Noi dung dao tao dang bi trung trong bang live du lieu.", 400);
        }

        var invalidScope = items.Any(x => x.DonViId != donViId);
        if (invalidScope)
        {
            throw new AppException("DTHV_INVALID_SCOPE", "Cac dong dao tao hoc vien phai cung don vi thao tac.", 400);
        }
    }

    private static string NormalizeRequiredCode(string? value)
    {
        var normalized = NormalizeText(value);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        throw new AppException("DTHV_REQUIRED", "Truong bat buoc khong duoc de trong.", 400);
    }

    private static int ResolveNam(int fallbackNam)
        => fallbackNam is >= 2000 and <= 2100 ? fallbackNam : DateTime.UtcNow.Year;

    private static string? NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}

public sealed class UpsertDaoTaoHocVienRequestValidator : AbstractValidator<UpsertDaoTaoHocVienRequest>
{
    public UpsertDaoTaoHocVienRequestValidator()
    {
        RuleFor(x => x.DonViId).GreaterThan(0);
        RuleFor(x => x.Nam).InclusiveBetween(2000, 2100);
        RuleFor(x => x.NoiDungDaoTao).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SoTienSi).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SoThacSi).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SoDaiHoc).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SoCaoDang).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SoTrungCap).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GhiChu).MaximumLength(500);
    }
}
