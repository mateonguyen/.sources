using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;

namespace ThucLuc.Application.Features.ThongBao;

public interface IThongBaoService
{
    Task<IReadOnlyCollection<ThongBaoDto>> GetCurrentUserNotificationsAsync(CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(long id, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(CancellationToken cancellationToken = default);
}

public sealed class ThongBaoService : IThongBaoService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ThongBaoService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<ThongBaoDto>> GetCurrentUserNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        return await _dbContext.ThongBaos
            .Where(x => x.UserId == null || x.UserId == currentUser.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ThongBaoDto
            {
                Id = x.Id,
                LoaiThongBao = x.LoaiThongBao,
                TieuDe = x.TieuDe,
                NoiDung = x.NoiDung,
                DaDoc = x.DaDoc,
                CreatedAt = x.CreatedAt
            })
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsReadAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ThongBaos.FirstAsync(x => x.Id == id, cancellationToken);
        entity.DaDoc = true;
        entity.DocLuc = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var items = await _dbContext.ThongBaos.Where(x => (x.UserId == null || x.UserId == currentUser.UserId) && !x.DaDoc).ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.DaDoc = true;
            item.DocLuc = _dateTimeProvider.Now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}