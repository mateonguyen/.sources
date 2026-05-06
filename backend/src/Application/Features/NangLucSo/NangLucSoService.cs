using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Entities.Reporting;
using NangLucSoEntity = ThucLuc.Domain.Entities.Business.NangLucSo;
using NangLucSoHisEntity = ThucLuc.Domain.Entities.Business.NangLucSoHis;

namespace ThucLuc.Application.Features.NangLucSo;

public interface INangLucSoService
{
    Task<IReadOnlyCollection<NangLucSoDto>> GetAllAsync(NangLucSoQuery query, CancellationToken cancellationToken = default);

    Task<NangLucSoDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<NangLucSoDto> UpsertAsync(long? id, UpsertNangLucSoRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NangLucSoDto>> SaveMatrixAsync(SaveNangLucSoMatrixRequest request, CancellationToken cancellationToken = default);

    Task<FinalizeNangLucSoResult> FinalizeAsync(FinalizeNangLucSoRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class NangLucSoService : INangLucSoService
{
    private static bool RequireExactAssessmentMatch => false;

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<UpsertNangLucSoRequest> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public NangLucSoService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IValidator<UpsertNangLucSoRequest> validator,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<NangLucSoDto>> GetAllAsync(NangLucSoQuery query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeQuery(query);
        var currentUser = _currentUserService.GetCurrentUser();
        var hasCrossDonViPermission = HasCrossDonViPermission(currentUser);
        var currentDonViId = currentUser.DonViId;

        if (!string.IsNullOrWhiteSpace(normalizedQuery.KyBaoCaoCode))
        {
            var hisQuery = _dbContext.NangLucSoHis
                .AsNoTracking()
                .Where(x => x.KyBaoCaoCode == normalizedQuery.KyBaoCaoCode);

            if (!hasCrossDonViPermission && currentDonViId > 0)
            {
                hisQuery = hisQuery.Where(x => x.DonViId == currentDonViId);
            }

            if (normalizedQuery.DonViId.HasValue)
            {
                hisQuery = hisQuery.Where(x => x.DonViId == normalizedQuery.DonViId.Value);
            }

            var hisItems = await hisQuery
                .OrderBy(x => x.NhomViTri)
                .ThenByDescending(x => x.SnapshotCreatedAt)
                .ThenByDescending(x => x.Id)
                .ToListAsync(cancellationToken);

            return hisItems
                .GroupBy(x => x.NhomViTri, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(x => x.NhomViTri)
                .Select(MapHisToDto)
                .ToList();
        }

        var liveQuery = ApplyReadScope(_dbContext.NangLucSos);
        if (normalizedQuery.DonViId.HasValue)
        {
            liveQuery = liveQuery.Where(x => x.DonViId == normalizedQuery.DonViId.Value);
        }

        var liveItems = await liveQuery
            .AsNoTracking()
            .OrderBy(x => x.NhomViTri)
            .ThenByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return liveItems
            .GroupBy(x => x.NhomViTri, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(x => x.NhomViTri)
            .Select(MapLiveToDto)
            .ToList();
    }

    public async Task<NangLucSoDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.NangLucSos)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : MapLiveToDto(entity);
    }

    public async Task<NangLucSoDto> UpsertAsync(long? id, UpsertNangLucSoRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedRequest = NormalizeRequest(request);
        await _validator.ValidateAndThrowAsync(normalizedRequest, cancellationToken);
        await EnsureValidScopeAsync(normalizedRequest.DonViId, cancellationToken);
        ValidateBusinessRule(normalizedRequest);

        NangLucSoEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.NangLucSos)
                .FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("NLS_NOT_FOUND", "Khong tim thay ban ghi nang luc so.", 404);
        }
        else
        {
            entity = new NangLucSoEntity();
            await _dbContext.NangLucSos.AddAsync(entity, cancellationToken);
        }

        ApplyValues(entity, normalizedRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<IReadOnlyCollection<NangLucSoDto>> SaveMatrixAsync(SaveNangLucSoMatrixRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        var items = (request.Items ?? [])
            .Select(item => NormalizeRequest(item, request.DonViId))
            .ToList();

        ValidateMatrix(request.DonViId, items);

        var existingItems = await _dbContext.NangLucSos
            .IgnoreQueryFilters()
            .Where(x => x.DonViId == request.DonViId)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingItems.Where(x => x.DeletedAt == null))
        {
            existing.DeletedAt = _dateTimeProvider.Now;
        }

        foreach (var item in items)
        {
            var entity = existingItems.FirstOrDefault(x => string.Equals(x.NhomViTri, item.NhomViTri, StringComparison.OrdinalIgnoreCase));
            if (entity is null)
            {
                entity = new NangLucSoEntity();
                existingItems.Add(entity);
                await _dbContext.NangLucSos.AddAsync(entity, cancellationToken);
            }

            ApplyValues(entity, item);
            entity.DeletedAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAllAsync(
            new NangLucSoQuery
            {
                DonViId = request.DonViId,
            },
            cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.NangLucSos)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("NLS_NOT_FOUND", "Khong tim thay ban ghi nang luc so.", 404);

        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<FinalizeNangLucSoResult> FinalizeAsync(FinalizeNangLucSoRequest request, CancellationToken cancellationToken = default)
    {
        var kyBaoCaoCode = NormalizeRequiredText(request.KyBaoCaoCode, "NLS_KY_REQUIRED", "Ky bao cao la bat buoc.");
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
            var liveItems = await ApplyReadScope(_dbContext.NangLucSos)
                .Where(x => x.DonViId == request.DonViId)
                .ToListAsync(cancellationToken);

            var hisRows = liveItems.Select(x => new NangLucSoHisEntity
            {
                SourceId = x.Id,
                DonViId = x.DonViId,
                NhomViTri = x.NhomViTri,
                TongSoDienDanhGia = x.TongSoDienDanhGia,
                TongSoDat = x.TongSoDat,
                TongSoChuaDat = x.TongSoChuaDat,
                GhiChu = x.GhiChu,
                KyBaoCaoCode = kyBaoCaoCode,
                SnapshotBatchId = batch.Id,
                SnapshotCreatedAt = now,
            }).ToList();

            if (hisRows.Count > 0)
            {
                await _dbContext.NangLucSoHis.AddRangeAsync(hisRows, cancellationToken);
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

        return new FinalizeNangLucSoResult
        {
            BatchId = batch.Id,
            DonViId = request.DonViId,
            KyBaoCaoCode = kyBaoCaoCode,
            FinishedAt = batch.FinishedAt ?? _dateTimeProvider.Now,
        };
    }

    private IQueryable<NangLucSoEntity> ApplyReadScope(IQueryable<NangLucSoEntity> query)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0)
        {
            query = query.Where(x => x.DonViId == currentUser.DonViId);
        }

        return query;
    }

    private NangLucSoQuery NormalizeQuery(NangLucSoQuery query)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var donViId = query.DonViId;

        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0)
        {
            donViId = currentUser.DonViId;
        }

        return new NangLucSoQuery
        {
            DonViId = donViId,
            KyBaoCaoCode = NormalizeOptional(query.KyBaoCaoCode),
        };
    }

    private async Task EnsureValidScopeAsync(long donViId, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0 && currentUser.DonViId != donViId)
        {
            throw new AppException("NLS_SCOPE_DENIED", "Khong co quyen thao tac du lieu nang luc so cua don vi khac.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Khong tim thay don vi.", 404);
        }
    }

