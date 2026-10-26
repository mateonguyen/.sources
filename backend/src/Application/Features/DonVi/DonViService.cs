using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Entities.System;
using ThucLuc.Domain.Enums;
using DonViEntity = ThucLuc.Domain.Entities.System.DonVi;

namespace ThucLuc.Application.Features.DonVi;

public interface IDonViService
{
    Task<IReadOnlyCollection<DonViDto>> GetFlatListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DonViDto>> GetTreeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DonViParentCandidateDto>> GetParentCandidatesAsync(long? excludeId, CancellationToken cancellationToken = default);

    Task<DonViDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<DonViDto> UpsertAsync(long? id, UpsertDonViRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class DonViService : IDonViService
{
    private sealed class DonViNodeProjection
    {
        public long Id { get; init; }

        public string MaDonVi { get; init; } = string.Empty;

        public string TenDonVi { get; init; } = string.Empty;

        public string? TenVietTat { get; init; }

        public long? ParentId { get; init; }

        public string? DiaChi { get; init; }

        public string? CapDonVi { get; init; }

        public string? KhoiDonVi { get; init; }

        public string CheDoNhapLieu { get; init; } = "TU_NHAP";

        public string? WebsiteNoiBo { get; init; }

        public string? WebsiteInternet { get; init; }

        public int? TongBienChe { get; init; }

        public bool IsActive { get; init; }
    }

    private readonly IApplicationDbContext _dbContext;
    private readonly IValidator<UpsertDonViRequest> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IDonViDataScopeService _donViDataScopeService;
    private readonly ICurrentUserService _currentUserService;

    public DonViService(
        IApplicationDbContext dbContext,
        IValidator<UpsertDonViRequest> validator,
        IDateTimeProvider dateTimeProvider,
        IDonViDataScopeService donViDataScopeService,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
        _donViDataScopeService = donViDataScopeService;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyCollection<DonViDto>> GetFlatListAsync(CancellationToken cancellationToken = default)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);
        var query = QueryDonViNodes();
        if (!scope.HasFullAccess)
        {
            query = query.Where(x => scope.AllowedDonViIds.Contains(x.Id));
        }

        var items = await query
            .OrderBy(x => x.MaDonVi)
            .ThenBy(x => x.TenDonVi)
            .ToListAsync(cancellationToken);

        var countsByParent = BuildChildCounts(items);
        return items
            .Select(x => MapNode(x, countsByParent))
            .ToList();
    }

    public async Task<IReadOnlyCollection<DonViDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);
        var query = QueryDonViNodes();
        if (!scope.HasFullAccess)
        {
            query = query.Where(x => scope.AllowedDonViIds.Contains(x.Id));
        }

        var items = await query
            .OrderBy(x => x.MaDonVi)
            .ThenBy(x => x.TenDonVi)
            .ToListAsync(cancellationToken);

        var countsByParent = BuildChildCounts(items);

        var lookup = items.ToDictionary(x => x.Id, x => new DonViDto
        {
            Id = x.Id,
            MaDonVi = x.MaDonVi,
            TenDonVi = x.TenDonVi,
            TenVietTat = x.TenVietTat,
            ParentId = x.ParentId,
            DiaChi = x.DiaChi,
            CapDonVi = x.CapDonVi,
            KhoiDonVi = x.KhoiDonVi,
            CheDoNhapLieu = x.CheDoNhapLieu,
            WebsiteNoiBo = x.WebsiteNoiBo,
            WebsiteInternet = x.WebsiteInternet,
            TongBienChe = x.TongBienChe,
            SoDonViCapPhong = countsByParent.GetValueOrDefault(x.Id).CapPhong,
            SoDonViCapXa = countsByParent.GetValueOrDefault(x.Id).CapXa,
            IsActive = x.IsActive
        });

        var roots = new List<DonViDto>();
        foreach (var item in items)
        {
            var dto = lookup[item.Id];
            if (item.ParentId.HasValue && lookup.TryGetValue(item.ParentId.Value, out var parent))
            {
                dto.ParentDisplayName = $"{parent.MaDonVi} - {parent.TenDonVi}";
                var children = parent.Children.ToList();
                children.Add(dto);
                parent.Children = children;
            }
            else
            {
                roots.Add(dto);
            }
        }

