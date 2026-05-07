using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using HaTangMangEntity = ThucLuc.Domain.Entities.Business.HaTangMang;

namespace ThucLuc.Application.Features.HaTangMang;

public interface IHaTangMangService
{
    Task<IReadOnlyCollection<HaTangMangDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<HaTangMangDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<HaTangMangDto> UpsertAsync(long? id, UpsertHaTangMangRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<HaTangMangDto>> SaveMatrixAsync(SaveHaTangMangMatrixRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class HaTangMangService : IHaTangMangService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public HaTangMangService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<HaTangMangDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.HaTangMangs).Select(x => new HaTangMangDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            SoDonViTrucThuoc = x.SoDonViTrucThuoc,
            SoDaKetNoiBcanet = x.SoDaKetNoiBcanet,
            SoDuongTruyenVnpt = x.SoDuongTruyenVnpt,
            SoDuongTruyenKhac = x.SoDuongTruyenKhac,
            SoKetNoiInternet = x.SoKetNoiInternet,
            GhiChu = x.GhiChu
        }).ToListAsync(cancellationToken);

    public async Task<HaTangMangDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.HaTangMangs).Where(x => x.Id == id).Select(x => new HaTangMangDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            SoDonViTrucThuoc = x.SoDonViTrucThuoc,
            SoDaKetNoiBcanet = x.SoDaKetNoiBcanet,
            SoDuongTruyenVnpt = x.SoDuongTruyenVnpt,
            SoDuongTruyenKhac = x.SoDuongTruyenKhac,
            SoKetNoiInternet = x.SoKetNoiInternet,
            GhiChu = x.GhiChu
        }).FirstOrDefaultAsync(cancellationToken);

    public async Task<HaTangMangDto> UpsertAsync(long? id, UpsertHaTangMangRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        HaTangMangEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.HaTangMangs).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("HTM_NOT_FOUND", "Không tìm thấy bản ghi hạ tầng mạng.", 404);
        }
        else
        {
            entity = new HaTangMangEntity();
            await _dbContext.HaTangMangs.AddAsync(entity, cancellationToken);
        }

        entity.DonViId = request.DonViId;
        entity.SoDonViTrucThuoc = request.SoDonViTrucThuoc;
        entity.SoDaKetNoiBcanet = request.SoDaKetNoiBcanet;
        entity.SoDuongTruyenVnpt = request.SoDuongTruyenVnpt;
        entity.SoDuongTruyenKhac = request.SoDuongTruyenKhac;
        entity.SoKetNoiInternet = request.SoKetNoiInternet;
        entity.GhiChu = request.GhiChu;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<IReadOnlyCollection<HaTangMangDto>> SaveMatrixAsync(
        SaveHaTangMangMatrixRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);
        var existing = await _dbContext.HaTangMangs
            .IgnoreQueryFilters()
            .Where(x => x.DonViId == request.DonViId)
            .ToListAsync(cancellationToken);
        foreach (var e in existing) e.DeletedAt = _dateTimeProvider.Now;
        foreach (var req in (request.Items ?? []))
        {
            var entity = existing.FirstOrDefault() ?? new HaTangMangEntity();
            if (entity.Id == 0) await _dbContext.HaTangMangs.AddAsync(entity, cancellationToken);
            entity.DonViId = request.DonViId;
            entity.SoDonViTrucThuoc = req.SoDonViTrucThuoc;
            entity.SoDaKetNoiBcanet = req.SoDaKetNoiBcanet;
            entity.SoDuongTruyenVnpt = req.SoDuongTruyenVnpt;
            entity.SoDuongTruyenKhac = req.SoDuongTruyenKhac;
            entity.SoKetNoiInternet = req.SoKetNoiInternet;
            entity.GhiChu = string.IsNullOrWhiteSpace(req.GhiChu) ? null : req.GhiChu.Trim();
            entity.DeletedAt = null;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAllAsync(cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.HaTangMangs).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("HTM_NOT_FOUND", "Không tìm thấy bản ghi hạ tầng mạng.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<HaTangMangEntity> ApplyReadScope(IQueryable<HaTangMangEntity> query)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0)
        {
            query = query.Where(x => x.DonViId == currentUser.DonViId);
        }

        return query;
    }

    private async Task EnsureValidScopeAsync(long donViId, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0 && currentUser.DonViId != donViId)
        {
            throw new AppException("HTM_SCOPE_DENIED", "Không có quyền thao tác dữ liệu hạ tầng mạng của đơn vị khác.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        }
    }

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}
