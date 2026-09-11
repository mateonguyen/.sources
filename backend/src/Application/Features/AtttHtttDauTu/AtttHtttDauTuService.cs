using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using AtttHtttDauTuEntity = ThucLuc.Domain.Entities.Business.AtttHtttDauTu;

namespace ThucLuc.Application.Features.AtttHtttDauTu;

public interface IAtttHtttDauTuService
{
    Task<IReadOnlyCollection<AtttHtttDauTuDto>> GetAllAsync(AtttHtttDauTuQuery query, CancellationToken cancellationToken = default);

    Task<AtttHtttDauTuDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<AtttHtttDauTuDto> UpsertAsync(long? id, UpsertAtttHtttDauTuRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class AtttHtttDauTuService : IAtttHtttDauTuService
{
    private static readonly HashSet<string> LoaiHaTangCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BCANET", "INTERNET", "KHAC"
    };

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AtttHtttDauTuService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<AtttHtttDauTuDto>> GetAllAsync(AtttHtttDauTuQuery query, CancellationToken cancellationToken = default)
    {
        var liveQuery = ApplyReadScope(_dbContext.AtttHtttDauTus.AsNoTracking());
        if (query.DonViId.HasValue)
        {
            liveQuery = liveQuery.Where(x => x.DonViId == query.DonViId.Value);
        }

        return await liveQuery
            .OrderBy(x => x.HtttId)
            .ThenBy(x => x.Id)
            .Select(MapLiveToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<AtttHtttDauTuDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.AtttHtttDauTus)
            .Where(x => x.Id == id)
            .Select(MapLiveToDto())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<AtttHtttDauTuDto> UpsertAsync(long? id, UpsertAtttHtttDauTuRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        AtttHtttDauTuEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.AtttHtttDauTus).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("ATTTDT_NOT_FOUND", "Không tìm thấy bản ghi ATTT HTTT đầu tư.", 404);

            if (entity.DonViId != request.DonViId)
            {
                throw new AppException(
                    "ATTTDT_DONVI_IMMUTABLE",
                    "Không thể chuyển bản ghi ATTT HTTT đầu tư sang đơn vị khác.",
                    422);
            }
        }
        else
        {
            entity = new AtttHtttDauTuEntity();
            await _dbContext.AtttHtttDauTus.AddAsync(entity, cancellationToken);
        }

        await EnsureValidHtttAsync(id, request, cancellationToken);

        var loaiHaTang = NormalizeOptional(request.LoaiHaTang)?.ToUpperInvariant();
        if (loaiHaTang is null || !LoaiHaTangCodes.Contains(loaiHaTang))
        {
            throw new AppException(
                "ATTTDT_LOAI_HA_TANG_INVALID",
                "Nhóm hạ tầng phải là BCANet, Internet hoặc Hệ thống khác.",
                422);
        }

        entity.DonViId = request.DonViId;
        entity.HtttId = request.HtttId;
        entity.LoaiHaTang = loaiHaTang;
        entity.ChuQuan = NormalizeOptional(request.ChuQuan);
        entity.DonViVanHanh = NormalizeOptional(request.DonViVanHanh);
        entity.CapDoDeXuat = NormalizeOptional(request.CapDoDeXuat);
        entity.NgayPheDuyetHsdxcd = request.NgayPheDuyetHsdxcd;
        entity.QuyetDinhPheDuyet = NormalizeOptional(request.QuyetDinhPheDuyet);
        entity.DaLongGhepThuyetMinh = request.DaLongGhepThuyetMinh;
        entity.GhiChu = NormalizeOptional(request.GhiChu);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.AtttHtttDauTus).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("ATTTDT_NOT_FOUND", "Không tìm thấy bản ghi ATTT HTTT đầu tư.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<AtttHtttDauTuEntity> ApplyReadScope(IQueryable<AtttHtttDauTuEntity> query)
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
            throw new AppException("ATTTDT_SCOPE_DENIED", "Không có quyền thao tác dữ liệu ATTT HTTT đầu tư của đơn vị khác.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        }
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task EnsureValidHtttAsync(
        long? id,
        UpsertAtttHtttDauTuRequest request,
        CancellationToken cancellationToken)
    {
        var httt = await _dbContext.HeThongThongTins
            .AsNoTracking()
            .Where(x => x.Id == request.HtttId)
            .Select(x => new { x.Id, x.DonViId })
            .FirstOrDefaultAsync(cancellationToken);

        if (httt is null)
        {
            throw new AppException("ATTTDT_HTTT_NOT_FOUND", "Không tìm thấy hệ thống thông tin đã chọn.", 404);
        }

        if (httt.DonViId != request.DonViId)
        {
            throw new AppException(
                "ATTTDT_HTTT_SCOPE_MISMATCH",
                "Hệ thống thông tin đã chọn không thuộc đơn vị đang khai báo.",
                422);
        }

        var duplicateExists = await _dbContext.AtttHtttDauTus
            .AsNoTracking()
            .AnyAsync(
                x => x.DonViId == request.DonViId
                    && x.HtttId == request.HtttId
                    && (!id.HasValue || x.Id != id.Value),
                cancellationToken);

        if (duplicateExists)
        {
            throw new AppException(
                "ATTTDT_HTTT_DUPLICATE",
                "Hệ thống thông tin này đã có trong danh sách ATTT HTTT đầu tư.",
                409);
        }
    }

    private static System.Linq.Expressions.Expression<Func<AtttHtttDauTuEntity, AtttHtttDauTuDto>> MapLiveToDto()
        => x => new AtttHtttDauTuDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            HtttId = x.HtttId,
            LoaiHaTang = x.LoaiHaTang,
            ChuQuan = x.ChuQuan,
            DonViVanHanh = x.DonViVanHanh,
            CapDoDeXuat = x.CapDoDeXuat,
            NgayPheDuyetHsdxcd = x.NgayPheDuyetHsdxcd,
            QuyetDinhPheDuyet = x.QuyetDinhPheDuyet,
            DaLongGhepThuyetMinh = x.DaLongGhepThuyetMinh,
            GhiChu = x.GhiChu
        };

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}
