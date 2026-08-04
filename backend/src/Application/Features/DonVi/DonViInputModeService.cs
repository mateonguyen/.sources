using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;

namespace ThucLuc.Application.Features.DonVi;

public interface IDonViInputModeService
{
    Task<DonViInputModeContext> GetContextAsync(long donViId, CancellationToken cancellationToken = default);
}

public sealed class DonViInputModeContext
{
    public long DonViId { get; init; }

    public string CapDonVi { get; init; } = string.Empty;

    public string CheDoNhapLieu { get; init; } = "TU_NHAP";

    public IReadOnlyCollection<long> DescendantDonViIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// Các đơn vị được gộp vào báo cáo khi TONG_HOP: CHỈ đơn vị cấp dưới
    /// (đơn vị tổng hợp không tự nhập — dữ liệu tự nhập cũ không được tính).
    /// Fallback về chính nó khi không có đơn vị con để tránh báo cáo rỗng vô nghĩa.
    /// </summary>
    public IReadOnlyCollection<long> AggregateDonViIds { get; init; } = Array.Empty<long>();

    public bool IsTongHop => CheDoNhapLieu == "TONG_HOP";
}

public sealed class DonViInputModeService : IDonViInputModeService
{
    private sealed class UnitNode
    {
        public long Id { get; init; }

        public long? ParentId { get; init; }

        public bool IsActive { get; init; }

        public string? CapDonVi { get; init; }

        public string? CheDoNhapLieu { get; init; }
    }

    private readonly IApplicationDbContext _dbContext;

    public DonViInputModeService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DonViInputModeContext> GetContextAsync(long donViId, CancellationToken cancellationToken = default)
    {
        var allUnits = await _dbContext.DonVis
            .AsNoTracking()
            .Select(x => new UnitNode
            {
                Id = x.Id,
                ParentId = x.ParentId,
                IsActive = x.IsActive,
                CapDonVi = x.CapDonVi,
                CheDoNhapLieu = x.CheDoNhapLieu,
            })
            .ToListAsync(cancellationToken);

        var current = allUnits.FirstOrDefault(x => x.Id == donViId)
            ?? throw new AppException("DONVI_NOT_FOUND", "Khong tim thay don vi.", 404);

        var descendants = GetDescendantIds(allUnits, donViId)
            .Where(id => allUnits.Any(x => x.Id == id && x.IsActive))
            .ToArray();

        // TONG_HOP = gộp từ đơn vị cấp dưới, KHÔNG tính dữ liệu tự nhập của chính đơn vị.
        // Không có đơn vị con thì fallback về chính nó (tránh snapshot rỗng).
        var aggregateDonViIds = descendants.Length > 0
            ? descendants
            : new long[] { donViId };

        return new DonViInputModeContext
        {
            DonViId = donViId,
            CapDonVi = (current.CapDonVi ?? string.Empty).ToUpperInvariant(),
            CheDoNhapLieu = NormalizeCheDoNhapLieu(current.CheDoNhapLieu),
            DescendantDonViIds = descendants,
            AggregateDonViIds = aggregateDonViIds,
        };
    }

    private static string NormalizeCheDoNhapLieu(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized is "TONG_HOP" ? "TONG_HOP" : "TU_NHAP";
    }

    private static IReadOnlyCollection<long> GetDescendantIds(
        IReadOnlyCollection<UnitNode> allUnits,
        long rootId)
    {
        var childrenLookup = allUnits
            .Where(x => x.ParentId is not null)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(x => x.Id).ToArray());

        var result = new HashSet<long>();
        var stack = new Stack<long>();
        stack.Push(rootId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!childrenLookup.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var childId in children)
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
