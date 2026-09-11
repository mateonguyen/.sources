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
    private static readonly HashSet<string> LoaiHaTangCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BCANET", "INTERNET", "KHAC"
    };

    private static readonly HashSet<string> CapDoCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CAP_1", "CAP_2", "CAP_3", "CAP_4", "CAP_5"
    };

    private static readonly HashSet<string> TinhTrangPheDuyetCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CHUA_PHE_DUYET", "DANG_XU_LY", "DA_PHE_DUYET"
    };

    private static readonly HashSet<string> TrangThaiTrienKhaiCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CHUA_TRIEN_KHAI", "DANG_TRIEN_KHAI", "DA_TRIEN_KHAI_DAY_DU"
    };

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

        var rows = await liveQuery.Select(MapLiveToDto()).ToListAsync(cancellationToken);
        await HydrateLinkedUnitsAsync(rows, cancellationToken);
        return rows;
    }

    public async Task<AtttHtttVanHanhDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var row = await ApplyReadScope(_dbContext.AtttHtttVanHanhs)
            .Where(x => x.Id == id)
            .Select(MapLiveToDto())
            .FirstOrDefaultAsync(cancellationToken);
        if (row is not null)
        {
            await HydrateLinkedUnitsAsync(new[] { row }, cancellationToken);
        }

        return row;
    }

    public async Task<AtttHtttVanHanhDto> UpsertAsync(long? id, UpsertAtttHtttVanHanhRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureValidScopeAsync(request.DonViId, cancellationToken);
        var linkedUnits = await ValidateRequestAsync(id, request, cancellationToken);

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
        entity.LoaiHaTang = NormalizeOptional(request.LoaiHaTang)?.ToUpperInvariant();
        entity.ChuQuan = linkedUnits.ChuQuan;
        entity.DonViVanHanh = linkedUnits.DonViVanHanh;
        entity.CapDoDeXuat = NormalizeOptional(request.CapDoDeXuat)?.ToUpperInvariant();
        entity.TinhTrangPheDuyet = NormalizeOptional(request.TinhTrangPheDuyet)?.ToUpperInvariant();
        entity.QuyetDinhPheDuyet = NormalizeOptional(request.QuyetDinhPheDuyet);
        entity.QuyCheAttt = NormalizeOptional(request.QuyCheAttt);
        entity.DuKienNgayPheDuyet = request.DuKienNgayPheDuyet;
        var trangThaiTrienKhai = NormalizeOptional(request.TrangThaiTrienKhaiPhuongAn)?.ToUpperInvariant()
            ?? (request.DaTrienKhaiPhuongAn ? "DA_TRIEN_KHAI_DAY_DU" : "CHUA_TRIEN_KHAI");
        entity.TrangThaiTrienKhaiPhuongAn = trangThaiTrienKhai;
        entity.DaTrienKhaiPhuongAn = trangThaiTrienKhai == "DA_TRIEN_KHAI_DAY_DU";
        entity.NoiDungPhuongAnDaTrienKhai = NormalizeOptional(request.NoiDungPhuongAnDaTrienKhai);
        entity.DuKienNgayTrienKhai = request.DuKienNgayTrienKhai;
        entity.KiemTraDanhGia = NormalizeOptional(request.KiemTraDanhGia);
        entity.GhiChu = NormalizeOptional(request.GhiChu);

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

    private async Task<(string ChuQuan, string DonViVanHanh)> ValidateRequestAsync(
        long? id,
        UpsertAtttHtttVanHanhRequest request,
        CancellationToken cancellationToken)
    {
        if (request.HtttId <= 0)
        {
            throw new AppException("ATTTVH_HTTT_REQUIRED", "Bạn phải chọn hệ thống thông tin.", 422);
        }

        var loaiHaTang = NormalizeOptional(request.LoaiHaTang)?.ToUpperInvariant();
        var capDo = NormalizeOptional(request.CapDoDeXuat)?.ToUpperInvariant();
        var tinhTrangPheDuyet = NormalizeOptional(request.TinhTrangPheDuyet)?.ToUpperInvariant();
        var trangThaiTrienKhai = NormalizeOptional(request.TrangThaiTrienKhaiPhuongAn)?.ToUpperInvariant()
            ?? (request.DaTrienKhaiPhuongAn ? "DA_TRIEN_KHAI_DAY_DU" : "CHUA_TRIEN_KHAI");

        if (loaiHaTang is null || !LoaiHaTangCodes.Contains(loaiHaTang))
        {
            throw new AppException("ATTTVH_LOAI_HA_TANG_INVALID", "Loại hạ tầng không hợp lệ.", 422);
        }

        if (capDo is null || !CapDoCodes.Contains(capDo))
        {
            throw new AppException("ATTTVH_CAP_DO_INVALID", "Bạn phải chọn cấp độ đề xuất hợp lệ.", 422);
        }

        if (tinhTrangPheDuyet is null || !TinhTrangPheDuyetCodes.Contains(tinhTrangPheDuyet))
        {
            throw new AppException("ATTTVH_PHE_DUYET_INVALID", "Bạn phải chọn tình trạng phê duyệt.", 422);
        }

        if (tinhTrangPheDuyet == "DA_PHE_DUYET" && string.IsNullOrWhiteSpace(request.QuyetDinhPheDuyet))
        {
            throw new AppException("ATTTVH_QUYET_DINH_REQUIRED", "HTTT đã phê duyệt phải có thông tin quyết định phê duyệt.", 422);
        }

        if (tinhTrangPheDuyet != "DA_PHE_DUYET" && request.DuKienNgayPheDuyet is null)
        {
            throw new AppException("ATTTVH_NGAY_PHE_DUYET_REQUIRED", "HTTT chưa phê duyệt phải có thời điểm dự kiến phê duyệt.", 422);
        }

        if (!TrangThaiTrienKhaiCodes.Contains(trangThaiTrienKhai))
        {
            throw new AppException("ATTTVH_TRIEN_KHAI_INVALID", "Trạng thái triển khai phương án ATTT không hợp lệ.", 422);
        }

        if (trangThaiTrienKhai != "CHUA_TRIEN_KHAI" && string.IsNullOrWhiteSpace(request.NoiDungPhuongAnDaTrienKhai))
        {
            throw new AppException("ATTTVH_PHUONG_AN_REQUIRED", "Hãy mô tả các giải pháp hoặc phương án ATTT đã triển khai.", 422);
        }

        if (trangThaiTrienKhai != "DA_TRIEN_KHAI_DAY_DU" && request.DuKienNgayTrienKhai is null)
        {
            throw new AppException("ATTTVH_NGAY_TRIEN_KHAI_REQUIRED", "HTTT chưa triển khai đầy đủ phải có thời điểm dự kiến hoàn thành.", 422);
        }

        ValidateLength(request.QuyetDinhPheDuyet, 200, "Quyết định phê duyệt");
        ValidateLength(request.QuyCheAttt, 200, "Quy chế ATTT");
        ValidateLength(request.NoiDungPhuongAnDaTrienKhai, 2000, "Nội dung phương án đã triển khai");
        ValidateLength(request.KiemTraDanhGia, 500, "Kiểm tra, đánh giá ATTT");
        ValidateLength(request.GhiChu, 2000, "Ghi chú");

        var httt = await _dbContext.HeThongThongTins
            .Where(x => x.Id == request.HtttId && x.DonViId == request.DonViId)
            .Select(x => new { x.DonViId, x.DonViQuanLy })
            .FirstOrDefaultAsync(cancellationToken);
        if (httt is null)
        {
            throw new AppException("ATTTVH_HTTT_NOT_FOUND", "Hệ thống thông tin không thuộc đơn vị đang nhập dữ liệu.", 422);
        }

        var chuQuan = await _dbContext.DonVis
            .Where(x => x.Id == httt.DonViId)
            .Select(x => x.TenDonVi)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(chuQuan) || string.IsNullOrWhiteSpace(httt.DonViQuanLy))
        {
            throw new AppException(
                "ATTTVH_HTTT_UNIT_MISSING",
                "HTTT chưa có đủ đơn vị chủ quản hoặc đơn vị quản lý/sử dụng. Hãy cập nhật tại module Hệ thống thông tin trước.",
                422);
        }

        var duplicateExists = await _dbContext.AtttHtttVanHanhs.AnyAsync(
            x => x.DonViId == request.DonViId && x.HtttId == request.HtttId && (!id.HasValue || x.Id != id.Value),
            cancellationToken);
        if (duplicateExists)
        {
            throw new AppException("ATTTVH_DUPLICATE", "Hệ thống thông tin này đã được khai báo trong danh sách vận hành.", 409);
        }

        return (chuQuan.Trim(), httt.DonViQuanLy.Trim());
    }

    private static void ValidateLength(string? value, int maxLength, string fieldName)
    {
        if (value?.Trim().Length > maxLength)
        {
            throw new AppException("ATTTVH_FIELD_TOO_LONG", $"{fieldName} không được vượt quá {maxLength} ký tự.", 422);
        }
    }

    private async Task HydrateLinkedUnitsAsync(
        IReadOnlyCollection<AtttHtttVanHanhDto> rows,
        CancellationToken cancellationToken)
    {
        var htttIds = rows.Select(x => x.HtttId).Distinct().ToArray();
        if (htttIds.Length == 0)
        {
            return;
        }

        var linkedUnits = await (
            from httt in _dbContext.HeThongThongTins.AsNoTracking()
            join donVi in _dbContext.DonVis.AsNoTracking() on httt.DonViId equals donVi.Id
            where htttIds.Contains(httt.Id)
            select new { httt.Id, ChuQuan = donVi.TenDonVi, DonViVanHanh = httt.DonViQuanLy })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var row in rows)
        {
            if (!linkedUnits.TryGetValue(row.HtttId, out var linked))
            {
                continue;
            }

            row.ChuQuan = linked.ChuQuan;
            row.DonViVanHanh = linked.DonViVanHanh;
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
            LoaiHaTang = x.LoaiHaTang,
            ChuQuan = x.ChuQuan,
            DonViVanHanh = x.DonViVanHanh,
            CapDoDeXuat = x.CapDoDeXuat,
            TinhTrangPheDuyet = x.TinhTrangPheDuyet,
            QuyetDinhPheDuyet = x.QuyetDinhPheDuyet,
            QuyCheAttt = x.QuyCheAttt,
            DuKienNgayPheDuyet = x.DuKienNgayPheDuyet,
            DaTrienKhaiPhuongAn = x.DaTrienKhaiPhuongAn,
            TrangThaiTrienKhaiPhuongAn = x.TrangThaiTrienKhaiPhuongAn,
            NoiDungPhuongAnDaTrienKhai = x.NoiDungPhuongAnDaTrienKhai,
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
            LoaiHaTang = x.LoaiHaTang,
            ChuQuan = x.ChuQuan,
            DonViVanHanh = x.DonViVanHanh,
            CapDoDeXuat = x.CapDoDeXuat,
            TinhTrangPheDuyet = x.TinhTrangPheDuyet,
            QuyetDinhPheDuyet = x.QuyetDinhPheDuyet,
            QuyCheAttt = x.QuyCheAttt,
            DuKienNgayPheDuyet = x.DuKienNgayPheDuyet,
            DaTrienKhaiPhuongAn = x.DaTrienKhaiPhuongAn,
            TrangThaiTrienKhaiPhuongAn = x.TrangThaiTrienKhaiPhuongAn,
            NoiDungPhuongAnDaTrienKhai = x.NoiDungPhuongAnDaTrienKhai,
            DuKienNgayTrienKhai = x.DuKienNgayTrienKhai,
            KiemTraDanhGia = x.KiemTraDanhGia,
            GhiChu = x.GhiChu
        };

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}
