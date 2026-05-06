using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using AtttHtttVanHanhEntity = ThucLuc.Domain.Entities.Business.AtttHtttVanHanh;
using AtttHtttVanHanhHisEntity = ThucLuc.Domain.Entities.Business.AtttHtttVanHanhHis;

namespace ThucLuc.Application.Features.AtttHtttVanHanh;

public interface IAtttHtttVanHanhService
{
    Task<IReadOnlyCollection<AtttHtttVanHanhDto>> GetAllAsync(AtttHtttVanHanhQuery query, CancellationToken cancellationToken = default);

    Task<AtttHtttVanHanhDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<AtttHtttVanHanhDto> UpsertAsync(long? id, UpsertAtttHtttVanHanhRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class AtttHtttVanHanhService : IAtttHtttVanHanhService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AtttHtttVanHanhService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<AtttHtttVanHanhDto>> GetAllAsync(AtttHtttVanHanhQuery query, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(query.KyBaoCaoCode))
        {
            var hisQuery = _dbContext.AtttHtttVanHanhHis.AsNoTracking()
                .Where(x => x.KyBaoCaoCode == query.KyBaoCaoCode)
                .Where(x => ApplyDonViScopePredicate(x.DonViId));

            if (query.DonViId.HasValue)
            {
                hisQuery = hisQuery.Where(x => x.DonViId == query.DonViId.Value);
            }

            return await hisQuery.Select(MapHisToDto()).ToListAsync(cancellationToken);
        }

        var liveQuery = ApplyReadScope(_dbContext.AtttHtttVanHanhs);
        if (query.DonViId.HasValue)
        {
            liveQuery = liveQuery.Where(x => x.DonViId == query.DonViId.Value);
        }

        return await liveQuery.Select(MapLiveToDto()).ToListAsync(cancellationToken);
    }

    public async Task<AtttHtttVanHanhDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.AtttHtttVanHanhs)
            .Where(x => x.Id == id)
            .Select(MapLiveToDto())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<AtttHtttVanHanhDto> UpsertAsync(long? id, UpsertAtttHtttVanHanhRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        AtttHtttVanHanhEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.AtttHtttVanHanhs).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("ATTTVH_NOT_FOUND", "Không tìm thấy bản ghi ATTT HTTT vận hành.", 404);
        }
        else
        {
            entity = new AtttHtttVanHanhEntity();
            await _dbContext.AtttHtttVanHanhs.AddAsync(entity, cancellationToken);
        }

        entity.DonViId = request.DonViId;
        entity.HtttId = request.HtttId;
        entity.ChuQuan = NormalizeOptional(request.ChuQuan);
        entity.DonViVanHanh = NormalizeOptional(request.DonViVanHanh);
        entity.CapDoDeXuat = NormalizeOptional(request.CapDoDeXuat);
        entity.TinhTrangPheDuyet = NormalizeOptional(request.TinhTrangPheDuyet)?.ToUpperInvariant();
        entity.QuyetDinhPheDuyet = NormalizeOptional(request.QuyetDinhPheDuyet);
        entity.QuyCheAttt = NormalizeOptional(request.QuyCheAttt);
        entity.DuKienNgayPheDuyet = request.DuKienNgayPheDuyet;
        entity.DaTrienKhaiPhuongAn = request.DaTrienKhaiPhuongAn;
        entity.DuKienNgayTrienKhai = request.DuKienNgayTrienKhai;
        entity.KiemTraDanhGia = NormalizeOptional(request.KiemTraDanhGia);
        entity.GhiChu = request.GhiChu;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.AtttHtttVanHanhs).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("ATTTVH_NOT_FOUND", "Không tìm thấy bản ghi ATTT HTTT vận hành.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<AtttHtttVanHanhEntity> ApplyReadScope(IQueryable<AtttHtttVanHanhEntity> query)
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
            throw new AppException("ATTTVH_SCOPE_DENIED", "Không có quyền thao tác dữ liệu ATTT HTTT vận hành của đơn vị khác.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        }
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private bool ApplyDonViScopePredicate(long donViId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        return HasCrossDonViPermission(currentUser) || currentUser.DonViId <= 0 || currentUser.DonViId == donViId;
    }

    private static System.Linq.Expressions.Expression<Func<AtttHtttVanHanhEntity, AtttHtttVanHanhDto>> MapLiveToDto()
        => x => new AtttHtttVanHanhDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            KyBaoCaoCode = null,
            HtttId = x.HtttId,
            ChuQuan = x.ChuQuan,
            DonViVanHanh = x.DonViVanHanh,
            CapDoDeXuat = x.CapDoDeXuat,
            TinhTrangPheDuyet = x.TinhTrangPheDuyet,
            QuyetDinhPheDuyet = x.QuyetDinhPheDuyet,
            QuyCheAttt = x.QuyCheAttt,
            DuKienNgayPheDuyet = x.DuKienNgayPheDuyet,
            DaTrienKhaiPhuongAn = x.DaTrienKhaiPhuongAn,
            DuKienNgayTrienKhai = x.DuKienNgayTrienKhai,
            KiemTraDanhGia = x.KiemTraDanhGia,
            GhiChu = x.GhiChu
        };

    private static System.Linq.Expressions.Expression<Func<AtttHtttVanHanhHisEntity, AtttHtttVanHanhDto>> MapHisToDto()
        => x => new AtttHtttVanHanhDto
        {
            Id = x.SourceId,
            DonViId = x.DonViId,
            KyBaoCaoCode = x.KyBaoCaoCode,
            HtttId = x.HtttId,
            ChuQuan = x.ChuQuan,
            DonViVanHanh = x.DonViVanHanh,
            CapDoDeXuat = x.CapDoDeXuat,
            TinhTrangPheDuyet = x.TinhTrangPheDuyet,
            QuyetDinhPheDuyet = x.QuyetDinhPheDuyet,
            QuyCheAttt = x.QuyCheAttt,
            DuKienNgayPheDuyet = x.DuKienNgayPheDuyet,
            DaTrienKhaiPhuongAn = x.DaTrienKhaiPhuongAn,
            DuKienNgayTrienKhai = x.DuKienNgayTrienKhai,
            KiemTraDanhGia = x.KiemTraDanhGia,
            GhiChu = x.GhiChu
        };

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}
