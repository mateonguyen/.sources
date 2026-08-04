using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Domain.Entities.System;
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

    public DonViService(IApplicationDbContext dbContext, IValidator<UpsertDonViRequest> validator, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<DonViDto>> GetFlatListAsync(CancellationToken cancellationToken = default)
    {
        var items = await QueryDonViNodes()
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
        var items = await QueryDonViNodes()
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
        var items = await QueryDonViNodes()
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

        entity.MaDonVi = request.MaDonVi.Trim().ToUpperInvariant();
        entity.TenDonVi = request.TenDonVi.Trim();
        entity.TenVietTat = request.TenVietTat?.Trim();
        entity.ParentId = request.ParentId;
        entity.DiaChi = request.DiaChi?.Trim();
        entity.CapDonVi = normalizedCapDonVi;
        entity.KhoiDonVi = NormalizeNullable(request.KhoiDonVi)?.ToUpperInvariant();
        entity.CheDoNhapLieu = normalizedCheDoNhapLieu;
        entity.WebsiteNoiBo = NormalizeNullable(request.WebsiteNoiBo);
        entity.WebsiteInternet = NormalizeNullable(request.WebsiteInternet);
        entity.TongBienChe = request.TongBienChe;
        entity.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.DonVis.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);

        var hasChildren = await _dbContext.DonVis.CountAsync(x => x.ParentId == id, cancellationToken) > 0;
        if (hasChildren)
        {
            throw new BusinessRuleException("DONVI_HAS_CHILDREN", "Không thể xóa đơn vị còn đơn vị con.");
        }

        var now = _dateTimeProvider.Now;
        entity.DeletedAt = now;
        entity.IsActive = false;
        entity.MaDonVi = $"{entity.MaDonVi}_GT{now:yyyyMMdd}";
        await _dbContext.SaveChangesAsync(cancellationToken);
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

        if (cheDoNhapLieu == "TONG_HOP" && capDonVi != "CAP_1")
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