    private static NangLucSoDto MapLiveToDto(NangLucSoEntity entity)
        => new()
        {
            Id = entity.Id,
            DonViId = entity.DonViId,
            NhomViTri = entity.NhomViTri,
            TongSoDienDanhGia = entity.TongSoDienDanhGia,
            TongSoDat = entity.TongSoDat,
            TongSoChuaDat = entity.TongSoChuaDat,
            KyBaoCaoCode = null,
            IsLatest = true,
            SnapshotVersion = 0,
            GhiChu = entity.GhiChu,
        };

    private static NangLucSoDto MapHisToDto(NangLucSoHisEntity entity)
        => new()
        {
            Id = entity.SourceId,
            DonViId = entity.DonViId,
            NhomViTri = entity.NhomViTri,
            TongSoDienDanhGia = entity.TongSoDienDanhGia,
            TongSoDat = entity.TongSoDat,
            TongSoChuaDat = entity.TongSoChuaDat,
            KyBaoCaoCode = entity.KyBaoCaoCode,
            IsLatest = false,
            SnapshotVersion = 1,
            GhiChu = entity.GhiChu,
        };

    private static UpsertNangLucSoRequest NormalizeRequest(
        UpsertNangLucSoRequest request,
        long? fallbackDonViId = null)
        => new()
        {
            DonViId = request.DonViId > 0 ? request.DonViId : fallbackDonViId ?? 0,
            NhomViTri = NormalizeRequiredText(request.NhomViTri, "NLS_GROUP_REQUIRED", "Nhom vi tri la bat buoc."),
            TongSoDienDanhGia = Math.Max(0, request.TongSoDienDanhGia),
            TongSoDat = Math.Max(0, request.TongSoDat),
            TongSoChuaDat = Math.Max(0, request.TongSoChuaDat),
            GhiChu = NormalizeOptional(request.GhiChu),
        };

