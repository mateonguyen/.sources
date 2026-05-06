using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;

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
    public List<RefLoaiThietBiDto> Children { get; set; } = new();
}

public interface IRefLoaiThietBiService
{
    Task<IReadOnlyCollection<RefLoaiThietBiDto>> GetTreeAsync(CancellationToken cancellationToken = default);
}

public sealed class RefLoaiThietBiService : IRefLoaiThietBiService
{
    private readonly IApplicationDbContext _dbContext;

    public RefLoaiThietBiService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<RefLoaiThietBiDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var all = await _dbContext.RefLoaiThietBis
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new RefLoaiThietBiDto
            {
                Id = x.Id,
                ParentId = x.ParentId,
                MaLoai = x.MaLoai,
                TenLoai = x.TenLoai,
                Cap = x.Cap,
                LaTongHop = x.LaTongHop,
                SortOrder = x.SortOrder
            })
            .ToListAsync(cancellationToken);

        var roots = all.Where(x => x.ParentId == null).ToList();
        foreach (var root in roots)
        {
            root.Children = all.Where(x => x.ParentId == root.Id).OrderBy(x => x.SortOrder).ToList();
        }

        return roots;
    }
}