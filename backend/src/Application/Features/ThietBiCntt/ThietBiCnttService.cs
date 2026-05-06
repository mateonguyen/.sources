using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Entities.Business;
using ThietBiCnttEntity = ThucLuc.Domain.Entities.Business.ThietBiCntt;

namespace ThucLuc.Application.Features.ThietBiCntt;

public interface IThietBiCnttService
{
    Task<IReadOnlyCollection<ThietBiCnttDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ThietBiCnttDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<ThietBiCnttDto> UpsertAsync(long? id, UpsertThietBiCnttRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ThietBiCnttService : IThietBiCnttService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ThietBiCnttService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<ThietBiCnttDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.ThietBiCntts)
            .Include(x => x.UngDungs)
            .Select(x => new ThietBiCnttDto
            {
                Id = x.Id,
                DonViId = x.DonViId,
                LoaiThietBiId = x.LoaiThietBiId,
                TenThietBi = x.TenThietBi,
                HangSanXuat = x.HangSanXuat,
                Model = x.Model,
                CauHinh = x.CauHinh,
                HeDieuHanh = x.HeDieuHanh,
                DonViSuDung = x.DonViSuDung,
                SoLuongTong = x.SoLuongTong,
                SoLuongHienDung = x.SoLuongHienDung,
                SoLuongHong = x.SoLuongHong,
                TinhTrang = x.TinhTrang,
                UngDungIds = x.UngDungs.Select(u => u.HeThongId).ToList(),
                GhiChu = x.GhiChu
            }).ToListAsync(cancellationToken);

