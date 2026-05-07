using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Entities.Business;
using HeThongThongTinEntity = ThucLuc.Domain.Entities.Business.HeThongThongTin;

namespace ThucLuc.Application.Features.HeThongThongTin;

public interface IHeThongThongTinService
{
    Task<IReadOnlyCollection<HeThongThongTinDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<HeThongThongTinDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<HeThongThongTinDto> UpsertAsync(long? id, UpsertHeThongThongTinRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class HeThongThongTinService : IHeThongThongTinService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public HeThongThongTinService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<HeThongThongTinDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.HeThongThongTins)
            .OrderBy(x => x.TenPhanMem)
            .Select(MapToDto())
            .ToListAsync(cancellationToken);

    public async Task<HeThongThongTinDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.HeThongThongTins)
            .Where(x => x.Id == id)
            .Select(MapToDto())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<HeThongThongTinDto> UpsertAsync(long? id, UpsertHeThongThongTinRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        HeThongThongTinEntity entity;
        bool isNew = !id.HasValue;

        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.HeThongThongTins).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("HTTT_NOT_FOUND", "Không tìm thấy hệ thống thông tin.", 404);
        }
        else
        {
            entity = new HeThongThongTinEntity { ValidFrom = _dateTimeProvider.Now, VersionNo = 1 };
            await _dbContext.HeThongThongTins.AddAsync(entity, cancellationToken);
        }

        var normalizedPhamViHoatDong = NormalizeText(request.PhamViHoatDong);
        var normalizedPhamViHoatDongKyThuat = NormalizeText(request.PhamViHoatDongKyThuat);
        var normalizedUngDungCnMoi = NormalizeText(request.UngDungCnMoi);
        var normalizedKhaNangTichHop = NormalizeText(request.KhaNangTichHop);
        var newDaCongNhanSangKien = request.DaCongNhanSangKien;

        if (!isNew)
        {
            bool shouldVersion =
                normalizedPhamViHoatDong != entity.PhamViHoatDong ||
                normalizedPhamViHoatDongKyThuat != entity.PhamViHoatDongKyThuat ||
                normalizedUngDungCnMoi != entity.UngDungCnMoi ||
                normalizedKhaNangTichHop != entity.KhaNangTichHop ||
                newDaCongNhanSangKien != entity.DaCongNhanSangKien;

            if (shouldVersion)
            {
                var now = _dateTimeProvider.Now;
                await _dbContext.HeThongThongTinHis.AddAsync(new HeThongThongTinHis
                {
                    SourceId = entity.Id,
                    DonViId = entity.DonViId,
                    TenPhanMem = entity.TenPhanMem,
                    DonViPhatTrien = entity.DonViPhatTrien,
                    DonViQuanLy = entity.DonViQuanLy,
                    NamTrienKhai = entity.NamTrienKhai,
                    PhamViHoatDong = entity.PhamViHoatDong,
                    PhamViHoatDongKyThuat = entity.PhamViHoatDongKyThuat,
                    UngDungCnMoi = entity.UngDungCnMoi,
                    KhaNangTichHop = entity.KhaNangTichHop,
                    DaCongNhanSangKien = entity.DaCongNhanSangKien,
                    GhiChu = entity.GhiChu,
                    ValidFrom = entity.ValidFrom,
                    ValidTo = now,
                    VersionNo = entity.VersionNo,
                }, cancellationToken);
                entity.ValidFrom = now;
                entity.VersionNo++;
            }
        }

        entity.DonViId = request.DonViId;
        entity.TenPhanMem = NormalizeRequired(request.TenPhanMem);
        entity.DonViPhatTrien = NormalizeText(request.DonViPhatTrien);
        entity.DonViQuanLy = NormalizeText(request.DonViQuanLy);
        entity.NamTrienKhai = request.NamTrienKhai;
        entity.PhamViHoatDong = normalizedPhamViHoatDong;
        entity.PhamViHoatDongKyThuat = normalizedPhamViHoatDongKyThuat;
        entity.UngDungCnMoi = normalizedUngDungCnMoi;
        entity.KhaNangTichHop = normalizedKhaNangTichHop;
        entity.DaCongNhanSangKien = newDaCongNhanSangKien;
        entity.GhiChu = NormalizeText(request.GhiChu);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await ApplyReadScope(_dbContext.HeThongThongTins)
            .Where(x => x.Id == entity.Id)
            .Select(MapToDto())
            .FirstAsync(cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.HeThongThongTins).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("HTTT_NOT_FOUND", "Không tìm thấy hệ thống thông tin.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<HeThongThongTinEntity> ApplyReadScope(IQueryable<HeThongThongTinEntity> query)
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
            throw new AppException("HTTT_SCOPE_DENIED", "Không có quyền thao tác dữ liệu hệ thống thông tin của đơn vị khác.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        }
    }

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);

    private static System.Linq.Expressions.Expression<Func<HeThongThongTinEntity, HeThongThongTinDto>> MapToDto()
        => x => new HeThongThongTinDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            TenPhanMem = x.TenPhanMem,
            DonViPhatTrien = x.DonViPhatTrien,
            DonViQuanLy = x.DonViQuanLy,
            NamTrienKhai = x.NamTrienKhai,
            PhamViHoatDong = x.PhamViHoatDong,
            PhamViHoatDongKyThuat = x.PhamViHoatDongKyThuat,
            UngDungCnMoi = x.UngDungCnMoi,
            KhaNangTichHop = x.KhaNangTichHop,
            DaCongNhanSangKien = x.DaCongNhanSangKien,
            GhiChu = x.GhiChu
        };

    private static string NormalizeRequired(string value) => value.Trim();

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
