using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Entities.System;
using RefLoaiThietBiEntity = ThucLuc.Domain.Entities.System.RefLoaiThietBi;

namespace ThucLuc.Application.Features.RefLoaiThietBi;

public sealed class RefLoaiThietBiDto
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string MaLoai { get; set; } = string.Empty;
    public string TenLoai { get; set; } = string.Empty;
    public int Cap { get; set; }
    public bool LaTongHop { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public long? DonViId { get; set; }
    public long? ReportingLoaiId { get; set; }
    public bool IsRequired { get; set; }
    public bool IsLocal { get; set; }
    public bool IsVisible { get; set; } = true;
    public List<RefLoaiThietBiDto> Children { get; set; } = new();
}

public sealed class UpsertRefLoaiThietBiRequest
{
    public long? ParentId { get; set; }
    public string MaLoai { get; set; } = string.Empty;
    public string TenLoai { get; set; } = string.Empty;
    public bool LaTongHop { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsRequired { get; set; }
}

public sealed class UpsertUnitRefLoaiThietBiRequest
{
    public long? ParentId { get; set; }
    public string TenLoai { get; set; } = string.Empty;
    public bool LaTongHop { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SetRefLoaiThietBiVisibilityRequest
{
    public bool IsVisible { get; set; }
}

public sealed class UnitRefLoaiThietBiProposalDto
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public long DonViId { get; set; }
    public string TenDonVi { get; set; } = string.Empty;
    public string TenLoai { get; set; } = string.Empty;
    public string? TenNhomCha { get; set; }
    public bool LaTongHop { get; set; }
    public int SortOrder { get; set; }
    public int SoBanGhiSuDung { get; set; }
    public bool CoLoaiCon { get; set; }
}

public sealed class StandardizeUnitRefLoaiThietBiRequest
{
    public long? TargetStandardId { get; set; }
    public string? MaLoaiMoi { get; set; }
}

public sealed class UpsertRefLoaiThietBiRequestValidator : AbstractValidator<UpsertRefLoaiThietBiRequest>
{
    public UpsertRefLoaiThietBiRequestValidator()
    {
        RuleFor(x => x.MaLoai).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TenLoai).NotEmpty().MaximumLength(200);
    }
}

public interface IRefLoaiThietBiService
{
    Task<IReadOnlyCollection<RefLoaiThietBiDto>> GetTreeAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<RefLoaiThietBiDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<RefLoaiThietBiDto> CreateAsync(UpsertRefLoaiThietBiRequest request, CancellationToken cancellationToken = default);

    Task<RefLoaiThietBiDto> UpdateAsync(long id, UpsertRefLoaiThietBiRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RefLoaiThietBiDto>> GetEffectiveTreeAsync(bool includeHidden = false, CancellationToken cancellationToken = default);

    Task<RefLoaiThietBiDto> CreateUnitAsync(UpsertUnitRefLoaiThietBiRequest request, CancellationToken cancellationToken = default);

    Task<RefLoaiThietBiDto> UpdateUnitAsync(long id, UpsertUnitRefLoaiThietBiRequest request, CancellationToken cancellationToken = default);

    Task DeleteUnitAsync(long id, CancellationToken cancellationToken = default);

    Task SetUnitVisibilityAsync(long id, bool isVisible, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UnitRefLoaiThietBiProposalDto>> GetUnitProposalsAsync(CancellationToken cancellationToken = default);

    Task StandardizeUnitProposalAsync(long id, StandardizeUnitRefLoaiThietBiRequest request, CancellationToken cancellationToken = default);
}

public sealed class RefLoaiThietBiService : IRefLoaiThietBiService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IValidator<UpsertRefLoaiThietBiRequest> _validator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDonViDataScopeService _donViDataScopeService;

    public RefLoaiThietBiService(
        IApplicationDbContext dbContext,
        IValidator<UpsertRefLoaiThietBiRequest> validator,
        ICurrentUserService currentUserService,
        IDonViDataScopeService donViDataScopeService)
    {
        _dbContext = dbContext;
        _validator = validator;
        _currentUserService = currentUserService;
        _donViDataScopeService = donViDataScopeService;
    }

    public async Task<IReadOnlyCollection<RefLoaiThietBiDto>> GetTreeAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var activeFlag = true;
        var query = _dbContext.RefLoaiThietBis.Where(x => x.DonViId == null);
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive == activeFlag);
        }

