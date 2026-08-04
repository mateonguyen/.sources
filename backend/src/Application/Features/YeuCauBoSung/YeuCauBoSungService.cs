using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Features.BaoCaoSnapshot;
using ThucLuc.Application.Features.DonVi;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Enums;
using YeuCauEntity = ThucLuc.Domain.Entities.System.YeuCauBoSung;

namespace ThucLuc.Application.Features.YeuCauBoSung;

public interface IYeuCauBoSungService
{
    Task<IReadOnlyCollection<YeuCauBoSungDto>> GetByKyAsync(long kyBaoCaoId, CancellationToken cancellationToken = default);
    Task<YeuCauBoSungDto> CreateAsync(CreateYeuCauBoSungRequest request, CancellationToken cancellationToken = default);
    Task<YeuCauBoSungDto> DuyetAsync(long id, DuyetYeuCauRequest request, CancellationToken cancellationToken = default);
    Task<YeuCauBoSungDto> TuChoiAsync(long id, TuChoiYeuCauRequest request, CancellationToken cancellationToken = default);
    Task HoanThanhAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class YeuCauBoSungService : IYeuCauBoSungService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBaoCaoSnapshotService _baoCaoSnapshotService;
    private readonly IDonViDataScopeService _donViDataScopeService;
    private readonly IDonViInputModeService _donViInputModeService;

