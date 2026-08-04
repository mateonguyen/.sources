using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using FluentValidation;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Features.DonVi;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Entities.Business;
using ThucLuc.Domain.Entities.Reporting;
using BaoCaoSnapshotEntity = ThucLuc.Domain.Entities.Reporting.BaoCaoSnapshot;
using BaoCaoSnapshotXacNhanEntity = ThucLuc.Domain.Entities.Reporting.BaoCaoSnapshotXacNhan;
using BaoCaoFileEntity = ThucLuc.Domain.Entities.Reporting.BaoCaoFile;
using SnapshotBatchEntity = ThucLuc.Domain.Entities.Reporting.SnapshotBatch;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Application.Features.BaoCaoSnapshot;

public interface IBaoCaoSnapshotService
{
    Task<IReadOnlyCollection<DaoTaoBoiDuongPreviewItem>> PreviewDaoTaoAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default);
    Task<string> BuildSnapshotJsonAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default);
    Task<BaoCaoSnapshotDto?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<BaoCaoSnapshotDto>> GetByKyAsync(long kyBaoCaoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<BaoCaoSnapshotDto>> GetLatestByDonViAsync(long? kyBaoCaoId, CancellationToken cancellationToken = default);
    Task<SnapshotCompareDto> CompareTwoKyAsync(long donViId, long fromKyBaoCaoId, long toKyBaoCaoId, CancellationToken cancellationToken = default);
    Task<BaoCaoSnapshotDto> CreateDraftAsync(CreateBaoCaoSnapshotRequest request, CancellationToken cancellationToken = default);
    Task<BaoCaoSnapshotDto> UpdateDraftAsync(long id, UpdateBaoCaoSnapshotRequest request, CancellationToken cancellationToken = default);
    Task<BaoCaoSnapshotDto> SubmitCurrentAsync(SubmitCurrentSnapshotRequest request, CancellationToken cancellationToken = default);
    Task<BaoCaoSnapshotDto> MoLaiAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default);
    Task<BaoCaoSnapshotDto> SubmitAsync(long id, SubmitBaoCaoSnapshotRequest request, CancellationToken cancellationToken = default);
    Task<SnapshotBreakdownDto> GetBreakdownAsync(long id, CancellationToken cancellationToken = default);
    Task<FinalizeBizModuleResult> FinalizeBizModuleAsync(FinalizeBizModuleRequest request, CancellationToken cancellationToken = default);
    Task CancelAsync(long id, CancellationToken cancellationToken = default);
    Task<BaoCaoPdfResultDto> GeneratePdfAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ModuleStatusDto>> GetModuleStatusAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default);

    Task<SubmitSnapshotContextDto> GetSubmitContextAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default);
}

public sealed class BaoCaoSnapshotService : IBaoCaoSnapshotService
{
    private const int MaxGeneratedPdfBytes = 10485760;

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IPdfService _pdfService;
    private readonly IAuditLogService _auditLogService;
    private readonly IValidator<CreateBaoCaoSnapshotRequest> _createValidator;
    private readonly IValidator<UpdateBaoCaoSnapshotRequest> _updateValidator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<BaoCaoSnapshotService> _logger;
    private readonly IDonViDataScopeService _donViDataScopeService;
    private readonly IDonViInputModeService _donViInputModeService;

