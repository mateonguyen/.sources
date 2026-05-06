using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Features.BaoCaoSnapshot;
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

    public YeuCauBoSungService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IBaoCaoSnapshotService baoCaoSnapshotService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _baoCaoSnapshotService = baoCaoSnapshotService;
    }

    public async Task<IReadOnlyCollection<YeuCauBoSungDto>> GetByKyAsync(long kyBaoCaoId, CancellationToken cancellationToken = default)
        => await _dbContext.YeuCauBoSungs
            .Where(x => x.KyBaoCaoId == kyBaoCaoId)
            .OrderByDescending(x => x.RequestedAt)
            .Select(MapToDto())
            .ToListAsync(cancellationToken);

    public async Task<YeuCauBoSungDto> CreateAsync(CreateYeuCauBoSungRequest request, CancellationToken cancellationToken = default)
    {
        var ky = await _dbContext.KyBaoCaos.FirstOrDefaultAsync(x => x.Id == request.KyBaoCaoId, cancellationToken)
            ?? throw new AppException("KY_NOT_FOUND", "Không tìm thấy kỳ báo cáo.", 404);

        if (ky.TrangThai == KyBaoCaoStatus.ChuanBi)
        {
            throw new BusinessRuleException("KY_NOT_OPEN", "Kỳ báo cáo chưa mở, không thể tạo yêu cầu bổ sung.");
        }

        var hasSubmitted = await _dbContext.BaoCaoSnapshots.AnyAsync(
            x => x.KyBaoCaoId == request.KyBaoCaoId && x.DonViId == request.DonViId && x.TrangThai == SnapshotStatus.Locked,
            cancellationToken);

        if (!hasSubmitted)
        {
            throw new BusinessRuleException("SNAPSHOT_NOT_SUBMITTED", "Đơn vị chưa nộp báo cáo cho kỳ này.");
        }

        var existing = await _dbContext.YeuCauBoSungs.AnyAsync(
            x => x.KyBaoCaoId == request.KyBaoCaoId
              && x.DonViId == request.DonViId
              && (x.TrangThai == YeuCauBoSungStatus.ChoDuyet
                  || x.TrangThai == YeuCauBoSungStatus.DaDuyet
                  || x.TrangThai == YeuCauBoSungStatus.DangBoSung),
            cancellationToken);

        if (existing)
        {
            throw new BusinessRuleException("YEU_CAU_DUPLICATE", "Đã có yêu cầu bổ sung đang xử lý cho đơn vị này.");
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var now = _dateTimeProvider.Now;
        var entity = new YeuCauEntity
        {
            KyBaoCaoId = request.KyBaoCaoId,
            DonViId = request.DonViId,
            LyDo = NormalizeRequired(request.LyDo, nameof(request.LyDo)),
            HanBoSung = request.HanBoSung,
            TrangThai = YeuCauBoSungStatus.ChoDuyet,
            RequestedBy = currentUser.UserId,
            RequestedAt = now
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
            .Select(MapToDto())
            .FirstAsync(cancellationToken);

    private static System.Linq.Expressions.Expression<Func<YeuCauEntity, YeuCauBoSungDto>> MapToDto() => x => new YeuCauBoSungDto
    {
        Id = x.Id,
        KyBaoCaoId = x.KyBaoCaoId,
        DonViId = x.DonViId,
        TrangThai = x.TrangThai,
        LyDo = x.LyDo,
        RequestedBy = x.RequestedBy,
        RequestedAt = x.RequestedAt,
        ApprovedBy = x.ApprovedBy,
        ApprovedAt = x.ApprovedAt,
        TuChoiLyDo = x.TuChoiLyDo,
        HanBoSung = x.HanBoSung,
        CompletedAt = x.CompletedAt
    };

    private static string NormalizeRequired(string? value, string fieldName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new BusinessRuleException("YEU_CAU_INVALID", $"{fieldName} là bắt buộc.")
            : value.Trim();
}