    public YeuCauBoSungService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IBaoCaoSnapshotService baoCaoSnapshotService,
        IDonViDataScopeService donViDataScopeService,
        IDonViInputModeService donViInputModeService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _baoCaoSnapshotService = baoCaoSnapshotService;
        _donViDataScopeService = donViDataScopeService;
        _donViInputModeService = donViInputModeService;
    }

    public async Task<IReadOnlyCollection<YeuCauBoSungDto>> GetByKyAsync(long kyBaoCaoId, CancellationToken cancellationToken = default)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);

        var query = _dbContext.YeuCauBoSungs.Where(x => x.KyBaoCaoId == kyBaoCaoId);
        if (!scope.HasFullAccess)
        {
            var allowedIds = scope.AllowedDonViIds;
            query = query.Where(x => allowedIds.Contains(x.DonViId));
        }

        return await query
            .OrderByDescending(x => x.RequestedAt)
            .Select(x => new YeuCauBoSungDto
            {
                Id = x.Id,
                KyBaoCaoId = x.KyBaoCaoId,
                DonViId = x.DonViId,
                TenDonVi = _dbContext.DonVis.Where(d => d.Id == x.DonViId).Select(d => d.TenDonVi).FirstOrDefault() ?? string.Empty,
                TrangThai = x.TrangThai,
                LyDo = x.LyDo,
                RequestedBy = x.RequestedBy,
                RequestedAt = x.RequestedAt,
                ApprovedBy = x.ApprovedBy,
                ApprovedAt = x.ApprovedAt,
                TuChoiLyDo = x.TuChoiLyDo,
                HanBoSung = x.HanBoSung,
                CompletedAt = x.CompletedAt,
                CapGui = x.CapGui
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<YeuCauBoSungDto> CreateAsync(CreateYeuCauBoSungRequest request, CancellationToken cancellationToken = default)
    {
        var ky = await _dbContext.KyBaoCaos.FirstOrDefaultAsync(x => x.Id == request.KyBaoCaoId, cancellationToken)
            ?? throw new AppException("KY_NOT_FOUND", "Không tìm thấy kỳ báo cáo.", 404);

        if (ky.TrangThai == KyBaoCaoStatus.ChuanBi)
        {
            throw new BusinessRuleException("KY_NOT_OPEN", "Kỳ báo cáo chưa mở, không thể tạo yêu cầu bổ sung.");
        }

        var hasSubmitted = await _dbContext.BaoCaoSnapshots.CountAsync(
            x => x.KyBaoCaoId == request.KyBaoCaoId && x.DonViId == request.DonViId && x.TrangThai == SnapshotStatus.Locked,
            cancellationToken) > 0;

        if (!hasSubmitted)
        {
            throw new BusinessRuleException("SNAPSHOT_NOT_SUBMITTED", "Đơn vị chưa nộp báo cáo cho kỳ này.");
        }

        var existing = await _dbContext.YeuCauBoSungs.CountAsync(
            x => x.KyBaoCaoId == request.KyBaoCaoId
              && x.DonViId == request.DonViId
              && (x.TrangThai == YeuCauBoSungStatus.ChoDuyet
                  || x.TrangThai == YeuCauBoSungStatus.DaDuyet
                  || x.TrangThai == YeuCauBoSungStatus.DangBoSung),
            cancellationToken) > 0;

        if (existing)
        {
            throw new BusinessRuleException("YEU_CAU_DUPLICATE", "Đã có yêu cầu bổ sung đang xử lý cho đơn vị này.");
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var now = _dateTimeProvider.Now;
        var capGui = await ResolveCapGuiAsync(currentUser.DonViId, request.DonViId, cancellationToken);
        var entity = new YeuCauEntity
        {
            KyBaoCaoId = request.KyBaoCaoId,
            DonViId = request.DonViId,
            LyDo = NormalizeRequired(request.LyDo, nameof(request.LyDo)),
            HanBoSung = request.HanBoSung,
            TrangThai = YeuCauBoSungStatus.ChoDuyet,
            RequestedBy = currentUser.UserId,
            RequestedAt = now,
            CapGui = capGui
        };

        await _dbContext.YeuCauBoSungs.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<YeuCauBoSungDto> DuyetAsync(long id, DuyetYeuCauRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.YeuCauBoSungs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("YEU_CAU_NOT_FOUND", "Không tìm thấy yêu cầu bổ sung.", 404);

        if (entity.TrangThai != YeuCauBoSungStatus.ChoDuyet)
        {
            throw new BusinessRuleException("YEU_CAU_INVALID_STATUS", "Yêu cầu không ở trạng thái chờ duyệt.");
        }

        await _baoCaoSnapshotService.MoLaiAsync(entity.KyBaoCaoId, entity.DonViId, cancellationToken);

        var currentUser = _currentUserService.GetCurrentUser();
        var now = _dateTimeProvider.Now;
        entity.TrangThai = YeuCauBoSungStatus.DangBoSung;
        entity.ApprovedBy = currentUser.UserId;
        entity.ApprovedAt = now;
        if (request.HanBoSung.HasValue)
        {
            entity.HanBoSung = request.HanBoSung;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<YeuCauBoSungDto> TuChoiAsync(long id, TuChoiYeuCauRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.YeuCauBoSungs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("YEU_CAU_NOT_FOUND", "Không tìm thấy yêu cầu bổ sung.", 404);

        if (entity.TrangThai != YeuCauBoSungStatus.ChoDuyet)
        {
            throw new BusinessRuleException("YEU_CAU_INVALID_STATUS", "Yêu cầu không ở trạng thái chờ duyệt.");
        }

        var currentUser = _currentUserService.GetCurrentUser();
        entity.TrangThai = YeuCauBoSungStatus.TuChoi;
        entity.ApprovedBy = currentUser.UserId;
        entity.ApprovedAt = _dateTimeProvider.Now;
        entity.TuChoiLyDo = NormalizeRequired(request.TuChoiLyDo, nameof(request.TuChoiLyDo));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task HoanThanhAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.YeuCauBoSungs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null || entity.TrangThai != YeuCauBoSungStatus.DangBoSung)
        {
            return;
        }

        entity.TrangThai = YeuCauBoSungStatus.HoanThanh;
        entity.CompletedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<YeuCauBoSungDto> GetByIdAsync(long id, CancellationToken cancellationToken)
        => await _dbContext.YeuCauBoSungs
            .Where(x => x.Id == id)
            .Select(x => new YeuCauBoSungDto
            {
                Id = x.Id,
                KyBaoCaoId = x.KyBaoCaoId,
                DonViId = x.DonViId,
                TenDonVi = _dbContext.DonVis.Where(d => d.Id == x.DonViId).Select(d => d.TenDonVi).FirstOrDefault() ?? string.Empty,
                TrangThai = x.TrangThai,
                LyDo = x.LyDo,
                RequestedBy = x.RequestedBy,
                RequestedAt = x.RequestedAt,
                ApprovedBy = x.ApprovedBy,
                ApprovedAt = x.ApprovedAt,
                TuChoiLyDo = x.TuChoiLyDo,
                HanBoSung = x.HanBoSung,
                CompletedAt = x.CompletedAt,
                CapGui = x.CapGui
            })
            .FirstAsync(cancellationToken);

    private async Task<string> ResolveCapGuiAsync(long requesterDonViId, long targetDonViId, CancellationToken cancellationToken)
    {
        if (requesterDonViId <= 0 || requesterDonViId == targetDonViId)
        {
            return "BO_XUONG_TINH";
        }

        var modeContext = await _donViInputModeService.GetContextAsync(requesterDonViId, cancellationToken);
        if (!modeContext.IsTongHop)
        {
            return "BO_XUONG_TINH";
        }

        return modeContext.DescendantDonViIds.Contains(targetDonViId)
            ? "TINH_XUONG_PHONG"
            : "BO_XUONG_TINH";
    }

    private static string NormalizeRequired(string? value, string fieldName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new BusinessRuleException("YEU_CAU_INVALID", $"{fieldName} là bắt buộc.")
            : value.Trim();
}