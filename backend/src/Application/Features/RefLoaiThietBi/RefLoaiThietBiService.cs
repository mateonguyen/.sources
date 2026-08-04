using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
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
}

public sealed class RefLoaiThietBiService : IRefLoaiThietBiService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IValidator<UpsertRefLoaiThietBiRequest> _validator;

    public RefLoaiThietBiService(IApplicationDbContext dbContext, IValidator<UpsertRefLoaiThietBiRequest> validator)
    {
        _dbContext = dbContext;
        _validator = validator;
    }

    public async Task<IReadOnlyCollection<RefLoaiThietBiDto>> GetTreeAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var activeFlag = true;
        var query = _dbContext.RefLoaiThietBis.AsQueryable();
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
                IsActive = x.IsActive
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
                IsActive = x.IsActive
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
            IsActive = request.IsActive
        };

        await _dbContext.RefLoaiThietBis.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<RefLoaiThietBiDto> UpdateAsync(long id, UpsertRefLoaiThietBiRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var entity = await _dbContext.RefLoaiThietBis.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
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

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RefLoaiThietBis.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
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
            .Where(x => x.Id == parentId)
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
}