        return SortTree(roots);
    }

    public async Task<IReadOnlyCollection<DonViParentCandidateDto>> GetParentCandidatesAsync(long? excludeId, CancellationToken cancellationToken = default)
    {
        var activeFlag = true;
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);
        var query = QueryDonViNodes();
        if (!scope.HasFullAccess)
        {
            query = query.Where(x => scope.AllowedDonViIds.Contains(x.Id));
        }

        var items = await query
            .Where(x => x.IsActive == activeFlag)
            .OrderBy(x => x.MaDonVi)
            .ThenBy(x => x.TenDonVi)
            .ToListAsync(cancellationToken);

        var excludedIds = excludeId.HasValue
            ? GetDescendantIds(items.Select(x => (x.Id, x.ParentId)).ToList(), excludeId.Value)
            : new HashSet<long>();
        if (excludeId.HasValue)
        {
            excludedIds.Add(excludeId.Value);
        }

        var childrenLookup = items
            .ToLookup(x => x.ParentId, x => x);

        var results = new List<DonViParentCandidateDto>();
        var visited = new HashSet<long>();

        void Visit(DonViNodeProjection node, int level)
        {
            if (!visited.Add(node.Id))
            {
                return;
            }

            if (!excludedIds.Contains(node.Id))
            {
                results.Add(new DonViParentCandidateDto
                {
                    Id = node.Id,
                    MaDonVi = node.MaDonVi,
                    TenDonVi = node.TenDonVi,
                    TenVietTat = node.TenVietTat,
                    Level = level,
                    DisplayName = $"{new string(' ', level * 2)}{node.MaDonVi} - {node.TenDonVi}"
                });
            }

            foreach (var child in childrenLookup[node.Id].OrderBy(x => x.MaDonVi).ThenBy(x => x.TenDonVi))
            {
                Visit(child, level + 1);
            }
        }

        foreach (var root in childrenLookup[null].OrderBy(x => x.MaDonVi).ThenBy(x => x.TenDonVi))
        {
            Visit(root, 0);
        }

        foreach (var item in items)
        {
            Visit(item, 0);
        }

        return results;
    }

    public async Task<DonViDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);
        if (!scope.Contains(id))
        {
            return null;
        }

        var donVi = await _dbContext.DonVis
            .AsNoTracking()
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (donVi is null)
        {
            return null;
        }

        var parentDisplayName = donVi.ParentId.HasValue
            ? await _dbContext.DonVis
                .AsNoTracking()
                .Where(parent => parent.Id == donVi.ParentId.Value)
                .Select(parent => parent.MaDonVi + " - " + parent.TenDonVi)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new DonViDto
        {
            Id = donVi.Id,
            MaDonVi = donVi.MaDonVi,
            TenDonVi = donVi.TenDonVi,
            TenVietTat = donVi.TenVietTat,
            ParentId = donVi.ParentId,
            ParentDisplayName = parentDisplayName,
            DiaChi = donVi.DiaChi,
            CapDonVi = donVi.CapDonVi,
            KhoiDonVi = donVi.KhoiDonVi,
            CheDoNhapLieu = donVi.CheDoNhapLieu,
            WebsiteNoiBo = donVi.WebsiteNoiBo,
            WebsiteInternet = donVi.WebsiteInternet,
            TongBienChe = donVi.TongBienChe,
            SoDonViCapPhong = donVi.Children.Count(child => child.IsActive && IsCapPhongByTen(child.TenDonVi)),
            SoDonViCapXa = donVi.Children.Count(child => child.IsActive && IsCapXaByTen(child.TenDonVi)),
            IsActive = donVi.IsActive
        };
    }

    public async Task<DonViDto> UpsertAsync(long? id, UpsertDonViRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);

        if (id.HasValue && !scope.Contains(id.Value))
        {
            throw new AppException("DONVI_OUTSIDE_SCOPE", "Không có quyền thao tác đơn vị ngoài phạm vi quản lý.", 403);
        }

        if (!id.HasValue && !request.ParentId.HasValue && !scope.HasFullAccess)
        {
            throw new AppException("DONVI_ROOT_FORBIDDEN", "Không có quyền tạo đơn vị gốc.", 403);
        }

        if (request.ParentId.HasValue && !scope.Contains(request.ParentId.Value))
        {
            throw new AppException("DONVI_PARENT_OUTSIDE_SCOPE", "Đơn vị cha nằm ngoài phạm vi quản lý.", 403);
        }

        DonViEntity entity;
        if (id.HasValue)
        {
            entity = await _dbContext.DonVis.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        }
        else
        {
            entity = new DonViEntity();
            await _dbContext.DonVis.AddAsync(entity, cancellationToken);
        }

        var oldIdentity = id.HasValue
            ? (entity.MaDonVi, entity.TenDonVi, entity.ParentId, entity.IsActive, entity.CapDonVi, entity.CheDoNhapLieu)
            : default;

        if (request.ParentId.HasValue)
        {
            if (id.HasValue && request.ParentId.Value == id.Value)
            {
                throw new BusinessRuleException("DONVI_INVALID_PARENT", "Đơn vị không thể là cha của chính nó.");
            }

            var parent = await _dbContext.DonVis
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.ParentId.Value, cancellationToken);
            if (parent is null)
            {
                throw new AppException("DONVI_PARENT_NOT_FOUND", "Không tìm thấy đơn vị cha.", 404);
            }

            if (!parent.IsActive)
            {
                throw new BusinessRuleException("DONVI_PARENT_INACTIVE", "Don vi cha dang ngung hoat dong.");
            }

            if (id.HasValue)
            {
                var lineage = await _dbContext.DonVis
                    .AsNoTracking()
                    .Select(x => new { x.Id, x.ParentId })
                    .ToListAsync(cancellationToken);
                var descendantIds = GetDescendantIds(lineage.Select(x => (x.Id, x.ParentId)).ToList(), id.Value);
                if (descendantIds.Contains(request.ParentId.Value))
                {
                    throw new BusinessRuleException("DONVI_CYCLE_DETECTED", "Khong the gan don vi cha nam trong cay con cua chinh no.");
                }
            }
        }

        var normalizedCapDonVi = NormalizeNullable(request.CapDonVi)?.ToUpperInvariant();
        var normalizedCheDoNhapLieu = NormalizeNullable(request.CheDoNhapLieu)?.ToUpperInvariant() ?? "TU_NHAP";
        ValidateCheDoNhapLieu(normalizedCapDonVi, normalizedCheDoNhapLieu);
        if (!request.IsActive && entity.IsActive)
        {
            if (await _dbContext.DonVis.CountAsync(x => x.ParentId == entity.Id && x.IsActive, cancellationToken) > 0)
                throw new BusinessRuleException("DONVI_HAS_CHILDREN", "Cần chuyển các đơn vị con trước khi ngừng hoạt động.");
            if (await _dbContext.Users.CountAsync(x => x.DonViId == entity.Id && x.IsActive, cancellationToken) > 0)
                throw new BusinessRuleException("DONVI_HAS_ACTIVE_USERS", "Cần chuyển hoặc vô hiệu hóa tài khoản trước khi ngừng hoạt động đơn vị.");
            if (await _dbContext.BaoCaoSnapshots.CountAsync(x => x.DonViId == entity.Id && x.TrangThai == SnapshotStatus.Draft, cancellationToken) > 0)
                throw new BusinessRuleException("DONVI_HAS_DRAFT_REPORTS", "Cần xử lý báo cáo nháp trước khi ngừng hoạt động đơn vị.");
        }

        entity.MaDonVi = request.MaDonVi.Trim().ToUpperInvariant();
        entity.TenDonVi = request.TenDonVi.Trim();
        entity.TenVietTat = request.TenVietTat?.Trim();
        entity.ParentId = request.ParentId;
        entity.DiaChi = request.DiaChi?.Trim();
        entity.CapDonVi = normalizedCapDonVi;
        entity.CapSo = DonViEntity.ResolveCapSo(normalizedCapDonVi);
        entity.KhoiDonVi = NormalizeNullable(request.KhoiDonVi)?.ToUpperInvariant();
        entity.CheDoNhapLieu = normalizedCheDoNhapLieu;
        entity.WebsiteNoiBo = NormalizeNullable(request.WebsiteNoiBo);
        entity.WebsiteInternet = NormalizeNullable(request.WebsiteInternet);
        entity.TongBienChe = request.TongBienChe;
        entity.IsActive = request.IsActive;

        if (_dbContext is not DbContext efDbContext)
            throw new InvalidOperationException("Database context does not support transactions.");
        await using var transaction = await efDbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (!id.HasValue || oldIdentity != (entity.MaDonVi, entity.TenDonVi, entity.ParentId, entity.IsActive, entity.CapDonVi, entity.CheDoNhapLieu))
        {
            await RecordVersionAsync(entity, cancellationToken);
            await _dbContext.DonViChanges.AddAsync(new DonViChange
            {
                ChangeType = id.HasValue ? "UPDATE" : "CREATE",
                EffectiveAt = _dateTimeProvider.Now,
                CreatedAt = _dateTimeProvider.Now,
                CreatedBy = _currentUserService.GetCurrentUser().UserId
            }, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);
        if (!scope.Contains(id))
        {
            throw new AppException("DONVI_OUTSIDE_SCOPE", "Không có quyền thao tác đơn vị ngoài phạm vi quản lý.", 403);
        }

        var entity = await _dbContext.DonVis.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);

        var hasChildren = await _dbContext.DonVis.CountAsync(x => x.ParentId == id && x.IsActive, cancellationToken) > 0;
        if (hasChildren)
        {
            throw new BusinessRuleException("DONVI_HAS_CHILDREN", "Không thể xóa đơn vị còn đơn vị con.");
        }

        if (!entity.IsActive) return;
        var activeUsers = await _dbContext.Users.CountAsync(x => x.DonViId == id && x.IsActive, cancellationToken);
        if (activeUsers > 0)
            throw new BusinessRuleException("DONVI_HAS_ACTIVE_USERS", "Cần chuyển hoặc vô hiệu hóa tài khoản trước khi ngừng hoạt động đơn vị.");
        if (await _dbContext.BaoCaoSnapshots.CountAsync(x => x.DonViId == id && x.TrangThai == SnapshotStatus.Draft, cancellationToken) > 0)
            throw new BusinessRuleException("DONVI_HAS_DRAFT_REPORTS", "Cần xử lý báo cáo nháp trước khi ngừng hoạt động đơn vị.");
        if (_dbContext is not DbContext efDbContext)
            throw new InvalidOperationException("Database context does not support transactions.");
        await using var transaction = await efDbContext.Database.BeginTransactionAsync(cancellationToken);
        entity.IsActive = false;
        await RecordVersionAsync(entity, cancellationToken);
        await _dbContext.DonViChanges.AddAsync(new DonViChange
        {
            ChangeType = "DEACTIVATE",
            EffectiveAt = _dateTimeProvider.Now,
            CreatedAt = _dateTimeProvider.Now,
            CreatedBy = _currentUserService.GetCurrentUser().UserId
        }, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RecordVersionAsync(DonViEntity entity, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        var currentVersion = await _dbContext.DonViVersions
            .Where(x => x.DonViId == entity.Id && x.ValidTo == null)
            .OrderByDescending(x => x.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);
        if (currentVersion is not null) currentVersion.ValidTo = now;
        await _dbContext.DonViVersions.AddAsync(new DonViVersion
        {
            DonViId = entity.Id,
            MaDonVi = entity.MaDonVi,
            TenDonVi = entity.TenDonVi,
            CapDonVi = entity.CapDonVi,
            CheDoNhapLieu = entity.CheDoNhapLieu,
            ParentId = entity.ParentId,
            IsActive = entity.IsActive,
            ValidFrom = now
        }, cancellationToken);
    }

    private IQueryable<DonViNodeProjection> QueryDonViNodes()
    {
        return _dbContext.DonVis
            .AsNoTracking()
            .Select(x => new DonViNodeProjection
            {
                Id = x.Id,
                MaDonVi = x.MaDonVi,
                TenDonVi = x.TenDonVi,
                TenVietTat = x.TenVietTat,
                ParentId = x.ParentId,
                DiaChi = x.DiaChi,
                CapDonVi = x.CapDonVi,
                KhoiDonVi = x.KhoiDonVi,
                CheDoNhapLieu = x.CheDoNhapLieu,
                WebsiteNoiBo = x.WebsiteNoiBo,
                WebsiteInternet = x.WebsiteInternet,
                TongBienChe = x.TongBienChe,
                IsActive = x.IsActive
            });
    }

    private static DonViDto MapNode(
        DonViNodeProjection node,
        IReadOnlyDictionary<long, (int CapPhong, int CapXa)> countsByParent)
    {
        var counts = countsByParent.GetValueOrDefault(node.Id);
        return new DonViDto
        {
            Id = node.Id,
            MaDonVi = node.MaDonVi,
            TenDonVi = node.TenDonVi,
            TenVietTat = node.TenVietTat,
            ParentId = node.ParentId,
            DiaChi = node.DiaChi,
            CapDonVi = node.CapDonVi,
            KhoiDonVi = node.KhoiDonVi,
            CheDoNhapLieu = node.CheDoNhapLieu,
            WebsiteNoiBo = node.WebsiteNoiBo,
            WebsiteInternet = node.WebsiteInternet,
            TongBienChe = node.TongBienChe,
            SoDonViCapPhong = counts.CapPhong,
            SoDonViCapXa = counts.CapXa,
            IsActive = node.IsActive
        };
    }

    private static Dictionary<long, (int CapPhong, int CapXa)> BuildChildCounts(
        IReadOnlyCollection<DonViNodeProjection> items)
    {
        return items
            .Where(x => x.ParentId.HasValue && x.IsActive)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (
                    group.Count(child => IsCapPhongByTen(child.TenDonVi)),
                    group.Count(child => IsCapXaByTen(child.TenDonVi))));
    }

    private static readonly string[] CapPhongPrefixes = { "Phòng", "Phong" };
    private static readonly string[] CapXaPrefixes = { "Công an xã", "Công an phường", "Công an thị trấn", "Cong an xa", "Cong an phuong", "Cong an thi tran" };

    private static bool IsCapPhongByTen(string tenDonVi)
    {
        var name = tenDonVi.Trim();
        foreach (var prefix in CapPhongPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCapXaByTen(string tenDonVi)
    {
        var name = tenDonVi.Trim();
        foreach (var prefix in CapXaPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? NormalizeNullable(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static void ValidateCheDoNhapLieu(string? capDonVi, string cheDoNhapLieu)
    {
        if (cheDoNhapLieu is not ("TU_NHAP" or "TONG_HOP"))
        {
            throw new AppException("DONVI_CHE_DO_NHAP_LIEU_INVALID", "Che do nhap lieu khong hop le.", 422);
        }

        if (cheDoNhapLieu == "TONG_HOP" && DonViEntity.ResolveCapSo(capDonVi) != 1)
        {
            throw new AppException("DONVI_CHE_DO_NHAP_LIEU_INVALID", "Chi don vi cap CAP_1 (truc thuoc Bo) moi duoc cau hinh TONG_HOP.", 422);
        }
    }

    private static IReadOnlyCollection<DonViDto> SortTree(IReadOnlyCollection<DonViDto> nodes)
    {
        return nodes
            .OrderBy(x => x.MaDonVi)
            .ThenBy(x => x.TenDonVi)
            .Select(x =>
            {
                x.Children = SortTree(x.Children);
                return x;
            })
            .ToList();
    }

    private static HashSet<long> GetDescendantIds(IReadOnlyCollection<(long Id, long? ParentId)> lineage, long rootId)
    {
        var childrenLookup = lineage
            .ToLookup(x => x.ParentId, x => x.Id);

        var result = new HashSet<long>();
        var stack = new Stack<long>();
        stack.Push(rootId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var childId in childrenLookup[current])
            {
                if (result.Add(childId))
                {
                    stack.Push(childId);
                }
            }
        }

        return result;
    }
}

public sealed class UpsertDonViRequestValidator : AbstractValidator<UpsertDonViRequest>
{
    public UpsertDonViRequestValidator()
    {
        RuleFor(x => x.MaDonVi).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TenDonVi).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TenVietTat).MaximumLength(50);
        RuleFor(x => x.DiaChi).MaximumLength(500);
        RuleFor(x => x.CapDonVi).MaximumLength(20);
        RuleFor(x => x.KhoiDonVi).MaximumLength(20);
        RuleFor(x => x.CheDoNhapLieu).MaximumLength(20);
        RuleFor(x => x.WebsiteNoiBo).MaximumLength(200);
        RuleFor(x => x.WebsiteInternet).MaximumLength(200);
        RuleFor(x => x.TongBienChe).GreaterThanOrEqualTo(0).When(x => x.TongBienChe.HasValue);
    }
}