    private static void ApplyValues(NangLucSoEntity entity, UpsertNangLucSoRequest request)
    {
        entity.DonViId = request.DonViId;
        entity.NhomViTri = request.NhomViTri;
        entity.TongSoDienDanhGia = request.TongSoDienDanhGia;
        entity.TongSoDat = request.TongSoDat;
        entity.TongSoChuaDat = request.TongSoChuaDat;
        entity.GhiChu = request.GhiChu;
    }

    private static void ValidateMatrix(long donViId, IReadOnlyCollection<UpsertNangLucSoRequest> items)
    {
        if (donViId <= 0)
        {
            throw new AppException("NLS_SCOPE_REQUIRED", "Don vi la bat buoc.", 400);
        }

        if (items.Count == 0)
        {
            throw new AppException("NLS_EMPTY_MATRIX", "Chua co du lieu nang luc so de luu.", 400);
        }

        var duplicatedGroup = items
            .GroupBy(x => x.NhomViTri, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (!string.IsNullOrWhiteSpace(duplicatedGroup))
        {
            throw new AppException("NLS_DUPLICATE_GROUP", $"Nhom vi tri '{duplicatedGroup}' bi lap trong bang nhap lieu.", 400);
        }

        foreach (var item in items)
        {
            ValidateBusinessRule(item);
        }
    }

    private static void ValidateBusinessRule(UpsertNangLucSoRequest request)
    {
        var accounted = request.TongSoDat + request.TongSoChuaDat;
        if (RequireExactAssessmentMatch)
        {
            if (accounted != request.TongSoDienDanhGia)
            {
                throw new AppException("NLS_TOTAL_MISMATCH", "Tong so dat va chua dat phai bang tong so dien danh gia.", 400);
            }

            return;
        }

        if (accounted > request.TongSoDienDanhGia)
        {
            throw new AppException("NLS_TOTAL_EXCEEDED", "Tong so dat va chua dat khong duoc vuot qua tong so dien danh gia.", 400);
        }
    }

    private static string NormalizeRequiredText(string? value, string code, string message)
        => string.IsNullOrWhiteSpace(value)
            ? throw new AppException(code, message, 400)
            : value.Trim();

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}

public sealed class UpsertNangLucSoRequestValidator : AbstractValidator<UpsertNangLucSoRequest>
{
    public UpsertNangLucSoRequestValidator()
    {
        RuleFor(x => x.DonViId).GreaterThan(0);
        RuleFor(x => x.NhomViTri).NotEmpty().MaximumLength(20);
        RuleFor(x => x.TongSoDienDanhGia).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TongSoDat).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TongSoChuaDat).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GhiChu).MaximumLength(500);
        RuleFor(x => x)
            .Must(IsAssessmentConsistent)
            .WithMessage("Tong so dat va chua dat khong duoc vuot qua tong so dien danh gia.");
    }

    private static bool IsAssessmentConsistent(UpsertNangLucSoRequest request)
        => request.TongSoDat + request.TongSoChuaDat <= request.TongSoDienDanhGia;
}
