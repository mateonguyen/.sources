using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Features.Files;
using ThucLuc.Application.Security;
using VanBanQpplEntity = ThucLuc.Domain.Entities.Business.VanBanQppl;
using VanBanQpplHisEntity = ThucLuc.Domain.Entities.Business.VanBanQpplHis;

namespace ThucLuc.Application.Features.VanBanQppl;

public interface IVanBanQpplService
{
    Task<IReadOnlyCollection<VanBanQpplDto>> GetAllAsync(GetVanBanQpplQuery query, CancellationToken cancellationToken = default);

    Task<VanBanQpplDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<VanBanQpplDto> UpsertAsync(long? id, UpsertVanBanQpplRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class VanBanQpplService : IVanBanQpplService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IFileService _fileService;

    public VanBanQpplService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IFileService fileService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _fileService = fileService;
    }

    public async Task<IReadOnlyCollection<VanBanQpplDto>> GetAllAsync(GetVanBanQpplQuery query, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(query.KyBaoCaoCode))
        {
            var hisQuery = _dbContext.VanBanQpplHis.AsNoTracking()
                .Where(x => x.KyBaoCaoCode == query.KyBaoCaoCode)
                .Where(x => ApplyDonViScopePredicate(x.DonViId));

            if (query.DonViId.HasValue)
            {
                hisQuery = hisQuery.Where(x => x.DonViId == query.DonViId.Value);
            }

            return await hisQuery.Select(MapHisToDto()).ToListAsync(cancellationToken);
        }

        var liveQuery = ApplyReadScope(_dbContext.VanBanQppls);
        if (query.DonViId.HasValue)
        {
            liveQuery = liveQuery.Where(x => x.DonViId == query.DonViId.Value);
        }

        return await liveQuery.Select(MapLiveToDto()).ToListAsync(cancellationToken);
    }

    public async Task<VanBanQpplDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var dto = await ApplyReadScope(_dbContext.VanBanQppls)
            .Where(x => x.Id == id)
            .Select(MapLiveToDto())
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is not null)
        {
            dto.FileDinhKems = await _fileService.GetByEntityAsync("VanBanQppl", id, cancellationToken);
        }

        return dto;
    }

    public async Task<VanBanQpplDto> UpsertAsync(long? id, UpsertVanBanQpplRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        VanBanQpplEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.VanBanQppls)
                .FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("VANBAN_NOT_FOUND", "Không tìm thấy văn bản QPPL.", 404);
        }
        else
        {
            entity = new VanBanQpplEntity();
            await _dbContext.VanBanQppls.AddAsync(entity, cancellationToken);
        }

        entity.DonViId = request.DonViId;
        entity.SoHieu = request.SoHieu;
        entity.TenVanBan = request.TenVanBan;
        entity.LoaiVanBan = request.LoaiVanBan;
        entity.CoQuanBanHanh = request.CoQuanBanHanh;
        entity.NgayBanHanh = request.NgayBanHanh;
        entity.NgayHieuLuc = request.NgayHieuLuc;
        entity.LinhVuc = request.LinhVuc;
        entity.TrichYeu = request.TrichYeu;
        entity.TinhTrangTrienKhai = request.TinhTrangTrienKhai;
        entity.GhiChu = request.GhiChu;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.VanBanQppls).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("VANBAN_NOT_FOUND", "Không tìm thấy văn bản QPPL.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private bool ApplyDonViScopePredicate(long donViId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        return HasCrossDonViPermission(currentUser) || currentUser.DonViId <= 0 || currentUser.DonViId == donViId;
    }

    private static System.Linq.Expressions.Expression<Func<VanBanQpplEntity, VanBanQpplDto>> MapLiveToDto()
        => x => new VanBanQpplDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            KyBaoCaoCode = null,
            SoHieu = x.SoHieu,
            TenVanBan = x.TenVanBan,
            LoaiVanBan = x.LoaiVanBan,
            CoQuanBanHanh = x.CoQuanBanHanh,
            NgayBanHanh = x.NgayBanHanh,
            NgayHieuLuc = x.NgayHieuLuc,
            LinhVuc = x.LinhVuc,
            TrichYeu = x.TrichYeu,
            TinhTrangTrienKhai = x.TinhTrangTrienKhai,
            GhiChu = x.GhiChu
        };

    private static System.Linq.Expressions.Expression<Func<VanBanQpplHisEntity, VanBanQpplDto>> MapHisToDto()
        => x => new VanBanQpplDto
        {
            Id = x.SourceId,
            DonViId = x.DonViId,
            KyBaoCaoCode = x.KyBaoCaoCode,
            SoHieu = x.SoHieu,
            TenVanBan = x.TenVanBan,
            LoaiVanBan = x.LoaiVanBan,
            CoQuanBanHanh = x.CoQuanBanHanh,
            NgayBanHanh = x.NgayBanHanh,
            NgayHieuLuc = x.NgayHieuLuc,
            LinhVuc = x.LinhVuc,
            TrichYeu = x.TrichYeu,
            TinhTrangTrienKhai = x.TinhTrangTrienKhai,
            GhiChu = x.GhiChu
        };

    private IQueryable<VanBanQpplEntity> ApplyReadScope(IQueryable<VanBanQpplEntity> query)
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
            throw new AppException("VANBAN_SCOPE_DENIED", "Không có quyền thao tác văn bản QPPL của đơn vị khác.", 403);
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
