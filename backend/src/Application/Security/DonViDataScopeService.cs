using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;

namespace ThucLuc.Application.Security;

public interface IDonViDataScopeService
{
    Task<DonViDataScope> GetScopeAsync(CancellationToken cancellationToken = default);
}

public sealed class DonViDataScope
{
    public bool HasFullAccess { get; init; }

    public IReadOnlyCollection<long> AllowedDonViIds { get; init; } = Array.Empty<long>();

    public bool Contains(long donViId)
        => HasFullAccess || AllowedDonViIds.Contains(donViId);
}

public sealed class DonViDataScopeService : IDonViDataScopeService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public DonViDataScopeService(IApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<DonViDataScope> GetScopeAsync(CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (currentUser.DonViId <= 0 || currentUser.HasPermission(Permissions.SystemAdmin))
        {
            return new DonViDataScope { HasFullAccess = true };
        }

        var currentDonVi = await _dbContext.DonVis
            .AsNoTracking()
            .Where(x => x.Id == currentUser.DonViId)
            .Select(x => new { x.Id, x.ParentId, x.CapDonVi })
            .FirstOrDefaultAsync(cancellationToken);

        if (currentDonVi is null)
        {
            return new DonViDataScope { HasFullAccess = false, AllowedDonViIds = Array.Empty<long>() };
        }

        // Don vi goc cap quan ly (khong co parent/cap CUC) co the xem toan bo.
        if (!currentDonVi.ParentId.HasValue
            || string.Equals(currentDonVi.CapDonVi, "CUC", StringComparison.OrdinalIgnoreCase))
        {
            return new DonViDataScope { HasFullAccess = true };
        }

        var allUnits = await _dbContext.DonVis
            .AsNoTracking()
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync(cancellationToken);

        var childrenByParent = allUnits
            .Where(x => x.ParentId.HasValue)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToArray());

        var allowed = new HashSet<long> { currentDonVi.Id };
        var queue = new Queue<long>();
        queue.Enqueue(currentDonVi.Id);

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            if (!childrenByParent.TryGetValue(parentId, out var children))
            {
                continue;
            }

            foreach (var childId in children)
            {
                if (!allowed.Add(childId))
                {
                    continue;
                }

                queue.Enqueue(childId);
            }
        }

        return new DonViDataScope
        {
            HasFullAccess = false,
            AllowedDonViIds = allowed.ToArray()
        };
    }
}