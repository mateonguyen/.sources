using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using CameraQuanLyEntity = ThucLuc.Domain.Entities.Business.CameraQuanLy;
using CameraQuanLyHisEntity = ThucLuc.Domain.Entities.Business.CameraQuanLyHis;

namespace ThucLuc.Application.Features.CameraQuanLy;

public interface ICameraQuanLyService
{
    Task<IReadOnlyCollection<CameraQuanLyDto>> GetAllAsync(GetCameraQuanLyQuery query, CancellationToken cancellationToken = default);

    Task<CameraQuanLyDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<CameraQuanLyDto> UpsertAsync(long? id, UpsertCameraQuanLyRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class CameraQuanLyService : ICameraQuanLyService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CameraQuanLyService(IApplicationDbContext dbContext, ICurrentUserService currentUserService, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyCollection<CameraQuanLyDto>> GetAllAsync(GetCameraQuanLyQuery query, CancellationToken cancellationToken = default)
    {
        var normalizedNhomCamera = NormalizeCode(query.NhomCamera);
        if (!string.IsNullOrWhiteSpace(query.KyBaoCaoCode))
        {
            var hisQuery = _dbContext.CameraQuanLyHis.AsNoTracking()
                .Where(x => x.KyBaoCaoCode == query.KyBaoCaoCode)
                .Where(x => ApplyDonViScopePredicate(x.DonViId));

            if (query.DonViId.HasValue)
            {
                hisQuery = hisQuery.Where(x => x.DonViId == query.DonViId.Value);
            }

            if (!string.IsNullOrWhiteSpace(normalizedNhomCamera))
            {
                hisQuery = hisQuery.Where(x => x.NhomCamera == normalizedNhomCamera);
            }

            return await hisQuery.Select(MapHisToDto()).ToListAsync(cancellationToken);
        }

        var liveQuery = ApplyReadScope(_dbContext.CameraQuanLies);
        if (query.DonViId.HasValue)
        {
            liveQuery = liveQuery.Where(x => x.DonViId == query.DonViId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedNhomCamera))
        {
            liveQuery = liveQuery.Where(x => x.NhomCamera == normalizedNhomCamera);
        }

        return await liveQuery.Select(MapLiveToDto()).ToListAsync(cancellationToken);
    }

    public async Task<CameraQuanLyDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await ApplyReadScope(_dbContext.CameraQuanLies)
            .Where(x => x.Id == id)
            .Select(MapLiveToDto())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<CameraQuanLyDto> UpsertAsync(long? id, UpsertCameraQuanLyRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);

        CameraQuanLyEntity entity;
        if (id.HasValue)
        {
            entity = await ApplyReadScope(_dbContext.CameraQuanLies).FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("CAMERA_QL_NOT_FOUND", "Không tìm thấy bản ghi camera quản lý.", 404);
        }
        else
        {
            entity = new CameraQuanLyEntity();
            await _dbContext.CameraQuanLies.AddAsync(entity, cancellationToken);
        }

        entity.DonViId = request.DonViId;
        entity.NhomCamera = NormalizeCode(request.NhomCamera);
        entity.TenDonViDiaChi = NormalizeRequired(request.TenDonViDiaChi, nameof(request.TenDonViDiaChi));
        entity.BuongGiamTrangBiSl = request.BuongGiamTrangBiSl;
        entity.BuongGiamTrangBiTs = request.BuongGiamTrangBiTs;
        entity.NhuCauDauTu = request.NhuCauDauTu;
        entity.BaoTri = request.BaoTri;
        entity.SuaChua = request.SuaChua;
        entity.SoLanViPham = request.SoLanViPham;
        entity.KetNoiChiaSe = request.KetNoiChiaSe;
        entity.HoSoCapDoAttt = request.HoSoCapDoAttt;
        entity.CbChuyenTrach = request.CbChuyenTrach;
        entity.CbKiemNhiem = request.CbKiemNhiem;
        entity.CbDiaPhuong = request.CbDiaPhuong;
        entity.DaoTaoBo = request.DaoTaoBo;
        entity.DaoTaoNhuCau = request.DaoTaoNhuCau;
        entity.GhiChu = request.GhiChu;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.CameraQuanLies).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("CAMERA_QL_NOT_FOUND", "Không tìm thấy bản ghi camera quản lý.", 404);
        entity.DeletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<CameraQuanLyEntity> ApplyReadScope(IQueryable<CameraQuanLyEntity> query)
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
            throw new AppException("CAMERA_QL_SCOPE_DENIED", "Không có quyền thao tác dữ liệu camera quản lý của đơn vị khác.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        }
    }

    private static string? NormalizeCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string NormalizeRequired(string? value, string fieldName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new AppException("CAMERA_QL_REQUIRED", $"{fieldName} là bắt buộc.", 400)
            : value.Trim();

    private bool ApplyDonViScopePredicate(long donViId)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        return HasCrossDonViPermission(currentUser) || currentUser.DonViId <= 0 || currentUser.DonViId == donViId;
    }

    private static System.Linq.Expressions.Expression<Func<CameraQuanLyEntity, CameraQuanLyDto>> MapLiveToDto()
        => x => new CameraQuanLyDto
        {
            Id = x.Id,
            DonViId = x.DonViId,
            KyBaoCaoCode = null,
            NhomCamera = x.NhomCamera,
            TenDonViDiaChi = x.TenDonViDiaChi,
            BuongGiamTrangBiSl = x.BuongGiamTrangBiSl,
            BuongGiamTrangBiTs = x.BuongGiamTrangBiTs,
            NhuCauDauTu = x.NhuCauDauTu,
            BaoTri = x.BaoTri,
            SuaChua = x.SuaChua,
            SoLanViPham = x.SoLanViPham,
            KetNoiChiaSe = x.KetNoiChiaSe,
            HoSoCapDoAttt = x.HoSoCapDoAttt,
            CbChuyenTrach = x.CbChuyenTrach,
            CbKiemNhiem = x.CbKiemNhiem,
            CbDiaPhuong = x.CbDiaPhuong,
            DaoTaoBo = x.DaoTaoBo,
            DaoTaoNhuCau = x.DaoTaoNhuCau,
            GhiChu = x.GhiChu
        };

    private static System.Linq.Expressions.Expression<Func<CameraQuanLyHisEntity, CameraQuanLyDto>> MapHisToDto()
        => x => new CameraQuanLyDto
        {
            Id = x.SourceId,
            DonViId = x.DonViId,
            KyBaoCaoCode = x.KyBaoCaoCode,
            NhomCamera = x.NhomCamera,
            TenDonViDiaChi = x.TenDonViDiaChi,
            BuongGiamTrangBiSl = x.BuongGiamTrangBiSl,
            BuongGiamTrangBiTs = x.BuongGiamTrangBiTs,
            NhuCauDauTu = x.NhuCauDauTu,
            BaoTri = x.BaoTri,
            SuaChua = x.SuaChua,
            SoLanViPham = x.SoLanViPham,
            KetNoiChiaSe = x.KetNoiChiaSe,
            HoSoCapDoAttt = x.HoSoCapDoAttt,
            CbChuyenTrach = x.CbChuyenTrach,
            CbKiemNhiem = x.CbKiemNhiem,
            CbDiaPhuong = x.CbDiaPhuong,
            DaoTaoBo = x.DaoTaoBo,
            DaoTaoNhuCau = x.DaoTaoNhuCau,
            GhiChu = x.GhiChu
        };

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}
