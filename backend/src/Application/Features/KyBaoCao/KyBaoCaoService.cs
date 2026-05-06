using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Entities.Reporting;
using ThucLuc.Domain.Enums;
using KyBaoCaoEntity = ThucLuc.Domain.Entities.Reporting.KyBaoCao;

namespace ThucLuc.Application.Features.KyBaoCao;

public interface IKyBaoCaoService
{
    Task<IReadOnlyCollection<KyBaoCaoDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<KyBaoCaoDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<KyBaoCaoTienDoDto> GetTienDoAsync(long id, CancellationToken cancellationToken = default);

    Task<KyBaoCaoDto?> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<KyBaoCaoDto> CreateAsync(CreateKyBaoCaoRequest request, CancellationToken cancellationToken = default);

    Task<KyBaoCaoDto> UpdateStatusAsync(long id, UpdateKyBaoCaoStatusRequest request, CancellationToken cancellationToken = default);
}

public sealed class KyBaoCaoService : IKyBaoCaoService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateKyBaoCaoRequest> _createValidator;

    public KyBaoCaoService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IValidator<CreateKyBaoCaoRequest> createValidator)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _createValidator = createValidator;
    }

    public async Task<IReadOnlyCollection<KyBaoCaoDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.KyBaoCaos
            .OrderByDescending(x => x.Nam)
            .ThenByDescending(x => x.Quy)
            .Select(ToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<KyBaoCaoDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await _dbContext.KyBaoCaos.Where(x => x.Id == id).Select(ToDto()).FirstOrDefaultAsync(cancellationToken);

    public async Task<KyBaoCaoTienDoDto> GetTienDoAsync(long id, CancellationToken cancellationToken = default)
    {
        var ky = await _dbContext.KyBaoCaos
            .Where(x => x.Id == id)
            .Select(x => new { x.Id, x.KyCode, x.TrangThai })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppException("KY_NOT_FOUND", "Không tìm thấy kỳ báo cáo.", 404);

        var currentUser = _currentUserService.GetCurrentUser();
        var scopedQuery = _dbContext.KyTrangThaiDonVis
            .AsNoTracking()
            .Where(x => x.KyBaoCaoId == id);

        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0)
        {
            scopedQuery = scopedQuery.Where(x => x.DonViId == currentUser.DonViId);
        }

        var donVisRaw = await scopedQuery
            .Select(x => new
            {
                x.DonViId,
                TenDonVi = x.DonVi!.TenDonVi,
                TrangThaiDonVi = x.TrangThai,
                SnapshotId = _dbContext.BaoCaoSnapshots
                    .Where(snapshot => snapshot.KyBaoCaoId == x.KyBaoCaoId && snapshot.DonViId == x.DonViId)
                    .OrderByDescending(snapshot => snapshot.PhienBan)
                    .Select(snapshot => (long?)snapshot.Id)
                    .FirstOrDefault(),
                SnapshotPhienBan = _dbContext.BaoCaoSnapshots
                    .Where(snapshot => snapshot.KyBaoCaoId == x.KyBaoCaoId && snapshot.DonViId == x.DonViId)
                    .OrderByDescending(snapshot => snapshot.PhienBan)
                    .Select(snapshot => (int?)snapshot.PhienBan)
                    .FirstOrDefault(),
                SnapshotTrangThai = _dbContext.BaoCaoSnapshots
                    .Where(snapshot => snapshot.KyBaoCaoId == x.KyBaoCaoId && snapshot.DonViId == x.DonViId)
                    .OrderByDescending(snapshot => snapshot.PhienBan)
                    .Select(snapshot => (SnapshotStatus?)snapshot.TrangThai)
                    .FirstOrDefault(),
                SubmittedAt = _dbContext.BaoCaoSnapshots
                    .Where(snapshot => snapshot.KyBaoCaoId == x.KyBaoCaoId && snapshot.DonViId == x.DonViId)
                    .OrderByDescending(snapshot => snapshot.PhienBan)
                    .Select(snapshot => snapshot.SubmittedAt)
                    .FirstOrDefault(),
                LockedAt = _dbContext.BaoCaoSnapshots
                    .Where(snapshot => snapshot.KyBaoCaoId == x.KyBaoCaoId && snapshot.DonViId == x.DonViId)
                    .OrderByDescending(snapshot => snapshot.PhienBan)
                    .Select(snapshot => snapshot.LockedAt)
                    .FirstOrDefault(),
                x.NgayMoLai,
                x.NgayXacNhan
            })
            .ToListAsync(cancellationToken);

        var donVis = donVisRaw
            .Select(x => new KyBaoCaoTienDoDonViDto
            {
                DonViId = x.DonViId,
                TenDonVi = string.IsNullOrWhiteSpace(x.TenDonVi)
                    ? $"Đơn vị #{x.DonViId}"
                    : x.TenDonVi,
                KyTrangThai = ky.TrangThai,
                TrangThaiDonVi = x.TrangThaiDonVi,
                SnapshotId = x.SnapshotId,
                SnapshotPhienBan = x.SnapshotPhienBan,
                SnapshotTrangThai = x.SnapshotTrangThai,
                SubmittedAt = x.SubmittedAt,
                LockedAt = x.LockedAt,
                NgayMoLai = x.NgayMoLai,
                NgayXacNhan = x.NgayXacNhan
            })
            .OrderBy(x => x.TenDonVi)
            .ToList();

        return new KyBaoCaoTienDoDto
        {
            KyBaoCaoId = ky.Id,
            KyCode = ky.KyCode,
            TongDonVi = donVis.Count,
            SoDonViChuaNhap = donVis.Count(x => x.TrangThaiDonVi == KyTrangThaiDonViStatus.ChuaNhap),
            SoDonViDangNhap = donVis.Count(x => x.TrangThaiDonVi == KyTrangThaiDonViStatus.DangNhap),
            SoDonViDaXacNhan = donVis.Count(x => x.TrangThaiDonVi == KyTrangThaiDonViStatus.DaXacNhan),
            SoDonViDangBoSung = donVis.Count(x => x.TrangThaiDonVi == KyTrangThaiDonViStatus.DangBoSung),
            SoDonViDaNop = donVis.Count(x => x.TrangThaiDonVi == KyTrangThaiDonViStatus.DaNop),
            DonVis = donVis
        };
    }

    public async Task<KyBaoCaoDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
        => await _dbContext.KyBaoCaos.Where(x => x.TrangThai == KyBaoCaoStatus.DangMo).OrderByDescending(x => x.Nam).ThenByDescending(x => x.Quy).Select(ToDto()).FirstOrDefaultAsync(cancellationToken);

    public async Task<KyBaoCaoDto> CreateAsync(CreateKyBaoCaoRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var mauBaoCao = await _dbContext.MauBaoCaos
            .FirstOrDefaultAsync(x => x.Id == request.MauBaoCaoId, cancellationToken)
            ?? throw new AppException("MAU_BAO_CAO_NOT_FOUND", "Không tìm thấy mẫu báo cáo.", 404);

        var (quy, thang, kyCode) = BuildKyMetadata(request, mauBaoCao);

        var exists = await _dbContext.KyBaoCaos.CountAsync(x => x.KyCode == kyCode, cancellationToken) > 0;
        if (exists)
        {
            throw new AppException("DUPLICATE_CODE", "Kỳ báo cáo đã tồn tại.", 409);
        }

        var entity = new KyBaoCaoEntity
        {
            KyCode = kyCode,
            MauBaoCaoId = mauBaoCao.Id,
            Nam = request.Nam,
            Quy = quy,
            Thang = thang,
            NgayBatDau = request.NgayBatDau,
            NgayKetThuc = request.NgayKetThuc,
            GhiChu = request.GhiChu
        };

        await _dbContext.KyBaoCaos.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<KyBaoCaoDto> UpdateStatusAsync(long id, UpdateKyBaoCaoStatusRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.KyBaoCaos.FirstAsync(x => x.Id == id, cancellationToken);
        if (entity.TrangThai == KyBaoCaoStatus.Khoa)
        {
            throw new BusinessRuleException("KY_LOCKED", "Kỳ báo cáo đã khóa, không thể thay đổi trạng thái.");
        }

        if (!CanTransition(entity.TrangThai, request.TrangThai))
        {
            throw new BusinessRuleException("KY_INVALID_TRANSITION", "Không thể chuyển trạng thái kỳ báo cáo theo yêu cầu.");
        }

        entity.TrangThai = request.TrangThai;
        entity.GhiChu = request.GhiChu;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    private static Expression<Func<KyBaoCaoEntity, KyBaoCaoDto>> ToDto() => entity => new KyBaoCaoDto
    {
        Id = entity.Id,
        KyCode = entity.KyCode,
        MauBaoCaoId = entity.MauBaoCaoId,
        Nam = entity.Nam,
        Quy = entity.Quy,
        Thang = entity.Thang,
        TrangThai = entity.TrangThai,
        NgayBatDau = entity.NgayBatDau,
        NgayKetThuc = entity.NgayKetThuc,
        GhiChu = entity.GhiChu
    };

    private static bool CanTransition(KyBaoCaoStatus current, KyBaoCaoStatus next)
    {
        if (current == next)
        {
            return true;
        }

        return current switch
        {
            KyBaoCaoStatus.ChuanBi => next is KyBaoCaoStatus.DangMo or KyBaoCaoStatus.Khoa,
            KyBaoCaoStatus.DangMo => next is KyBaoCaoStatus.DaDong or KyBaoCaoStatus.Khoa,
            KyBaoCaoStatus.DaDong => next is KyBaoCaoStatus.DangMo or KyBaoCaoStatus.Khoa,
            _ => false
        };
    }

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);

    private static (int? Quy, int? Thang, string KyCode) BuildKyMetadata(
        CreateKyBaoCaoRequest request,
        ThucLuc.Domain.Entities.Reporting.MauBaoCao mauBaoCao)
    {
        var maMau = mauBaoCao.MaMau.Trim().ToUpperInvariant();

        return mauBaoCao.TanSuat switch
        {
            TanSuat.Thang => BuildThangKyMetadata(request, maMau),
            TanSuat.Quy => BuildQuyKyMetadata(request, maMau),
            TanSuat.Nam => BuildNamKyMetadata(request, maMau),
            _ => throw new BusinessRuleException("MAU_INVALID_TAN_SUAT", "Tần suất của mẫu báo cáo không hợp lệ.")
        };
    }

    private static (int? Quy, int? Thang, string KyCode) BuildThangKyMetadata(CreateKyBaoCaoRequest request, string prefix)
    {
        if (!request.Thang.HasValue || request.Thang < 1 || request.Thang > 12)
            throw new BusinessRuleException("KY_THANG_REQUIRED", "Mẫu theo tháng yêu cầu nhập tháng từ 1 đến 12.");

        if (request.Quy.HasValue)
            throw new BusinessRuleException("KY_QUY_NOT_ALLOWED", "Mẫu theo tháng không cho phép nhập quý.");

        var thang = request.Thang.Value;
        return (null, thang, $"{request.Nam}T{thang:D2}_{prefix}");
    }

    private static (int? Quy, int? Thang, string KyCode) BuildQuyKyMetadata(CreateKyBaoCaoRequest request, string prefix)
    {
        if (!request.Quy.HasValue || request.Quy < 1 || request.Quy > 4)
            throw new BusinessRuleException("KY_QUY_REQUIRED", "Mẫu theo quý yêu cầu nhập quý từ 1 đến 4.");

        if (request.Thang.HasValue)
            throw new BusinessRuleException("KY_THANG_NOT_ALLOWED", "Mẫu theo quý không cho phép nhập tháng.");

        var quy = request.Quy.Value;
        return (quy, null, $"{request.Nam}Q{quy}_{prefix}");
    }

    private static (int? Quy, int? Thang, string KyCode) BuildNamKyMetadata(CreateKyBaoCaoRequest request, string prefix)
    {
        if (request.Quy.HasValue || request.Thang.HasValue)
            throw new BusinessRuleException("KY_NAM_EXTRA_FIELD", "Mẫu theo năm không cho phép nhập quý hoặc tháng.");

        return (null, null, $"{request.Nam}_{prefix}");
    }
}

public sealed class CreateKyBaoCaoRequestValidator : AbstractValidator<CreateKyBaoCaoRequest>
{
    public CreateKyBaoCaoRequestValidator()
    {
        RuleFor(x => x.MauBaoCaoId).GreaterThan(0);
        RuleFor(x => x.Nam).GreaterThanOrEqualTo(2024);
        RuleFor(x => x.NgayKetThuc)
            .Must((request, ngayKetThuc) => !request.NgayBatDau.HasValue || !ngayKetThuc.HasValue || ngayKetThuc >= request.NgayBatDau)
            .WithMessage("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
        RuleFor(x => x.GhiChu).MaximumLength(2000);
    }
}