    public BaoCaoSnapshotService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        IPdfService pdfService,
        IAuditLogService auditLogService,
        IValidator<CreateBaoCaoSnapshotRequest> createValidator,
        IValidator<UpdateBaoCaoSnapshotRequest> updateValidator,
        IDateTimeProvider dateTimeProvider,
        ILogger<BaoCaoSnapshotService> logger,
        IDonViDataScopeService donViDataScopeService,
        IDonViInputModeService donViInputModeService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _pdfService = pdfService;
        _auditLogService = auditLogService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _donViDataScopeService = donViDataScopeService;
        _donViInputModeService = donViInputModeService;
    }


    public async Task<IReadOnlyCollection<DaoTaoBoiDuongPreviewItem>> PreviewDaoTaoAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);
        if (!scope.Contains(donViId))
        {
            throw new AppException("SNAPSHOT_SCOPE_DENIED", "Không có quyền xem dữ liệu đơn vị ngoài phạm vi.", 403);
        }

        var ky = await _dbContext.KyBaoCaos.FirstAsync(x => x.Id == kyBaoCaoId, cancellationToken);
        var all = await _dbContext.DaoTaoBoiDuongs
            .Where(x => x.DonViId == donViId)
            .Select(x => new DaoTaoBoiDuongPreviewItem
            {
                Id = x.Id,
                TenKhoaHoc = x.TenKhoaHoc,
                DonViToChuc = x.DonViToChuc,
                HinhThuc = x.HinhThuc,
                SoLuongHv = x.SoLuongHv,
                ThoiGianTu = x.ThoiGianTu,
                ThoiGianDen = x.ThoiGianDen,
                GhiChu = x.GhiChu
            })
            .ToListAsync(cancellationToken);
        if (ky.NgayBatDau.HasValue && ky.NgayKetThuc.HasValue)
        {
            var from = ky.NgayBatDau.Value;
            var to = ky.NgayKetThuc.Value;
            foreach (var item in all)
            {
                if (item.ThoiGianTu.HasValue)
                {
                    if (item.ThoiGianTu.Value >= from && item.ThoiGianTu.Value <= to)
                        item.Flag = "in_range";
                    else
                        item.Flag = "out_of_range";
                }
                else
                {
                    item.Flag = "no_date";
                }
            }
        }
        else
        {
            foreach (var item in all)
            {
                item.Flag = item.ThoiGianTu.HasValue ? "in_range" : "no_date";
            }
        }
        return all;
    }


    public async Task<BaoCaoSnapshotDto?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);

        var query = _dbContext.BaoCaoSnapshots.Where(x => x.Id == id);
        if (!scope.HasFullAccess)
        {
            var allowedIds = scope.AllowedDonViIds;
            query = query.Where(x => allowedIds.Contains(x.DonViId));
        }

        return await query
            .Select(ToDto())
            .FirstOrDefaultAsync(cancellationToken);
    }


    public async Task<IReadOnlyCollection<BaoCaoSnapshotDto>> GetByKyAsync(long kyBaoCaoId, CancellationToken cancellationToken = default)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);

        var query = _dbContext.BaoCaoSnapshots.Where(x => x.KyBaoCaoId == kyBaoCaoId);
        if (!scope.HasFullAccess)
        {
            var allowedIds = scope.AllowedDonViIds;
            query = query.Where(x => allowedIds.Contains(x.DonViId));
        }

        return await query
            .OrderByDescending(x => x.PhienBan)
            .Select(ToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<BaoCaoSnapshotDto>> GetLatestByDonViAsync(long? kyBaoCaoId, CancellationToken cancellationToken = default)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);

        var query = _dbContext.BaoCaoSnapshots
            .Where(x => x.SubmittedAt != null && (x.TrangThai == SnapshotStatus.Submitted || x.TrangThai == SnapshotStatus.Locked));

        if (kyBaoCaoId.HasValue)
        {
            query = query.Where(x => x.KyBaoCaoId == kyBaoCaoId.Value);
        }

        if (!scope.HasFullAccess)
        {
            var allowedIds = scope.AllowedDonViIds;
            query = query.Where(x => allowedIds.Contains(x.DonViId));
        }

        var snapshots = await query
            .OrderByDescending(x => x.SubmittedAt)
            .ThenByDescending(x => x.PhienBan)
            .Select(ToDto())
            .ToListAsync(cancellationToken);

        return snapshots
            .GroupBy(x => x.DonViId)
            .Select(group => group.First())
            .OrderBy(x => x.TenDonVi)
            .ToList();
    }

    public async Task<SnapshotCompareDto> CompareTwoKyAsync(long donViId, long fromKyBaoCaoId, long toKyBaoCaoId, CancellationToken cancellationToken = default)
    {
        if (fromKyBaoCaoId == toKyBaoCaoId)
        {
            throw new AppException("SNAPSHOT_COMPARE_INVALID", "Hai kỳ báo cáo so sánh phải khác nhau.", 400);
        }

        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);
        if (!scope.Contains(donViId))
        {
            throw new AppException("SNAPSHOT_SCOPE_DENIED", "Không có quyền xem dữ liệu đơn vị ngoài phạm vi.", 403);
        }

        var fromSnapshotId = await _dbContext.BaoCaoSnapshots
            .AsNoTracking()
            .Where(x => x.DonViId == donViId
                && x.KyBaoCaoId == fromKyBaoCaoId
                && x.SubmittedAt != null
                && (x.TrangThai == SnapshotStatus.Submitted || x.TrangThai == SnapshotStatus.Locked))
            .OrderByDescending(x => x.SubmittedAt)
            .ThenByDescending(x => x.PhienBan)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var toSnapshotId = await _dbContext.BaoCaoSnapshots
            .AsNoTracking()
            .Where(x => x.DonViId == donViId
                && x.KyBaoCaoId == toKyBaoCaoId
                && x.SubmittedAt != null
                && (x.TrangThai == SnapshotStatus.Submitted || x.TrangThai == SnapshotStatus.Locked))
            .OrderByDescending(x => x.SubmittedAt)
            .ThenByDescending(x => x.PhienBan)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!fromSnapshotId.HasValue)
        {
            throw new AppException("SNAPSHOT_COMPARE_FROM_NOT_FOUND", "Không tìm thấy snapshot đã nộp của kỳ nguồn.", 404);
        }

        if (!toSnapshotId.HasValue)
        {
            throw new AppException("SNAPSHOT_COMPARE_TO_NOT_FOUND", "Không tìm thấy snapshot đã nộp của kỳ đích.", 404);
        }

        var fromSnapshot = await LoadSnapshotAsync(fromSnapshotId.Value, cancellationToken);
        var toSnapshot = await LoadSnapshotAsync(toSnapshotId.Value, cancellationToken);

        var fromTotals = await BuildSnapshotModuleTotalsAsync(fromSnapshot, cancellationToken);
        var toTotals = await BuildSnapshotModuleTotalsAsync(toSnapshot, cancellationToken);

        var moduleCodes = fromTotals.Keys
            .Union(toTotals.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var modules = moduleCodes
            .Select(code =>
            {
                fromTotals.TryGetValue(code, out var fromCount);
                toTotals.TryGetValue(code, out var toCount);
                return new SnapshotModuleCompareItemDto
                {
                    ModuleCode = code,
                    FromCount = fromCount,
                    ToCount = toCount,
                    Delta = toCount - fromCount,
                };
            })
            .ToList();

        return new SnapshotCompareDto
        {
            DonViId = donViId,
            TenDonVi = toSnapshot.DonVi?.TenDonVi ?? fromSnapshot.DonVi?.TenDonVi ?? string.Empty,
            FromKyBaoCaoId = fromSnapshot.KyBaoCaoId,
            FromKyCode = fromSnapshot.KyBaoCao?.KyCode ?? string.Empty,
            FromSnapshotId = fromSnapshot.Id,
            ToKyBaoCaoId = toSnapshot.KyBaoCaoId,
            ToKyCode = toSnapshot.KyBaoCao?.KyCode ?? string.Empty,
            ToSnapshotId = toSnapshot.Id,
            Modules = modules,
        };
    }

    public async Task<BaoCaoSnapshotDto> CreateDraftAsync(CreateBaoCaoSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var currentUser = _currentUserService.GetCurrentUser();
        var nextVersion = await _dbContext.BaoCaoSnapshots
            .IgnoreQueryFilters()
            .Where(x => x.KyBaoCaoId == request.KyBaoCaoId && x.DonViId == request.DonViId)
            .MaxAsync(x => (int?)x.PhienBan, cancellationToken) ?? 0;
        var entity = new BaoCaoSnapshotEntity
        {
            KyBaoCaoId = request.KyBaoCaoId,
            DonViId = request.DonViId,
            GhiChu = request.GhiChu,
            PhienBan = nextVersion + 1,
            TrangThai = SnapshotStatus.Draft,
            CreatedBy = currentUser.UserId,
            UpdatedBy = currentUser.UserId
        };
        await _dbContext.BaoCaoSnapshots.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<BaoCaoSnapshotDto> UpdateDraftAsync(long id, UpdateBaoCaoSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var entity = await _dbContext.BaoCaoSnapshots.FirstAsync(x => x.Id == id, cancellationToken);
        if (entity.TrangThai != SnapshotStatus.Draft)
            throw new AppException("SNAPSHOT_ALREADY_SUBMITTED", "Snapshot không còn ở trạng thái nháp.", 422);
        entity.GhiChu = request.GhiChu;
        entity.UpdatedBy = _currentUserService.GetCurrentUser().UserId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<BaoCaoSnapshotDto> SubmitCurrentAsync(SubmitCurrentSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        var ky = await _dbContext.KyBaoCaos.FirstOrDefaultAsync(x => x.Id == request.KyBaoCaoId, cancellationToken)
            ?? throw new AppException("KY_BAO_CAO_NOT_FOUND", "Không tìm thấy kỳ báo cáo.", 404);

        if (ky.TrangThai != KyBaoCaoStatus.DangMo)
        {
            throw new AppException("SNAPSHOT_PERIOD_CLOSED", "Kỳ báo cáo không ở trạng thái mở để nộp.", 422);
        }

        var hasActiveSnapshot = await HasActiveSnapshotAsync(request.KyBaoCaoId, request.DonViId, null, cancellationToken);
        if (hasActiveSnapshot)
        {
            throw new AppException("SNAPSHOT_ALREADY_SUBMITTED", "Kỳ báo cáo này đã nộp. Vui lòng hủy nộp trước khi nộp lại.", 422);
        }

        var submitContext = await GetSubmitContextAsync(request.KyBaoCaoId, request.DonViId, cancellationToken);
        if (submitContext.IsTongHop
            && submitContext.HasUnconfirmedChildren
            && !request.ForceSubmitWhenChildrenUnconfirmed)
        {
            throw new AppException(
                "SNAPSHOT_CHILDREN_UNCONFIRMED",
                $"Con {submitContext.TotalChildren - submitContext.ConfirmedChildren} don vi con chua xac nhan. Vui long xac nhan canh bao de tiep tuc nop.",
                422);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var builtSnapshotJson = await BuildSnapshotJsonAsync(request.KyBaoCaoId, request.DonViId, cancellationToken);
        var nextVersion = await _dbContext.BaoCaoSnapshots
            .IgnoreQueryFilters()
            .Where(x => x.KyBaoCaoId == request.KyBaoCaoId && x.DonViId == request.DonViId)
            .MaxAsync(x => (int?)x.PhienBan, cancellationToken) ?? 0;

        var entity = new BaoCaoSnapshotEntity
        {
            KyBaoCaoId = request.KyBaoCaoId,
            DonViId = request.DonViId,
            PhienBan = nextVersion + 1,
            TrangThai = SnapshotStatus.Draft,
            CreatedBy = currentUser.UserId,
            UpdatedBy = currentUser.UserId,
            GhiChu = request.GhiChu,
        };

        await _dbContext.BaoCaoSnapshots.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        var submitted = await SubmitAsync(entity.Id, new SubmitBaoCaoSnapshotRequest { GhiChu = request.GhiChu }, cancellationToken);
        submitted.SnapshotJson = builtSnapshotJson;
        return submitted;
    }

    public async Task<BaoCaoSnapshotDto> MoLaiAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var now = _dateTimeProvider.Now;
        await using var transaction = await BeginBusinessTransactionAsync(cancellationToken);

        var activeSnapshots = await _dbContext.BaoCaoSnapshots
            .Where(x => x.KyBaoCaoId == kyBaoCaoId
                && x.DonViId == donViId
                && (x.TrangThai == SnapshotStatus.Submitted || x.TrangThai == SnapshotStatus.Locked))
            .OrderByDescending(x => x.PhienBan)
            .ToListAsync(cancellationToken);

        if (activeSnapshots.Count == 0)
        {
            throw new AppException("SNAPSHOT_REOPEN_NOT_FOUND", "Không tìm thấy snapshot đã nộp/khóa để mở lại.", 422);
        }

        foreach (var snapshot in activeSnapshots)
        {
            snapshot.TrangThai = SnapshotStatus.Superseded;
            snapshot.UpdatedBy = currentUser.UserId;
        }

        var latestSnapshot = activeSnapshots[0];
        var nextVersion = await _dbContext.BaoCaoSnapshots
            .IgnoreQueryFilters()
            .Where(x => x.KyBaoCaoId == kyBaoCaoId && x.DonViId == donViId)
            .MaxAsync(x => (int?)x.PhienBan, cancellationToken) ?? 0;

        var newSnapshot = new BaoCaoSnapshotEntity
        {
            KyBaoCaoId = latestSnapshot.KyBaoCaoId,
            DonViId = latestSnapshot.DonViId,
            GhiChu = latestSnapshot.GhiChu,
            PhienBan = nextVersion + 1,
            TrangThai = SnapshotStatus.Draft,
            CreatedBy = currentUser.UserId,
            UpdatedBy = currentUser.UserId
        };
        await _dbContext.BaoCaoSnapshots.AddAsync(newSnapshot, cancellationToken);
        var kyTrangThai = await _dbContext.KyTrangThaiDonVis
            .FirstOrDefaultAsync(x => x.KyBaoCaoId == kyBaoCaoId && x.DonViId == donViId, cancellationToken);
        if (kyTrangThai is null)
        {
            kyTrangThai = new KyTrangThaiDonVi
            {
                KyBaoCaoId = kyBaoCaoId,
                DonViId = donViId,
            };
            _dbContext.KyTrangThaiDonVis.Add(kyTrangThai);
        }

        kyTrangThai.TrangThai = KyTrangThaiDonViStatus.DangBoSung;
        kyTrangThai.NgayMoLai = now;
        kyTrangThai.MoLaiBy = currentUser.UserId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return await GetAsync(newSnapshot.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<BaoCaoSnapshotDto> SubmitAsync(long id, SubmitBaoCaoSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BaoCaoSnapshots.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("SNAPSHOT_NOT_FOUND", "Không tìm thấy snapshot.", 404);

        if (entity.TrangThai != SnapshotStatus.Draft)
        {
            throw new AppException("SNAPSHOT_ALREADY_SUBMITTED", "Snapshot đã nộp hoặc đã khóa.", 422);
        }

        var ky = await _dbContext.KyBaoCaos.FirstOrDefaultAsync(x => x.Id == entity.KyBaoCaoId, cancellationToken)
            ?? throw new AppException("KY_BAO_CAO_NOT_FOUND", "Không tìm thấy kỳ báo cáo.", 404);

        if (ky.TrangThai != KyBaoCaoStatus.DangMo)
        {
            throw new AppException("SNAPSHOT_PERIOD_CLOSED", "Kỳ báo cáo không ở trạng thái mở để nộp.", 422);
        }

        var hasActiveSnapshot = await HasActiveSnapshotAsync(entity.KyBaoCaoId, entity.DonViId, entity.Id, cancellationToken);
        if (hasActiveSnapshot)
        {
            throw new AppException("SNAPSHOT_ALREADY_SUBMITTED", "Kỳ báo cáo này đã nộp. Vui lòng hủy nộp trước khi nộp lại.", 422);
        }

        var now = _dateTimeProvider.Now;
        var currentUser = _currentUserService.GetCurrentUser();
        await using var transaction = await BeginBusinessTransactionAsync(cancellationToken);

        var modeContext = await _donViInputModeService.GetContextAsync(entity.DonViId, cancellationToken);
        var batch = await CreateSnapshotBatchAsync(entity.KyBaoCaoId, entity.DonViId, now, currentUser.UserId, cancellationToken);

        var copiedRows = 0;
        if (modeContext.IsTongHop)
        {
            var kyModuleList = ParseSnapshotModuleList(
                await _dbContext.MauBaoCaos
                    .Where(m => m.Id == ky.MauBaoCaoId)
                    .Select(m => m.DanhSachModule)
                    .FirstOrDefaultAsync(cancellationToken) ?? "[]");
            var allDonViIds = GetTongHopSnapshotDonViIds(modeContext, entity.DonViId);
            copiedRows = await CopyLiveToHisAsync(ky.KyCode, allDonViIds, batch.Id, now, currentUser.UserId, kyModuleList, cancellationToken);
            await WriteTongHopConfirmationsAsync(entity.Id, entity.KyBaoCaoId, modeContext.DescendantDonViIds, now, currentUser.UserId, cancellationToken);
        }

        batch.Status = "SUCCEEDED";
        batch.FinishedAt = now; // cùng mốc với SubmittedAt để ResolveSnapshotBatchAsync (FinishedAt <= SubmittedAt) tìm thấy
        batch.TotalRows = copiedRows;
        batch.UpdatedBy = currentUser.UserId;

        entity.TrangThai = SnapshotStatus.Locked;
        entity.SubmittedAt = now;
        entity.SubmittedBy = currentUser.UserId;
        entity.LockedAt = now;
        entity.LockedBy = currentUser.UserId;
        entity.UpdatedBy = currentUser.UserId;
        entity.GhiChu = request.GhiChu;

        var kyTrangThaiDonVi = await _dbContext.KyTrangThaiDonVis.FirstOrDefaultAsync(
            x => x.KyBaoCaoId == entity.KyBaoCaoId && x.DonViId == entity.DonViId,
            cancellationToken);
        if (kyTrangThaiDonVi is null)
        {
            // Dòng KyTrangThaiDonVi có thể chưa tồn tại (đơn vị Tu nhap chưa bao giờ qua bước xac-nhan) -- tao moi thay vi bo qua,
            // neu khong thanh Snapshot da Locked nhung thanh Chua nhap mai mai (GetDonViTrangThaiAsync fallback ve 1).
            kyTrangThaiDonVi = new KyTrangThaiDonVi
            {
                KyBaoCaoId = entity.KyBaoCaoId,
                DonViId = entity.DonViId,
            };
            _dbContext.KyTrangThaiDonVis.Add(kyTrangThaiDonVi);
        }

        kyTrangThaiDonVi.TrangThai = KyTrangThaiDonViStatus.DaNop;
        kyTrangThaiDonVi.NgayXacNhan = now;
        kyTrangThaiDonVi.ConfirmedBy = currentUser.UserId;

        var yeuCauDangBoSung = await _dbContext.YeuCauBoSungs.FirstOrDefaultAsync(
            x => x.KyBaoCaoId == entity.KyBaoCaoId
              && x.DonViId == entity.DonViId
              && (x.TrangThai == YeuCauBoSungStatus.DangBoSung || x.TrangThai == YeuCauBoSungStatus.DaDuyet),
            cancellationToken);
        if (yeuCauDangBoSung is not null)
        {
            yeuCauDangBoSung.TrangThai = YeuCauBoSungStatus.HoanThanh;
            yeuCauDangBoSung.CompletedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync(
            AuditActionType.Submit,
            nameof(BaoCaoSnapshot),
            entity.Id,
            null,
            $"{{\"kyBaoCaoId\":{entity.KyBaoCaoId},\"donViId\":{entity.DonViId},\"phienBan\":{entity.PhienBan}}}",
            "/api/v1/snapshot/{id}/submit",
            null,
            null,
            cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        try
        {
            await GeneratePdfAsync(entity.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Generate PDF failed after submit for snapshot {SnapshotId}. Submit result remains successful.", entity.Id);
        }

        return await GetAsync(id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<SnapshotBreakdownDto> GetBreakdownAsync(long id, CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadSnapshotAsync(id, cancellationToken);
        var modeContext = await _donViInputModeService.GetContextAsync(snapshot.DonViId, cancellationToken);

        if (!modeContext.IsTongHop)
        {
            return new SnapshotBreakdownDto
            {
                SnapshotId = snapshot.Id,
                KyBaoCaoId = snapshot.KyBaoCaoId,
                KyCode = snapshot.KyBaoCao?.KyCode ?? string.Empty,
                DonViId = snapshot.DonViId,
                TenDonVi = snapshot.DonVi?.TenDonVi ?? string.Empty,
                SubmittedAt = snapshot.SubmittedAt,
            };
        }

        var childIds = modeContext.DescendantDonViIds;
        if (childIds.Count == 0)
        {
            return new SnapshotBreakdownDto
            {
                SnapshotId = snapshot.Id,
                KyBaoCaoId = snapshot.KyBaoCaoId,
                KyCode = snapshot.KyBaoCao?.KyCode ?? string.Empty,
                DonViId = snapshot.DonViId,
                TenDonVi = snapshot.DonVi?.TenDonVi ?? string.Empty,
                SubmittedAt = snapshot.SubmittedAt,
            };
        }

        var batch = await ResolveSnapshotBatchAsync(snapshot, cancellationToken);

        var childInfos = await _dbContext.DonVis
            .AsNoTracking()
            .Where(x => childIds.Contains(x.Id))
            .Select(x => new { x.Id, x.TenDonVi })
            .ToListAsync(cancellationToken);

        var confirmationLookup = await _dbContext.BaoCaoSnapshotXacNhans
            .AsNoTracking()
            .Where(x => x.SnapshotId == snapshot.Id)
            .ToDictionaryAsync(x => x.DonViId, cancellationToken);

        var children = new List<SnapshotBreakdownUnitDto>(childInfos.Count);
        foreach (var child in childInfos.OrderBy(x => x.TenDonVi))
        {
            children.Add(new SnapshotBreakdownUnitDto
            {
                DonViId = child.Id,
                TenDonVi = child.TenDonVi,
                DaXacNhan = confirmationLookup.TryGetValue(child.Id, out var confirmation) && confirmation.DaXacNhan,
                ModuleCounts = await BuildModuleCountsForDonViAsync(child.Id, batch, cancellationToken),
            });
        }

        return new SnapshotBreakdownDto
        {
            SnapshotId = snapshot.Id,
            KyBaoCaoId = snapshot.KyBaoCaoId,
            KyCode = snapshot.KyBaoCao?.KyCode ?? string.Empty,
            DonViId = snapshot.DonViId,
            TenDonVi = snapshot.DonVi?.TenDonVi ?? string.Empty,
            SubmittedAt = snapshot.SubmittedAt,
            TotalChildren = childInfos.Count,
            ConfirmedChildren = children.Count(x => x.DaXacNhan),
            Children = children,
        };
    }

    public async Task CancelAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BaoCaoSnapshots.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("SNAPSHOT_NOT_FOUND", "Không tìm thấy snapshot.", 404);

        if (entity.TrangThai != SnapshotStatus.Submitted && entity.TrangThai != SnapshotStatus.Locked)
        {
            throw new AppException("SNAPSHOT_CANCEL_INVALID", "Chỉ có thể hủy snapshot đã nộp.", 422);
        }

        var now = _dateTimeProvider.Now;
        var currentUser = _currentUserService.GetCurrentUser();

        entity.TrangThai = SnapshotStatus.Superseded;
        entity.DeletedAt = now;
        entity.UpdatedBy = currentUser.UserId;

        var kyTrangThai = await _dbContext.KyTrangThaiDonVis.FirstOrDefaultAsync(
            x => x.KyBaoCaoId == entity.KyBaoCaoId && x.DonViId == entity.DonViId,
            cancellationToken);
        if (kyTrangThai is not null)
        {
            kyTrangThai.TrangThai = KyTrangThaiDonViStatus.ChuaNhap;
            kyTrangThai.NgayXacNhan = null;
            kyTrangThai.ConfirmedBy = null;
        }

        var files = await _dbContext.BaoCaoFiles
            .Where(x => x.BaoCaoSnapshotId == entity.Id)
            .ToListAsync(cancellationToken);
        if (files.Count > 0)
        {
            _dbContext.BaoCaoFiles.RemoveRange(files);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync(
            AuditActionType.Delete,
            nameof(BaoCaoSnapshot),
            entity.Id,
            null,
            $"{{\"cancelled\":true,\"kyBaoCaoId\":{entity.KyBaoCaoId}}}",
            "/api/v1/snapshot/{id}",
            null,
            null,
            cancellationToken);
    }

    public async Task<FinalizeBizModuleResult> FinalizeBizModuleAsync(FinalizeBizModuleRequest request, CancellationToken cancellationToken = default)
    {
        if (request.DonViId <= 0)
            throw new AppException("SNAPSHOT_DONVI_REQUIRED", "Đơn vị là bắt buộc.", 400);

        var kyCode = request.KyBaoCaoCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(kyCode))
            throw new AppException("SNAPSHOT_KY_REQUIRED", "Kỳ báo cáo là bắt buộc.", 400);

        var ky = await _dbContext.KyBaoCaos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.KyCode == kyCode, cancellationToken)
            ?? throw new AppException("KY_BAO_CAO_NOT_FOUND", "Không tìm thấy kỳ báo cáo.", 404);

        var currentUser = _currentUserService.GetCurrentUser();
        var now = _dateTimeProvider.Now;

        var batch = new SnapshotBatchEntity
        {
            KyBaoCaoId = ky.Id,
            DonViId = request.DonViId,
            Status = "RUNNING",
            StartedAt = now,
            CreatedBy = currentUser.UserId,
            UpdatedBy = currentUser.UserId,
        };
        await _dbContext.SnapshotBatches.AddAsync(batch, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var kyModuleList = ParseSnapshotModuleList(
                await _dbContext.MauBaoCaos
                    .Where(m => m.Id == ky.MauBaoCaoId)
                    .Select(m => m.DanhSachModule)
                    .FirstOrDefaultAsync(cancellationToken) ?? "[]");
            var totalRows = await CopyLiveToHisAsync(kyCode, new[] { request.DonViId }, batch.Id, now, currentUser.UserId, kyModuleList, cancellationToken);
            batch.Status = "SUCCEEDED";
            batch.FinishedAt = _dateTimeProvider.Now;
            batch.TotalRows = totalRows;
            batch.UpdatedBy = currentUser.UserId;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            batch.Status = "FAILED";
            batch.FinishedAt = _dateTimeProvider.Now;
            batch.ErrorMessage = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
            batch.UpdatedBy = currentUser.UserId;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        return new FinalizeBizModuleResult
        {
            BatchId = batch.Id,
            DonViId = request.DonViId,
            KyBaoCaoCode = kyCode,
            ModuleKey = string.IsNullOrWhiteSpace(request.ModuleKey) ? null : request.ModuleKey.Trim(),
            FinishedAt = batch.FinishedAt ?? now,
        };
    }

    public async Task<BaoCaoPdfResultDto> GeneratePdfAsync(long id, CancellationToken cancellationToken = default)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);
        var entity = await _dbContext.BaoCaoSnapshots
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("SNAPSHOT_NOT_FOUND", "Không tìm thấy snapshot.", 404);

        if (!scope.Contains(entity.DonViId))
        {
            throw new AppException("SNAPSHOT_NOT_FOUND", "Không tìm thấy snapshot.", 404);
        }

        if (entity.TrangThai == SnapshotStatus.Draft)
            throw new AppException("SNAPSHOT_NOT_SUBMITTED", "Chỉ được xuất PDF từ snapshot đã nộp.", 422);
        var existingFile = await _dbContext.BaoCaoFiles
            .Where(x => x.BaoCaoSnapshotId == entity.Id && x.MimeType == "application/pdf")
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingFile is not null)
        {
            return new BaoCaoPdfResultDto
            {
                SnapshotId = entity.Id,
                FileName = existingFile.FileName,
                DownloadUrl = await _fileStorageService.GetPresignedDownloadUrlAsync(existingFile.FilePath, TimeSpan.FromMinutes(15), cancellationToken)
            };
        }
        var html = $"<html><body><h1>Báo cáo #{entity.Id} — Kỳ {entity.KyBaoCaoId} — Đơn vị {entity.DonViId}</h1><p>Nộp lúc: {entity.SubmittedAt:dd/MM/yyyy HH:mm}</p></body></html>";
        var pdfBytes = await _pdfService.GenerateFromHtmlAsync(html, cancellationToken);
        if (pdfBytes.Length > MaxGeneratedPdfBytes)
            throw new InvalidOperationException("Kích thước PDF vượt quá giới hạn cho phép.");
        var fileName = $"snapshot-{entity.KyBaoCaoId}-{entity.DonViId}-v{entity.PhienBan}.pdf";
        var objectKey = $"{entity.DonViId}/{entity.KyBaoCaoId}/snapshot/{Guid.NewGuid():N}.pdf";
        await using var stream = new MemoryStream(pdfBytes);
        var filePath = await _fileStorageService.UploadAsync(objectKey, stream, "application/pdf", cancellationToken);
        var reportFile = new BaoCaoFileEntity
        {
            BaoCaoSnapshotId = entity.Id,
            FileName = fileName,
            FilePath = filePath,
            MimeType = "application/pdf",
            FileSize = pdfBytes.Length,
            CreatedBy = _currentUserService.GetCurrentUser().UserId,
            UpdatedBy = _currentUserService.GetCurrentUser().UserId
        };
        await _dbContext.BaoCaoFiles.AddAsync(reportFile, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync(
            AuditActionType.GeneratePdf,
            nameof(BaoCaoFileEntity),
            reportFile.Id,
            null,
            $"{{\"snapshotId\":{entity.Id},\"fileName\":\"{fileName}\"}}",
            "/api/v1/snapshot/{id}/pdf",
            null,
            null,
            cancellationToken);
        return new BaoCaoPdfResultDto
        {
            SnapshotId = entity.Id,
            FileName = fileName,
            DownloadUrl = await _fileStorageService.GetPresignedDownloadUrlAsync(filePath, TimeSpan.FromMinutes(15), cancellationToken)
        };
    }

    public async Task<string> BuildSnapshotJsonAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);
        if (!scope.Contains(donViId))
        {
            throw new AppException("SNAPSHOT_SCOPE_DENIED", "Không có quyền xem dữ liệu đơn vị ngoài phạm vi.", 403);
        }

        var ky = await _dbContext.KyBaoCaos
            .Include(x => x.MauBaoCao)
            .FirstOrDefaultAsync(x => x.Id == kyBaoCaoId, cancellationToken)
            ?? throw new AppException("KY_BAO_CAO_NOT_FOUND", "Không tìm thấy kỳ báo cáo.", 404);

        var moduleList = ParseSnapshotModuleList(ky.MauBaoCao?.DanhSachModule ?? "[]");
        var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in moduleList)
        {
            switch (module)
            {
                case "DAO_TAO_HOC_VIEN":
                    payload[module] = await _dbContext.DaoTaoHocViens
                        .AsNoTracking()
                        .Where(x => x.DonViId == donViId)
                        .OrderByDescending(x => x.UpdatedAt)
                        .ThenByDescending(x => x.Id)
                        .Select(x => new
                        {
                            x.Id,
                            x.DonViId,
                            x.Nam,
                            x.NoiDungDaoTao,
                            x.SoTienSi,
                            x.SoThacSi,
                            x.SoDaiHoc,
                            x.SoCaoDang,
                            x.SoTrungCap,
                            x.GhiChu,
                        })
                        .ToListAsync(cancellationToken);
                    break;

                default:
                    payload[module] = Array.Empty<object>();
                    break;
            }
        }

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public async Task<IReadOnlyCollection<ModuleStatusDto>> GetModuleStatusAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default)
    {
        var ky = await _dbContext.KyBaoCaos
            .Include(x => x.MauBaoCao)
            .FirstOrDefaultAsync(x => x.Id == kyBaoCaoId, cancellationToken)
            ?? throw new AppException("KY_BAO_CAO_NOT_FOUND", "Không tìm thấy kỳ báo cáo.", 404);

        var modeContext = await _donViInputModeService.GetContextAsync(donViId, cancellationToken);
        var targetDonViIds = modeContext.IsTongHop
            ? modeContext.AggregateDonViIds
            : new[] { donViId };

        var moduleList = ParseSnapshotModuleList(ky.MauBaoCao?.DanhSachModule ?? "[]");
        var result = new List<ModuleStatusDto>();
        var ownDonViIds = new[] { donViId };

        foreach (var code in moduleList)
        {
            // TONG_HOP: count = chỉ đơn vị con (dữ liệu sẽ vào báo cáo);
            // ownCount = dữ liệu tự nhập cũ KHÔNG được tính — trả về để FE cảnh báo.
            var count = await CountModuleRecordsAsync(code, targetDonViIds, cancellationToken);
            var ownCount = modeContext.IsTongHop
                ? await CountModuleRecordsAsync(code, ownDonViIds, cancellationToken)
                : count;

            result.Add(new ModuleStatusDto
            {
                ModuleCode = code,
                RecordCount = count,
                OwnRecordCount = ownCount,
                ChildRecordCount = modeContext.IsTongHop && count >= 0 ? count : 0,
            });
        }

        return result;
    }

    private async Task<int> CountModuleRecordsAsync(string code, IReadOnlyCollection<long> targetDonViIds, CancellationToken cancellationToken)
        => code switch
        {
            "NHAN_LUC_CNTT" => await _dbContext.NhanLucCntts.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "NANG_LUC_SO" => await _dbContext.NangLucSos.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "DAO_TAO_BOI_DUONG" => await _dbContext.DaoTaoBoiDuongs.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "DAO_TAO_HOC_VIEN" => await _dbContext.DaoTaoHocViens.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "HE_THONG_THONG_TIN" => await _dbContext.HeThongThongTins.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "HTTT_TIEU_CHUAN" => await _dbContext.HtttTieuChuans.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "DU_AN_CNTT" => await _dbContext.DuAnCntts.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "THIET_BI_CNTT" => await _dbContext.ThietBiCntts.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "HA_TANG_MANG" => await _dbContext.HaTangMangs.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "GIAM_SAT_NOC" => await _dbContext.GiamSatNocs.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "CAMERA_QUAN_LY" => await _dbContext.CameraQuanLies.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "CAMERA_THUC_TRANG" => await _dbContext.CameraThucTrangs.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "GIAM_SAT_SOC" => await _dbContext.GiamSatSocs.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "ATTT_HTTT_VAN_HANH" => await _dbContext.AtttHtttVanHanhs.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "ATTT_HTTT_DAU_TU" => await _dbContext.AtttHtttDauTus.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "ATTT_GIAI_PHAP" => await _dbContext.GiaiPhapAttts.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            "VAN_BAN_QPPL" => await _dbContext.VanBanQppls.CountAsync(x => targetDonViIds.Contains(x.DonViId), cancellationToken),
            _ => -1
        };

    private static IReadOnlyCollection<string> ParseSnapshotModuleList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    public async Task<SubmitSnapshotContextDto> GetSubmitContextAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default)
    {
        var modeContext = await _donViInputModeService.GetContextAsync(donViId, cancellationToken);
        if (!modeContext.IsTongHop)
        {
            return new SubmitSnapshotContextDto
            {
                CheDoNhapLieu = modeContext.CheDoNhapLieu,
                IsTongHop = false,
                TotalChildren = 0,
                ConfirmedChildren = 0,
                HasUnconfirmedChildren = false,
            };
        }

        var childIds = modeContext.DescendantDonViIds;
        var totalChildren = childIds.Count;
        if (totalChildren == 0)
        {
            return new SubmitSnapshotContextDto
            {
                CheDoNhapLieu = modeContext.CheDoNhapLieu,
                IsTongHop = true,
                TotalChildren = 0,
                ConfirmedChildren = 0,
                HasUnconfirmedChildren = false,
            };
        }

        var confirmedFlag = true;
        var confirmedChildren = await _dbContext.KyTrangThaiDonVis
            .CountAsync(
                x => x.KyBaoCaoId == kyBaoCaoId
                  && childIds.Contains(x.DonViId)
                  && x.DaXacNhan == confirmedFlag,
                cancellationToken);

        var lastSubmittedAt = await _dbContext.BaoCaoSnapshots
            .Where(x => x.KyBaoCaoId == kyBaoCaoId && x.DonViId == donViId && x.TrangThai == SnapshotStatus.Locked)
            .OrderByDescending(x => x.SubmittedAt)
            .Select(x => x.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var latestChildUpdatedAt = await _dbContext.KyTrangThaiDonVis
            .Where(x => x.KyBaoCaoId == kyBaoCaoId && childIds.Contains(x.DonViId))
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => (DateTime?)x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new SubmitSnapshotContextDto
        {
            CheDoNhapLieu = modeContext.CheDoNhapLieu,
            IsTongHop = true,
            TotalChildren = totalChildren,
            ConfirmedChildren = confirmedChildren,
            HasUnconfirmedChildren = confirmedChildren < totalChildren,
            LastSubmittedAt = lastSubmittedAt,
            LatestChildUpdatedAt = latestChildUpdatedAt,
            HasChildDataChangedAfterLastSubmit =
                lastSubmittedAt.HasValue
                && latestChildUpdatedAt.HasValue
                && latestChildUpdatedAt.Value > lastSubmittedAt.Value,
        };
    }

    private async Task<IReadOnlyCollection<ModuleStatusDto>> BuildModuleCountsForDonViAsync(long donViId, SnapshotBatchEntity batch, CancellationToken cancellationToken)
    {
        // 12 module chốt qua CopyLiveToHis: đếm theo (KyBaoCaoCode, DonViId) — mỗi cặp này
        // chỉ tồn tại đúng 1 bộ dữ liệu chốt (RemoveRange trước khi ghi), nên KHÔNG so
        // SnapshotBatchId (đơn vị con chốt lại sau khi nộp sẽ đổi batch id → so id bị 0 nhầm).
        // 5 module version-hoá (NHAN_LUC_CNTT, HE_THONG_THONG_TIN, HTTT_TIEU_CHUAN,
        // THIET_BI_CNTT, HA_TANG_MANG) không đi qua CopyLiveToHis: đếm "as-of" thời điểm chốt.
        var kyCode = await _dbContext.KyBaoCaos
            .Where(k => k.Id == batch.KyBaoCaoId)
            .Select(k => k.KyCode)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        var asOf = batch.FinishedAt ?? batch.StartedAt;

        return new List<ModuleStatusDto>
        {
            new()
            {
                ModuleCode = "NHAN_LUC_CNTT",
                RecordCount =
                    await _dbContext.NhanLucCntts.CountAsync(x => x.DonViId == donViId && x.ValidFrom <= asOf, cancellationToken)
                    + await _dbContext.NhanLucCnttHis.CountAsync(x => x.DonViId == donViId && x.ValidFrom <= asOf && x.ValidTo > asOf, cancellationToken),
            },
            new() { ModuleCode = "NANG_LUC_SO", RecordCount = await _dbContext.NangLucSoHis.CountAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, cancellationToken) },
            new() { ModuleCode = "DAO_TAO_BOI_DUONG", RecordCount = await _dbContext.DaoTaoBoiDuongHis.CountAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, cancellationToken) },
            new() { ModuleCode = "DAO_TAO_HOC_VIEN", RecordCount = await _dbContext.DaoTaoHocVienHis.CountAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, cancellationToken) },
            new()
            {
                ModuleCode = "HE_THONG_THONG_TIN",
                RecordCount =
                    await _dbContext.HeThongThongTins.CountAsync(x => x.DonViId == donViId && x.ValidFrom <= asOf, cancellationToken)
                    + await _dbContext.HeThongThongTinHis.CountAsync(x => x.DonViId == donViId && x.ValidFrom <= asOf && x.ValidTo > asOf, cancellationToken),
            },
            new()
            {
                ModuleCode = "HTTT_TIEU_CHUAN",
                RecordCount =
                    await _dbContext.HtttTieuChuans.CountAsync(x => x.DonViId == donViId && x.ValidFrom <= asOf, cancellationToken)
                    + await _dbContext.HtttTieuChuanHis.CountAsync(x => x.DonViId == donViId && x.ValidFrom <= asOf && x.ValidTo > asOf, cancellationToken),
            },
            new() { ModuleCode = "DU_AN_CNTT", RecordCount = await _dbContext.DuAnCnttHis.CountAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, cancellationToken) },
            new()
            {
                ModuleCode = "THIET_BI_CNTT",
                RecordCount =
                    await _dbContext.ThietBiCntts.CountAsync(x => x.DonViId == donViId && x.ValidFrom <= asOf, cancellationToken)
                    + await _dbContext.ThietBiCnttHis.CountAsync(x => x.DonViId == donViId && x.ValidFrom <= asOf && x.ValidTo > asOf, cancellationToken),
            },
            new() { ModuleCode = "HA_TANG_MANG", RecordCount = await _dbContext.HaTangMangHis.CountAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, cancellationToken) },
            new() { ModuleCode = "GIAM_SAT_NOC", RecordCount = await _dbContext.GiamSatNocHis.CountAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, cancellationToken) },
            new() { ModuleCode = "CAMERA_QUAN_LY", RecordCount = await _dbContext.CameraQuanLyHis.CountAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, cancellationToken) },
            new() { ModuleCode = "CAMERA_THUC_TRANG", RecordCount = await _dbContext.CameraThucTrangHis.CountAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, cancellationToken) },
            new() { ModuleCode = "GIAM_SAT_SOC", RecordCount = await _dbContext.GiamSatSocHis.CountAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, cancellationToken) },
            new() { ModuleCode = "ATTT_HTTT_VAN_HANH", RecordCount = await _dbContext.AtttHtttVanHanhHis.CountAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, cancellationToken) },
            new() { ModuleCode = "ATTT_HTTT_DAU_TU", RecordCount = await _dbContext.AtttHtttDauTuHis.CountAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, cancellationToken) },
            new() { ModuleCode = "ATTT_GIAI_PHAP", RecordCount = await _dbContext.GiaiPhapAtttHis.CountAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, cancellationToken) },
            new() { ModuleCode = "VAN_BAN_QPPL", RecordCount = await _dbContext.VanBanQpplHis.CountAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, cancellationToken) },
        };
    }

    private async Task<Dictionary<string, int>> BuildSnapshotModuleTotalsAsync(BaoCaoSnapshotEntity snapshot, CancellationToken cancellationToken)
    {
        var modeContext = await _donViInputModeService.GetContextAsync(snapshot.DonViId, cancellationToken);
        var batch = await ResolveSnapshotBatchAsync(snapshot, cancellationToken);
        var targetDonViIds = modeContext.IsTongHop
            ? modeContext.AggregateDonViIds
            : new[] { snapshot.DonViId };

        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var targetDonViId in targetDonViIds)
        {
            var moduleCounts = await BuildModuleCountsForDonViAsync(targetDonViId, batch, cancellationToken);
            foreach (var module in moduleCounts)
            {
                if (totals.TryGetValue(module.ModuleCode, out var current))
                {
                    totals[module.ModuleCode] = current + module.RecordCount;
                }
                else
                {
                    totals[module.ModuleCode] = module.RecordCount;
                }
            }
        }

        return totals;
    }

    private async Task<bool> HasActiveSnapshotAsync(long kyBaoCaoId, long donViId, long? excludedSnapshotId, CancellationToken cancellationToken)
    {
        var submittedCount = await _dbContext.BaoCaoSnapshots.CountAsync(
            x => x.KyBaoCaoId == kyBaoCaoId
                && x.DonViId == donViId
                && (!excludedSnapshotId.HasValue || x.Id != excludedSnapshotId.Value)
                && x.TrangThai == SnapshotStatus.Submitted,
            cancellationToken);
        if (submittedCount > 0)
        {
            return true;
        }

        var lockedCount = await _dbContext.BaoCaoSnapshots.CountAsync(
            x => x.KyBaoCaoId == kyBaoCaoId
                && x.DonViId == donViId
                && (!excludedSnapshotId.HasValue || x.Id != excludedSnapshotId.Value)
                && x.TrangThai == SnapshotStatus.Locked,
            cancellationToken);
        return lockedCount > 0;
    }

    private DbContext GetEfDbContext()
    {
        if (_dbContext is DbContext dbContext)
        {
            return dbContext;
        }

        throw new InvalidOperationException("IApplicationDbContext must also be a DbContext.");
    }

    private async Task<IDbContextTransaction?> BeginBusinessTransactionAsync(CancellationToken cancellationToken)
    {
        var dbContext = GetEfDbContext();
        var providerName = dbContext.Database.ProviderName;
        if (!string.IsNullOrWhiteSpace(providerName)
            && providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    private async Task<BaoCaoSnapshotEntity> LoadSnapshotAsync(long id, CancellationToken cancellationToken)
    {
        var scope = await _donViDataScopeService.GetScopeAsync(cancellationToken);
        var query = _dbContext.BaoCaoSnapshots
            .Include(x => x.KyBaoCao)
            .Include(x => x.DonVi)
            .Where(x => x.Id == id);

        if (!scope.HasFullAccess)
        {
            var allowedIds = scope.AllowedDonViIds;
            query = query.Where(x => allowedIds.Contains(x.DonViId));
        }

        return await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppException("SNAPSHOT_NOT_FOUND", "Không tìm thấy snapshot.", 404);
    }

    private async Task<SnapshotBatchEntity> CreateSnapshotBatchAsync(long kyBaoCaoId, long donViId, DateTime now, long? userId, CancellationToken cancellationToken)
    {
        var batch = new SnapshotBatchEntity
        {
            KyBaoCaoId = kyBaoCaoId,
            DonViId = donViId,
            Status = "RUNNING",
            StartedAt = now,
            CreatedBy = userId,
            UpdatedBy = userId,
        };

        await _dbContext.SnapshotBatches.AddAsync(batch, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return batch;
    }

    private async Task<SnapshotBatchEntity> ResolveSnapshotBatchAsync(BaoCaoSnapshotEntity snapshot, CancellationToken cancellationToken)
    {
        var query = _dbContext.SnapshotBatches
            .Where(x => x.KyBaoCaoId == snapshot.KyBaoCaoId && x.DonViId == snapshot.DonViId && x.Status == "SUCCEEDED");

        if (snapshot.SubmittedAt.HasValue)
        {
            query = query.Where(x => x.FinishedAt != null && x.FinishedAt <= snapshot.SubmittedAt);
        }

        var batch = await query
            .OrderByDescending(x => x.FinishedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (batch is not null)
        {
            return batch;
        }

        throw new AppException("SNAPSHOT_BATCH_NOT_FOUND", "Không tìm thấy batch chốt dữ liệu cho snapshot.", 422);
    }

    private static IReadOnlyCollection<long> GetTongHopSnapshotDonViIds(ThucLuc.Application.Features.DonVi.DonViInputModeContext modeContext, long parentDonViId)
    {
        // TONG_HOP: snapshot chỉ gộp dữ liệu đơn vị cấp dưới, không tính dữ liệu
        // tự nhập của chính đơn vị tổng hợp (AggregateDonViIds đã fallback về
        // chính nó khi không có đơn vị con).
        return modeContext.AggregateDonViIds;
    }

    private async Task WriteTongHopConfirmationsAsync(long snapshotId, long kyBaoCaoId, IReadOnlyCollection<long> childDonViIds, DateTime now, long? userId, CancellationToken cancellationToken)
    {
        if (childDonViIds.Count == 0)
        {
            return;
        }

        var confirmedFlag = true;
        var confirmedIds = await _dbContext.KyTrangThaiDonVis
            .Where(x => x.KyBaoCaoId == kyBaoCaoId && childDonViIds.Contains(x.DonViId) && x.DaXacNhan == confirmedFlag)
            .Select(x => x.DonViId)
            .ToListAsync(cancellationToken);
        var confirmedSet = confirmedIds.ToHashSet();

        var rows = childDonViIds.Select(donViId => new BaoCaoSnapshotXacNhanEntity
        {
            SnapshotId = snapshotId,
            DonViId = donViId,
            DaXacNhan = confirmedSet.Contains(donViId),
            XacNhanAt = confirmedSet.Contains(donViId) ? now : null,
            CreatedBy = userId,
            UpdatedBy = userId,
        }).ToList();

        await _dbContext.BaoCaoSnapshotXacNhans.AddRangeAsync(rows, cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<BaoCaoSnapshotEntity, BaoCaoSnapshotDto>> ToDto() => entity => new BaoCaoSnapshotDto
    {
        Id = entity.Id,
        KyBaoCaoId = entity.KyBaoCaoId,
        KyCode = entity.KyBaoCao != null ? entity.KyBaoCao.KyCode : string.Empty,
        DonViId = entity.DonViId,
        TenDonVi = entity.DonVi != null ? entity.DonVi.TenDonVi : string.Empty,
        TrangThai = entity.TrangThai,
        PhienBan = entity.PhienBan,
        GhiChu = entity.GhiChu,
        SubmittedAt = entity.SubmittedAt,
        LockedAt = entity.LockedAt
    };

    private async Task<int> CopyLiveToHisAsync(
        string kyCode, IReadOnlyCollection<long> donViIds, long batchId, DateTime now, long? userId,
        IReadOnlyCollection<string>? moduleFilter,
        CancellationToken ct)
    {
        int total = 0;
        var distinctDonViIds = donViIds.Distinct().ToArray();

        // Chỉ copy các module thuộc mẫu báo cáo của kỳ; filter rỗng/null => copy tất cả (an toàn ngược).
        var moduleSet = moduleFilter is { Count: > 0 }
            ? new HashSet<string>(moduleFilter, StringComparer.OrdinalIgnoreCase)
            : null;
        bool Include(string moduleCode) => moduleSet == null || moduleSet.Contains(moduleCode);

        // DAO_TAO_BOI_DUONG
        if (distinctDonViIds.Length > 0 && Include("DAO_TAO_BOI_DUONG"))
        {
            _dbContext.DaoTaoBoiDuongHis.RemoveRange(await _dbContext.DaoTaoBoiDuongHis
                .Where(x => x.KyBaoCaoCode == kyCode && distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct));
            var live = await _dbContext.DaoTaoBoiDuongs.Where(x => distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct);
            await _dbContext.DaoTaoBoiDuongHis.AddRangeAsync(live.Select(r => new DaoTaoBoiDuongHis
            {
                SourceId = r.Id,
                DonViId = r.DonViId,
                TenKhoaHoc = r.TenKhoaHoc,
                DonViToChuc = r.DonViToChuc,
                HinhThuc = r.HinhThuc,
                SoLuongHv = r.SoLuongHv,
                ThoiGianTu = r.ThoiGianTu,
                ThoiGianDen = r.ThoiGianDen,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode,
                SnapshotBatchId = batchId,
                SnapshotCreatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // DAO_TAO_HOC_VIEN
        if (distinctDonViIds.Length > 0 && Include("DAO_TAO_HOC_VIEN"))
        {
            _dbContext.DaoTaoHocVienHis.RemoveRange(await _dbContext.DaoTaoHocVienHis
                .Where(x => x.KyBaoCaoCode == kyCode && distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct));
            var live = await _dbContext.DaoTaoHocViens.Where(x => distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct);
            await _dbContext.DaoTaoHocVienHis.AddRangeAsync(live.Select(r => new DaoTaoHocVienHis
            {
                SourceId = r.Id,
                DonViId = r.DonViId,
                Nam = r.Nam,
                NoiDungDaoTao = r.NoiDungDaoTao,
                SoTienSi = r.SoTienSi,
                SoThacSi = r.SoThacSi,
                SoDaiHoc = r.SoDaiHoc,
                SoCaoDang = r.SoCaoDang,
                SoTrungCap = r.SoTrungCap,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode,
                SnapshotBatchId = batchId,
                SnapshotCreatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // NANG_LUC_SO
        if (distinctDonViIds.Length > 0 && Include("NANG_LUC_SO"))
        {
            _dbContext.NangLucSoHis.RemoveRange(await _dbContext.NangLucSoHis
                .Where(x => x.KyBaoCaoCode == kyCode && distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct));
            var live = await _dbContext.NangLucSos.Where(x => distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct);
            await _dbContext.NangLucSoHis.AddRangeAsync(live.Select(r => new NangLucSoHis
            {
                SourceId = r.Id,
                DonViId = r.DonViId,
                NhomViTri = r.NhomViTri,
                TongSoDienDanhGia = r.TongSoDienDanhGia,
                TongSoDat = r.TongSoDat,
                TongSoChuaDat = r.TongSoChuaDat,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode,
                SnapshotBatchId = batchId,
                SnapshotCreatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // VAN_BAN_QPPL
        if (distinctDonViIds.Length > 0 && Include("VAN_BAN_QPPL"))
        {
            _dbContext.VanBanQpplHis.RemoveRange(await _dbContext.VanBanQpplHis
                .Where(x => x.KyBaoCaoCode == kyCode && distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct));
            var live = await _dbContext.VanBanQppls.Where(x => distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct);
            await _dbContext.VanBanQpplHis.AddRangeAsync(live.Select(r => new VanBanQpplHis
            {
                SourceId = r.Id,
                DonViId = r.DonViId,
                SoHieu = r.SoHieu,
                TenVanBan = r.TenVanBan,
                LoaiVanBan = r.LoaiVanBan,
                CoQuanBanHanh = r.CoQuanBanHanh,
                NgayBanHanh = r.NgayBanHanh,
                NgayHieuLuc = r.NgayHieuLuc,
                LinhVuc = r.LinhVuc,
                TrichYeu = r.TrichYeu,
                TinhTrangTrienKhai = r.TinhTrangTrienKhai,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode,
                SnapshotBatchId = batchId,
                SnapshotCreatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // DU_AN_CNTT
        if (distinctDonViIds.Length > 0 && Include("DU_AN_CNTT"))
        {
            _dbContext.DuAnCnttHis.RemoveRange(await _dbContext.DuAnCnttHis
                .Where(x => x.KyBaoCaoCode == kyCode && distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct));
            var live = await _dbContext.DuAnCntts.Where(x => distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct);
            await _dbContext.DuAnCnttHis.AddRangeAsync(live.Select(r => new DuAnCnttHis
            {
                SourceId = r.Id,
                DonViId = r.DonViId,
                TenDuAn = r.TenDuAn,
                DonViChuTri = r.DonViChuTri,
                NamTrienKhai = r.NamTrienKhai,
                NamDuaVaoSuDung = r.NamDuaVaoSuDung,
                TongKinhPhi = r.TongKinhPhi,
                NguonVon = r.NguonVon,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode,
                SnapshotBatchId = batchId,
                SnapshotCreatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // GIAM_SAT_SOC
        if (distinctDonViIds.Length > 0 && Include("GIAM_SAT_SOC"))
        {
            _dbContext.GiamSatSocHis.RemoveRange(await _dbContext.GiamSatSocHis
                .Where(x => x.KyBaoCaoCode == kyCode && distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct));
            var live = await _dbContext.GiamSatSocs.Where(x => distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct);
            await _dbContext.GiamSatSocHis.AddRangeAsync(live.Select(r => new GiamSatSocHis
            {
                SourceId = r.Id,
                DonViId = r.DonViId,
                LoaiMang = r.LoaiMang,
                LopGiamSat = r.LopGiamSat,
                CoHeThong = r.CoHeThong,
                ThucTrang = r.ThucTrang,
                TongSoDoiTuong = r.TongSoDoiTuong,
                SoGiamSatMotPhan = r.SoGiamSatMotPhan,
                SoGiamSatCoBan = r.SoGiamSatCoBan,
                SoGiamSatDayDu = r.SoGiamSatDayDu,
                SoSuCo = r.SoSuCo,
                SoSuCoDaKhacPhuc = r.SoSuCoDaKhacPhuc,
                LucLuongUngCuu = r.LucLuongUngCuu,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode,
                SnapshotBatchId = batchId,
                SnapshotCreatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // GIAM_SAT_NOC
        if (distinctDonViIds.Length > 0 && Include("GIAM_SAT_NOC"))
        {
            _dbContext.GiamSatNocHis.RemoveRange(await _dbContext.GiamSatNocHis
                .Where(x => x.KyBaoCaoCode == kyCode && distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct));
            var live = await _dbContext.GiamSatNocs.Where(x => distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct);
            await _dbContext.GiamSatNocHis.AddRangeAsync(live.Select(r => new GiamSatNocHis
            {
                SourceId = r.Id,
                DonViId = r.DonViId,
                LopGiamSat = r.LopGiamSat,
                CoNoc = r.CoNoc,
                ThucTrang = r.ThucTrang,
                TongSoDoiTuong = r.TongSoDoiTuong,
                SoDaGiamSat = r.SoDaGiamSat,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode,
                SnapshotBatchId = batchId,
                SnapshotCreatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // ATTT_HTTT_VAN_HANH
        if (distinctDonViIds.Length > 0 && Include("ATTT_HTTT_VAN_HANH"))
        {
            _dbContext.AtttHtttVanHanhHis.RemoveRange(await _dbContext.AtttHtttVanHanhHis
                .Where(x => x.KyBaoCaoCode == kyCode && distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct));
            var live = await _dbContext.AtttHtttVanHanhs.Where(x => distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct);
            await _dbContext.AtttHtttVanHanhHis.AddRangeAsync(live.Select(r => new AtttHtttVanHanhHis
            {
                SourceId = r.Id,
                DonViId = r.DonViId,
                HtttId = r.HtttId,
                ChuQuan = r.ChuQuan,
                DonViVanHanh = r.DonViVanHanh,
                CapDoDeXuat = r.CapDoDeXuat,
                TinhTrangPheDuyet = r.TinhTrangPheDuyet,
                QuyetDinhPheDuyet = r.QuyetDinhPheDuyet,
                QuyCheAttt = r.QuyCheAttt,
                DuKienNgayPheDuyet = r.DuKienNgayPheDuyet,
                DaTrienKhaiPhuongAn = r.DaTrienKhaiPhuongAn,
                DuKienNgayTrienKhai = r.DuKienNgayTrienKhai,
                KiemTraDanhGia = r.KiemTraDanhGia,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode,
                SnapshotBatchId = batchId,
                SnapshotCreatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // ATTT_HTTT_DAU_TU
        if (distinctDonViIds.Length > 0 && Include("ATTT_HTTT_DAU_TU"))
        {
            _dbContext.AtttHtttDauTuHis.RemoveRange(await _dbContext.AtttHtttDauTuHis
                .Where(x => x.KyBaoCaoCode == kyCode && distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct));
            var live = await _dbContext.AtttHtttDauTus.Where(x => distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct);
            await _dbContext.AtttHtttDauTuHis.AddRangeAsync(live.Select(r => new AtttHtttDauTuHis
            {
                SourceId = r.Id,
                DonViId = r.DonViId,
                HtttId = r.HtttId,
                ChuQuan = r.ChuQuan,
                DonViVanHanh = r.DonViVanHanh,
                CapDoDeXuat = r.CapDoDeXuat,
                NgayPheDuyetHsdxcd = r.NgayPheDuyetHsdxcd,
                QuyetDinhPheDuyet = r.QuyetDinhPheDuyet,
                DaLongGhepThuyetMinh = r.DaLongGhepThuyetMinh,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode,
                SnapshotBatchId = batchId,
                SnapshotCreatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // CAMERA_QUAN_LY
        if (distinctDonViIds.Length > 0 && Include("CAMERA_QUAN_LY"))
        {
            _dbContext.CameraQuanLyHis.RemoveRange(await _dbContext.CameraQuanLyHis
                .Where(x => x.KyBaoCaoCode == kyCode && distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct));
            var live = await _dbContext.CameraQuanLies.Where(x => distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct);
            await _dbContext.CameraQuanLyHis.AddRangeAsync(live.Select(r => new CameraQuanLyHis
            {
                SourceId = r.Id,
                DonViId = r.DonViId,
                NhomCamera = r.NhomCamera,
                TenDonViDiaChi = r.TenDonViDiaChi,
                BuongGiamTrangBiSl = r.BuongGiamTrangBiSl,
                BuongGiamTrangBiTs = r.BuongGiamTrangBiTs,
                NhuCauDauTu = r.NhuCauDauTu,
                BaoTri = r.BaoTri,
                SuaChua = r.SuaChua,
                SoLanViPham = r.SoLanViPham,
                KetNoiChiaSe = r.KetNoiChiaSe,
                HoSoCapDoAttt = r.HoSoCapDoAttt,
                CbChuyenTrach = r.CbChuyenTrach,
                CbKiemNhiem = r.CbKiemNhiem,
                CbDiaPhuong = r.CbDiaPhuong,
                DaoTaoBo = r.DaoTaoBo,
                DaoTaoNhuCau = r.DaoTaoNhuCau,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode,
                SnapshotBatchId = batchId,
                SnapshotCreatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // CAMERA_THUC_TRANG
        if (distinctDonViIds.Length > 0 && Include("CAMERA_THUC_TRANG"))
        {
            _dbContext.CameraThucTrangHis.RemoveRange(await _dbContext.CameraThucTrangHis
                .Where(x => x.KyBaoCaoCode == kyCode && distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct));
            var live = await _dbContext.CameraThucTrangs.Where(x => distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct);
            await _dbContext.CameraThucTrangHis.AddRangeAsync(live.Select(r => new CameraThucTrangHis
            {
                SourceId = r.Id,
                DonViId = r.DonViId,
                NhomCamera = r.NhomCamera,
                TenHeThong = r.TenHeThong,
                CauHinhIp = r.CauHinhIp,
                CauHinhAnalog = r.CauHinhAnalog,
                ThucTrangIp = r.ThucTrangIp,
                ThucTrangAnalog = r.ThucTrangAnalog,
                ChuDauTu = r.ChuDauTu,
                NamDauTu = r.NamDauTu,
                DuongTruyen = r.DuongTruyen,
                PhanMem = r.PhanMem,
                LuuTru = r.LuuTru,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode,
                SnapshotBatchId = batchId,
                SnapshotCreatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // ATTT_GIAI_PHAP
        if (distinctDonViIds.Length > 0 && Include("ATTT_GIAI_PHAP"))
        {
            _dbContext.GiaiPhapAtttHis.RemoveRange(await _dbContext.GiaiPhapAtttHis
                .Where(x => x.KyBaoCaoCode == kyCode && distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct));
            var live = await _dbContext.GiaiPhapAttts.Where(x => distinctDonViIds.Contains(x.DonViId)).ToListAsync(ct);
            await _dbContext.GiaiPhapAtttHis.AddRangeAsync(live.Select(r => new GiaiPhapAtttHis
            {
                SourceId = r.Id,
                DonViId = r.DonViId,
                TenGiaiPhap = r.TenGiaiPhap,
                MayTinhBcanetSl = r.MayTinhBcanetSl,
                MayTinhBcanetTs = r.MayTinhBcanetTs,
                MayTinhInternetSl = r.MayTinhInternetSl,
                MayTinhInternetTs = r.MayTinhInternetTs,
                MayTinhLocalSl = r.MayTinhLocalSl,
                MayTinhLocalTs = r.MayTinhLocalTs,
                MayChuBcanetSl = r.MayChuBcanetSl,
                MayChuBcanetTs = r.MayChuBcanetTs,
                MayChuInternetSl = r.MayChuInternetSl,
                MayChuInternetTs = r.MayChuInternetTs,
                MayChuLocalSl = r.MayChuLocalSl,
                MayChuLocalTs = r.MayChuLocalTs,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode,
                SnapshotBatchId = batchId,
                SnapshotCreatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        await _dbContext.SaveChangesAsync(ct);
        return total;
    }
}