    public async Task<ThietBiCnttDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.ThietBiCntts)
            .Include(x => x.UngDungs)
            .Where(x => x.Id == id).Select(x => new ThietBiCnttDto
            {
                Id = x.Id,
                DonViId = x.DonViId,
                LoaiThietBiId = x.LoaiThietBiId,
                TenThietBi = x.TenThietBi,
                HangSanXuat = x.HangSanXuat,
                Model = x.Model,
                CauHinh = x.CauHinh,
                HeDieuHanh = x.HeDieuHanh,
                DonViSuDung = x.DonViSuDung,
                SoLuongTong = x.SoLuongTong,
                SoLuongHienDung = x.SoLuongHienDung,
                SoLuongHong = x.SoLuongHong,
                TinhTrang = x.TinhTrang,
                UngDungIds = x.UngDungs.Select(u => u.HeThongId).ToList(),
                GhiChu = x.GhiChu
            }).FirstOrDefaultAsync(cancellationToken);

    public async Task<ThietBiCnttDto> UpsertAsync(long? id, UpsertThietBiCnttRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        ThietBiCnttEntity entity;
        bool isNew = !id.HasValue;

        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.ThietBiCntts).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("TBCNTT_NOT_FOUND", "Không tìm thấy bản ghi thiết bị CNTT.", 404);
        }
        else
        {
            entity = new ThietBiCnttEntity { ValidFrom = _dateTimeProvider.Now, VersionNo = 1 };
            await _dbContext.ThietBiCntts.AddAsync(entity, cancellationToken);
        }

        await EnsureLoaiThietBiAsync(request, cancellationToken);
        await EnsureUngDungScopeAsync(request.DonViId, request.UngDungIds, cancellationToken);

        // Load existing UngDung links early — reused for SCD2 archive and link removal
        var existingLinks = id.HasValue
            ? await _dbContext.ThietBiUngDungs.Where(x => x.ThietBiId == id.Value).ToListAsync(cancellationToken)
            : new List<ThietBiUngDung>();

        if (!isNew)
        {
            var existingUngDungIds = existingLinks.Select(x => x.HeThongId).ToHashSet();
            var requestUngDungIds = request.UngDungIds.Distinct().ToHashSet();

            bool shouldVersion =
                request.TinhTrang != entity.TinhTrang ||
                request.HeDieuHanh != entity.HeDieuHanh ||
                request.SoLuongHienDung != entity.SoLuongHienDung ||
                request.SoLuongHong != entity.SoLuongHong ||
                !existingUngDungIds.SetEquals(requestUngDungIds);

            if (shouldVersion)
            {
                var now = _dateTimeProvider.Now;

                await _dbContext.ThietBiCnttHis.AddAsync(new ThietBiCnttHis
                {
                    SourceId = entity.Id,
                    DonViId = entity.DonViId,
                    LoaiThietBiId = entity.LoaiThietBiId,
                    TenThietBi = entity.TenThietBi,
                    HangSanXuat = entity.HangSanXuat,
                    Model = entity.Model,
                    CauHinh = entity.CauHinh,
                    HeDieuHanh = entity.HeDieuHanh,
                    DonViSuDung = entity.DonViSuDung,
                    SoLuongTong = entity.SoLuongTong,
                    SoLuongHienDung = entity.SoLuongHienDung,
                    SoLuongHong = entity.SoLuongHong,
                    TinhTrang = entity.TinhTrang,
                    GhiChu = entity.GhiChu,
                    ValidFrom = entity.ValidFrom,
                    ValidTo = now,
                    VersionNo = entity.VersionNo,
                }, cancellationToken);

                foreach (var link in existingLinks)
                {
                    await _dbContext.ThietBiUngDungHis.AddAsync(new ThietBiUngDungHis
                    {
                        SourceThietBiId = link.ThietBiId,
                        SourceHeThongId = link.HeThongId,
                        ValidFrom = entity.ValidFrom,
                        ValidTo = now,
                        VersionNo = entity.VersionNo,
                    }, cancellationToken);
                }

                entity.ValidFrom = now;
                entity.VersionNo++;
            }
        }

        entity.DonViId = request.DonViId;
        entity.LoaiThietBiId = request.LoaiThietBiId;
        entity.TenThietBi = request.TenThietBi;
        entity.HangSanXuat = request.HangSanXuat;
        entity.Model = request.Model;
        entity.CauHinh = request.CauHinh;
        entity.HeDieuHanh = request.HeDieuHanh;
        entity.DonViSuDung = request.DonViSuDung;
        entity.SoLuongTong = request.SoLuongTong;
        entity.SoLuongHienDung = request.SoLuongHienDung;
        entity.SoLuongHong = request.SoLuongHong;
        entity.TinhTrang = request.TinhTrang;
        entity.GhiChu = request.GhiChu;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.ThietBiUngDungs.RemoveRange(existingLinks);

        foreach (var heThongId in request.UngDungIds.Distinct())
        {
            await _dbContext.ThietBiUngDungs.AddAsync(new ThietBiUngDung
            {
                ThietBiId = entity.Id,
                HeThongId = heThongId
            }, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.ThietBiCntts).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("TBCNTT_NOT_FOUND", "Không tìm thấy bản ghi thiết bị CNTT.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<ThietBiCnttEntity> ApplyReadScope(IQueryable<ThietBiCnttEntity> query)
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
            throw new AppException("TBCNTT_SCOPE_DENIED", "Không có quyền thao tác dữ liệu thiết bị CNTT của đơn vị khác.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        }
    }

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);

    private async Task EnsureLoaiThietBiAsync(UpsertThietBiCnttRequest request, CancellationToken cancellationToken)
    {
        var loai = await _dbContext.RefLoaiThietBis
            .Where(x => x.Id == request.LoaiThietBiId && x.IsActive)
            .Select(x => new { x.Id, x.Cap, x.LaTongHop })
            .FirstOrDefaultAsync(cancellationToken);

        if (loai is null || loai.Cap != 2)
        {
            throw new AppException("TBCNTT_LOAI_INVALID", "Loại thiết bị không hợp lệ.", 400);
        }

        if (loai.LaTongHop)
        {
            if (!string.IsNullOrWhiteSpace(request.TenThietBi) || request.UngDungIds.Count > 0)
            {
                throw new AppException("TBCNTT_TONG_HOP_INVALID", "Loại thiết bị tổng hợp không được khai báo thiết bị cụ thể hoặc ứng dụng đang chạy.", 400);
            }
        }
        else if (string.IsNullOrWhiteSpace(request.TenThietBi))
        {
            throw new AppException("TBCNTT_TEN_REQUIRED", "Thiết bị chi tiết phải có tên thiết bị.", 400);
        }
    }

    private async Task EnsureUngDungScopeAsync(long donViId, IReadOnlyCollection<long> ungDungIds, CancellationToken cancellationToken)
    {
        if (ungDungIds.Count == 0)
        {
            return;
        }

        var matchedIds = await _dbContext.HeThongThongTins
            .Where(x => x.DonViId == donViId && ungDungIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (matchedIds.Count != ungDungIds.Distinct().Count())
        {
            throw new AppException("TBCNTT_UNGDUNG_INVALID", "Danh sách ứng dụng đang chạy không hợp lệ với đơn vị được chọn.", 400);
        }
    }
}