        var all = await query
            .OrderBy(x => x.SortOrder)
            .Select(x => new RefLoaiThietBiDto
            {
                Id = x.Id,
                ParentId = x.ParentId,
                MaLoai = x.MaLoai,
                TenLoai = x.TenLoai,
                Cap = x.Cap,
                LaTongHop = x.LaTongHop,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                DonViId = x.DonViId,
                ReportingLoaiId = x.ReportingLoaiId,
                IsRequired = x.IsRequired,
                IsLocal = false,
                IsVisible = true
            })
            .ToListAsync(cancellationToken);

        return BuildTree(all, parentId: null);
    }

    // De quy - khong gioi han so cap, dua theo PARENT_ID tu tham chieu trong DB.
    private static List<RefLoaiThietBiDto> BuildTree(List<RefLoaiThietBiDto> all, long? parentId)
    {
        var level = all.Where(x => x.ParentId == parentId).OrderBy(x => x.SortOrder).ToList();
        foreach (var node in level)
        {
            node.Children = BuildTree(all, node.Id);
        }
        return level;
    }

    public async Task<RefLoaiThietBiDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await _dbContext.RefLoaiThietBis
            .Where(x => x.Id == id)
            .Select(x => new RefLoaiThietBiDto
            {
                Id = x.Id,
                ParentId = x.ParentId,
                MaLoai = x.MaLoai,
                TenLoai = x.TenLoai,
                Cap = x.Cap,
                LaTongHop = x.LaTongHop,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                DonViId = x.DonViId,
                ReportingLoaiId = x.ReportingLoaiId,
                IsRequired = x.IsRequired,
                IsLocal = x.DonViId != null,
                IsVisible = true
            })
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<RefLoaiThietBiDto> CreateAsync(UpsertRefLoaiThietBiRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var cap = await ResolveCapAsync(request.ParentId, cancellationToken);
        await EnsureMaLoaiUniqueAsync(request.MaLoai, null, cancellationToken);

        var entity = new RefLoaiThietBiEntity
        {
            ParentId = request.ParentId,
            MaLoai = request.MaLoai.Trim(),
            TenLoai = request.TenLoai.Trim(),
            Cap = cap,
            LaTongHop = request.LaTongHop,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            IsRequired = request.IsRequired
        };

        await _dbContext.RefLoaiThietBis.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<RefLoaiThietBiDto> UpdateAsync(long id, UpsertRefLoaiThietBiRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var entity = await _dbContext.RefLoaiThietBis.FirstOrDefaultAsync(x => x.Id == id && x.DonViId == null, cancellationToken)
            ?? throw new AppException("REF_LOAI_THIET_BI_NOT_FOUND", "Không tìm thấy loại thiết bị.", 404);

        var hasChildren = await _dbContext.RefLoaiThietBis.AnyAsync(x => x.ParentId == id, cancellationToken);
        if (hasChildren && request.ParentId is not null)
        {
            throw new AppException("REF_LOAI_THIET_BI_HAS_CHILDREN", "Nhóm đang có loại con, không thể chuyển thành loại con của nhóm khác.", 400);
        }

        var cap = await ResolveCapAsync(request.ParentId, cancellationToken, id);
        await EnsureMaLoaiUniqueAsync(request.MaLoai, id, cancellationToken);

        entity.ParentId = request.ParentId;
        entity.MaLoai = request.MaLoai.Trim();
        entity.TenLoai = request.TenLoai.Trim();
        entity.Cap = cap;
        entity.LaTongHop = cap == 2 && request.LaTongHop;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.IsRequired = request.IsRequired;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RefLoaiThietBis.FirstOrDefaultAsync(x => x.Id == id && x.DonViId == null, cancellationToken)
            ?? throw new AppException("REF_LOAI_THIET_BI_NOT_FOUND", "Không tìm thấy loại thiết bị.", 404);

        var activeFlag = true;
        var hasActiveChildren = await _dbContext.RefLoaiThietBis
            .AnyAsync(x => x.ParentId == id && x.IsActive == activeFlag, cancellationToken);
        if (hasActiveChildren)
        {
            throw new AppException("REF_LOAI_THIET_BI_HAS_CHILDREN", "Nhóm đang có loại con đang hoạt động, hãy vô hiệu hóa các loại con trước.", 400);
        }

        var inUse = await _dbContext.ThietBiCntts
            .AnyAsync(x => x.LoaiThietBiId == id && x.DeletedAt == null, cancellationToken);
        if (inUse)
        {
            throw new AppException("REF_LOAI_THIET_BI_IN_USE", "Loại thiết bị đang được sử dụng bởi ít nhất 1 thiết bị CNTT, không thể vô hiệu hóa.", 400);
        }

        entity.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<RefLoaiThietBiDto>> GetEffectiveTreeAsync(
        bool includeHidden = false,
        CancellationToken cancellationToken = default)
    {
        var donViId = GetCurrentDonViId();
        var activeFlag = true;
        var all = await _dbContext.RefLoaiThietBis
            .Where(x => x.IsActive == activeFlag && (x.DonViId == null || x.DonViId == donViId))
            .OrderBy(x => x.SortOrder)
            .Select(x => new RefLoaiThietBiDto
            {
                Id = x.Id,
                ParentId = x.ParentId,
                MaLoai = x.MaLoai,
                TenLoai = x.TenLoai,
                Cap = x.Cap,
                LaTongHop = x.LaTongHop,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive,
                DonViId = x.DonViId,
                ReportingLoaiId = x.ReportingLoaiId,
                IsRequired = x.IsRequired,
                IsLocal = x.DonViId != null,
                IsVisible = true
            })
            .ToListAsync(cancellationToken);
        const string catalogKey = "REF:LOAI_THIET_BI";
        var preferences = await _dbContext.UnitCatalogItemSettings
            .Where(x => x.DonViId == donViId && x.CatalogKey == catalogKey)
            .ToDictionaryAsync(x => x.ItemId, x => x.IsVisible, cancellationToken);

        foreach (var item in all.Where(x => !x.IsLocal))
        {
            item.IsVisible = item.IsRequired || !preferences.TryGetValue(item.Id, out var visible) || visible;
        }
        var includedIds = all
            .Where(x => includeHidden || x.IsVisible)
            .Select(x => x.Id)
            .ToHashSet();
        var filtered = all.Where(x => includedIds.Contains(x.Id) && (!x.ParentId.HasValue || includedIds.Contains(x.ParentId.Value))).ToList();
        return BuildTree(filtered, parentId: null);
    }

    public async Task<RefLoaiThietBiDto> CreateUnitAsync(
        UpsertUnitRefLoaiThietBiRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUnitRequest(request);
        var donViId = GetCurrentDonViId();
        await EnsureUnitNameUniqueAsync(request.TenLoai, request.ParentId, donViId, null, cancellationToken);
        var cap = await ResolveUnitCapAsync(request.ParentId, donViId, cancellationToken);
        var code = $"DV_{donViId}_{Guid.NewGuid():N}";
        var entity = new RefLoaiThietBiEntity
        {
            ParentId = request.ParentId,
            MaLoai = code[..Math.Min(50, code.Length)],
            TenLoai = request.TenLoai.Trim(),
            Cap = cap,
            LaTongHop = request.LaTongHop,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            DonViId = donViId,
            ReportingLoaiId = null,
            IsRequired = false
        };
        await _dbContext.RefLoaiThietBis.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<RefLoaiThietBiDto> UpdateUnitAsync(
        long id,
        UpsertUnitRefLoaiThietBiRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUnitRequest(request);
        var donViId = GetCurrentDonViId();
        var entity = await _dbContext.RefLoaiThietBis
            .FirstOrDefaultAsync(x => x.Id == id && x.DonViId == donViId, cancellationToken)
            ?? throw new AppException("UNIT_LOAI_THIET_BI_NOT_FOUND", "Không tìm thấy loại thiết bị riêng của đơn vị.", 404);
        if (request.ParentId == id)
        {
            throw new AppException("REF_LOAI_THIET_BI_INVALID_PARENT", "Loại thiết bị không thể là cha của chính nó.", 400);
        }
        if (request.ParentId.HasValue && await IsDescendantAsync(id, request.ParentId.Value, donViId, cancellationToken))
        {
            throw new AppException("REF_LOAI_THIET_BI_INVALID_PARENT", "Không thể chuyển một loại thiết bị vào chính nhánh con của nó.", 400);
        }
        await EnsureUnitNameUniqueAsync(request.TenLoai, request.ParentId, donViId, id, cancellationToken);
        entity.ParentId = request.ParentId;
        entity.Cap = await ResolveUnitCapAsync(request.ParentId, donViId, cancellationToken);
        entity.TenLoai = request.TenLoai.Trim();
        entity.LaTongHop = request.LaTongHop;
        entity.ReportingLoaiId = null;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteUnitAsync(long id, CancellationToken cancellationToken = default)
    {
        var donViId = GetCurrentDonViId();
        var entity = await _dbContext.RefLoaiThietBis
            .FirstOrDefaultAsync(x => x.Id == id && x.DonViId == donViId, cancellationToken)
            ?? throw new AppException("UNIT_LOAI_THIET_BI_NOT_FOUND", "Không tìm thấy loại thiết bị riêng của đơn vị.", 404);
        var inUse = await _dbContext.ThietBiCntts.AnyAsync(x => x.LoaiThietBiId == id && x.DeletedAt == null, cancellationToken);
        var activeFlag = true;
        var hasChildren = await _dbContext.RefLoaiThietBis.AnyAsync(x => x.ParentId == id && x.IsActive == activeFlag, cancellationToken);
        if (inUse || hasChildren)
        {
            throw new AppException("UNIT_LOAI_THIET_BI_IN_USE", "Loại thiết bị đang được sử dụng hoặc còn loại con, không thể ngừng áp dụng.", 400);
        }
        entity.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetUnitVisibilityAsync(long id, bool isVisible, CancellationToken cancellationToken = default)
    {
        var donViId = GetCurrentDonViId();
        var standard = await _dbContext.RefLoaiThietBis
            .FirstOrDefaultAsync(x => x.Id == id && x.DonViId == null, cancellationToken)
            ?? throw new AppException("REF_LOAI_THIET_BI_NOT_FOUND", "Không tìm thấy loại thiết bị chuẩn.", 404);
        if (standard.IsRequired && !isVisible)
        {
            throw new AppException("REF_LOAI_THIET_BI_REQUIRED", "Loại thiết bị bắt buộc không thể ẩn khỏi đơn vị.", 400);
        }
        if (!isVisible)
        {
            var affectedIds = await GetBranchIdsAsync(id, donViId, cancellationToken);
            var requiredFlag = true;
            var hasRequiredDescendant = await _dbContext.RefLoaiThietBis.AnyAsync(
                x => x.DonViId == null && affectedIds.Contains(x.Id) && x.IsRequired == requiredFlag,
                cancellationToken);
            if (hasRequiredDescendant)
            {
                throw new AppException(
                    "REF_LOAI_THIET_BI_REQUIRED",
                    "Nhánh này có loại thiết bị chuẩn bắt buộc nên không thể ẩn.",
                    400);
            }
            var inUse = await _dbContext.ThietBiCntts.AnyAsync(
                x => x.DonViId == donViId && affectedIds.Contains(x.LoaiThietBiId) && x.DeletedAt == null,
                cancellationToken);
            if (inUse)
            {
                throw new AppException("REF_LOAI_THIET_BI_IN_USE", "Đơn vị đang có thiết bị sử dụng loại này nên chưa thể ẩn.", 400);
            }
        }

        const string catalogKey = "REF:LOAI_THIET_BI";
        var preference = await _dbContext.UnitCatalogItemSettings.FirstOrDefaultAsync(
            x => x.DonViId == donViId && x.CatalogKey == catalogKey && x.ItemId == id,
            cancellationToken);
        if (preference is null)
        {
            preference = new UnitCatalogItemSetting
            {
                DonViId = donViId,
                CatalogKey = catalogKey,
                ItemId = id,
                IsVisible = isVisible
            };
            await _dbContext.UnitCatalogItemSettings.AddAsync(preference, cancellationToken);
        }
        else
        {
            preference.IsVisible = isVisible;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UnitRefLoaiThietBiProposalDto>> GetUnitProposalsAsync(
        CancellationToken cancellationToken = default)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);
        var activeFlag = true;
        var query = _dbContext.RefLoaiThietBis
            .AsNoTracking()
            .Where(x => x.DonViId.HasValue && x.IsActive == activeFlag);
        if (!scope.HasFullAccess)
        {
            query = query.Where(x => scope.AllowedDonViIds.Contains(x.DonViId!.Value));
        }

        var items = await query.OrderBy(x => x.TenLoai).ToListAsync(cancellationToken);
        if (items.Count == 0)
        {
            return [];
        }

        var donViIds = items.Select(x => x.DonViId!.Value).Distinct().ToArray();
        var donViNames = await _dbContext.DonVis.AsNoTracking()
            .Where(x => donViIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.TenDonVi, cancellationToken);
        var parentIds = items.Where(x => x.ParentId.HasValue).Select(x => x.ParentId!.Value).Distinct().ToArray();
        var parentNames = await _dbContext.RefLoaiThietBis.AsNoTracking()
            .Where(x => parentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.TenLoai, cancellationToken);
        var itemIds = items.Select(x => x.Id).ToArray();
        var usageCounts = await _dbContext.ThietBiCntts.AsNoTracking()
            .Where(x => itemIds.Contains(x.LoaiThietBiId) && x.DeletedAt == null)
            .GroupBy(x => x.LoaiThietBiId)
            .Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        var idsWithChildren = (await _dbContext.RefLoaiThietBis.AsNoTracking()
                .Where(x => x.ParentId.HasValue && itemIds.Contains(x.ParentId.Value) && x.IsActive == activeFlag)
                .Select(x => x.ParentId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        return items.Select(x => new UnitRefLoaiThietBiProposalDto
        {
            Id = x.Id,
            ParentId = x.ParentId,
            DonViId = x.DonViId!.Value,
            TenDonVi = donViNames.GetValueOrDefault(x.DonViId.Value, $"Đơn vị #{x.DonViId.Value}"),
            TenLoai = x.TenLoai,
            TenNhomCha = x.ParentId.HasValue ? parentNames.GetValueOrDefault(x.ParentId.Value) : null,
            LaTongHop = x.LaTongHop,
            SortOrder = x.SortOrder,
            SoBanGhiSuDung = usageCounts.GetValueOrDefault(x.Id),
            CoLoaiCon = idsWithChildren.Contains(x.Id)
        }).ToList();
    }

    public async Task StandardizeUnitProposalAsync(
        long id,
        StandardizeUnitRefLoaiThietBiRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);
        var activeFlag = true;
        var item = await _dbContext.RefLoaiThietBis
            .FirstOrDefaultAsync(x => x.Id == id && x.DonViId.HasValue && x.IsActive == activeFlag, cancellationToken)
            ?? throw new AppException("UNIT_LOAI_THIET_BI_NOT_FOUND", "Không tìm thấy loại thiết bị do đơn vị bổ sung.", 404);
        if (!scope.Contains(item.DonViId!.Value))
        {
            throw new AppException("UNIT_LOAI_THIET_BI_SCOPE_DENIED", "Không có quyền xử lý mục bổ sung của đơn vị này.", 403);
        }

        var hasChildren = await _dbContext.RefLoaiThietBis.AnyAsync(
            x => x.ParentId == item.Id && x.IsActive == activeFlag,
            cancellationToken);
        if (request.TargetStandardId.HasValue)
        {
            if (hasChildren)
            {
                throw new AppException(
                    "UNIT_LOAI_THIET_BI_HAS_CHILDREN",
                    "Mục bổ sung còn loại con. Hãy xử lý các loại con trước khi hợp nhất.",
                    400);
            }
            var target = await _dbContext.RefLoaiThietBis.FirstOrDefaultAsync(
                x => x.Id == request.TargetStandardId.Value && x.DonViId == null && x.IsActive == activeFlag,
                cancellationToken)
                ?? throw new AppException("STANDARD_LOAI_THIET_BI_NOT_FOUND", "Không tìm thấy loại thiết bị chuẩn để hợp nhất.", 404);
            var targetHasChildren = await _dbContext.RefLoaiThietBis.AnyAsync(
                x => x.ParentId == target.Id && x.IsActive == activeFlag,
                cancellationToken);
            var hasUsage = await _dbContext.ThietBiCntts.AnyAsync(
                x => x.LoaiThietBiId == item.Id && x.DeletedAt == null,
                cancellationToken);
            if (hasUsage && targetHasChildren)
            {
                throw new AppException(
                    "STANDARD_LOAI_THIET_BI_NOT_LEAF",
                    "Loại chuẩn được chọn còn loại con nên không thể nhận dữ liệu thiết bị trực tiếp.",
                    400);
            }

            var liveRows = await _dbContext.ThietBiCntts
                .Where(x => x.LoaiThietBiId == item.Id)
                .ToListAsync(cancellationToken);
            foreach (var row in liveRows)
            {
                row.LoaiThietBiId = target.Id;
            }
            var historyRows = await _dbContext.ThietBiCnttHis
                .Where(x => x.LoaiThietBiId == item.Id)
                .ToListAsync(cancellationToken);
            foreach (var row in historyRows)
            {
                row.LoaiThietBiId = target.Id;
            }
            item.IsActive = false;
        }
        else
        {
            var maLoai = request.MaLoaiMoi?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(maLoai) || maLoai.Length > 50)
            {
                throw new AppException("STANDARD_LOAI_CODE_INVALID", "Mã loại chuẩn phải có từ 1 đến 50 ký tự.", 400);
            }
            var duplicated = await _dbContext.RefLoaiThietBis.AnyAsync(
                x => x.DonViId == null
                    && (x.MaLoai.ToUpper() == maLoai || x.TenLoai.ToUpper() == item.TenLoai.ToUpper()),
                cancellationToken);
            if (duplicated)
            {
                throw new AppException(
                    "STANDARD_LOAI_DUPLICATE",
                    "Danh mục chuẩn đã có mã hoặc tên này. Hãy dùng chức năng Hợp nhất.",
                    409);
            }
            if (item.ParentId.HasValue)
            {
                var parentIsLocal = await _dbContext.RefLoaiThietBis.AnyAsync(
                    x => x.Id == item.ParentId.Value && x.DonViId.HasValue,
                    cancellationToken);
                if (parentIsLocal)
                {
                    throw new AppException(
                        "STANDARD_LOAI_PARENT_LOCAL",
                        "Nhóm cha vẫn là mục bổ sung. Hãy duyệt nhóm cha vào chuẩn trước.",
                        400);
                }
            }
            item.MaLoai = maLoai;
            item.DonViId = null;
            item.IsRequired = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private long GetCurrentDonViId()
    {
        var donViId = _currentUserService.GetCurrentUser().DonViId;
        return donViId > 0
            ? donViId
            : throw new AppException("UNIT_CONTEXT_REQUIRED", "Không xác định được đơn vị đang sử dụng.", 400);
    }

    private static void ValidateUnitRequest(UpsertUnitRefLoaiThietBiRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenLoai) || request.TenLoai.Trim().Length > 200)
        {
            throw new AppException("UNIT_LOAI_THIET_BI_NAME_INVALID", "Tên loại thiết bị phải có từ 1 đến 200 ký tự.", 400);
        }
    }

    private async Task<int> ResolveUnitCapAsync(long? parentId, long donViId, CancellationToken cancellationToken)
    {
        if (!parentId.HasValue)
        {
            return 1;
        }
        var activeFlag = true;
        var parent = await _dbContext.RefLoaiThietBis
            .Where(x => x.Id == parentId.Value && x.IsActive == activeFlag && (x.DonViId == null || x.DonViId == donViId))
            .Select(x => new { x.Cap })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppException("REF_LOAI_THIET_BI_INVALID_PARENT", "Nhóm cha không thuộc danh mục hiệu lực của đơn vị.", 400);
        return parent.Cap + 1;
    }

    private async Task<int> ResolveCapAsync(long? parentId, CancellationToken cancellationToken, long? selfId = null)
    {
        if (parentId is null)
        {
            return 1;
        }

        if (parentId == selfId)
        {
            throw new AppException("REF_LOAI_THIET_BI_INVALID_PARENT", "Loại thiết bị không thể là cha của chính nó.", 400);
        }

        var parent = await _dbContext.RefLoaiThietBis
            .Where(x => x.Id == parentId && x.DonViId == null)
            .Select(x => new { x.Id, x.Cap })
            .FirstOrDefaultAsync(cancellationToken);

        if (parent is null)
        {
            throw new AppException("REF_LOAI_THIET_BI_INVALID_PARENT", "Không tìm thấy nhóm cha.", 404);
        }

        // Khong gioi han so cap - cap cua node = cap cua cha + 1, du theo cay
        // PARENT_ID tu tham chieu (tren dung nhu thiet ke DB).
        return parent.Cap + 1;
    }

    private async Task EnsureMaLoaiUniqueAsync(string maLoai, long? selfId, CancellationToken cancellationToken)
    {
        var normalized = maLoai.Trim();
        var duplicated = await _dbContext.RefLoaiThietBis
            .AnyAsync(x => x.MaLoai == normalized && x.Id != (selfId ?? 0), cancellationToken);

        if (duplicated)
        {
            throw new AppException("REF_LOAI_THIET_BI_DUPLICATE_MA", "Mã loại thiết bị đã tồn tại.", 409);
        }
    }

    private async Task EnsureUnitNameUniqueAsync(
        string tenLoai,
        long? parentId,
        long donViId,
        long? selfId,
        CancellationToken cancellationToken)
    {
        var normalized = tenLoai.Trim().ToUpperInvariant();
        var duplicated = await _dbContext.RefLoaiThietBis.AnyAsync(
            x => (x.DonViId == null || x.DonViId == donViId)
                && x.ParentId == parentId
                && x.Id != (selfId ?? 0)
                && x.TenLoai.ToUpper() == normalized,
            cancellationToken);
        if (duplicated)
        {
            throw new AppException("UNIT_LOAI_THIET_BI_DUPLICATE", "Danh mục đang áp dụng tại đơn vị đã có loại thiết bị cùng tên.", 409);
        }
    }

    private async Task<bool> IsDescendantAsync(
        long ancestorId,
        long candidateId,
        long donViId,
        CancellationToken cancellationToken)
    {
        var nodes = await _dbContext.RefLoaiThietBis
            .Where(x => x.DonViId == null || x.DonViId == donViId)
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync(cancellationToken);
        var byId = nodes.ToDictionary(x => x.Id);
        var currentId = candidateId;
        var guard = 0;
        while (byId.TryGetValue(currentId, out var current) && guard++ <= nodes.Count)
        {
            if (current.Id == ancestorId)
            {
                return true;
            }
            if (!current.ParentId.HasValue)
            {
                return false;
            }
            currentId = current.ParentId.Value;
        }
        return false;
    }

    private async Task<HashSet<long>> GetBranchIdsAsync(
        long rootId,
        long donViId,
        CancellationToken cancellationToken)
    {
        var nodes = await _dbContext.RefLoaiThietBis
            .Where(x => x.DonViId == null || x.DonViId == donViId)
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync(cancellationToken);
        var result = new HashSet<long> { rootId };
        var queue = new Queue<long>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            foreach (var childId in nodes.Where(x => x.ParentId == parentId).Select(x => x.Id))
            {
                if (result.Add(childId))
                {
                    queue.Enqueue(childId);
                }
            }
        }
        return result;
    }
}
