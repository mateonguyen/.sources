using Microsoft.EntityFrameworkCore;
using FluentValidation;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Domain.Entities.Business;
using BaoCaoSnapshotEntity = ThucLuc.Domain.Entities.Reporting.BaoCaoSnapshot;
using BaoCaoFileEntity = ThucLuc.Domain.Entities.Reporting.BaoCaoFile;
using SnapshotBatchEntity = ThucLuc.Domain.Entities.Reporting.SnapshotBatch;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Application.Features.BaoCaoSnapshot;

public interface IBaoCaoSnapshotService
{
    Task<IReadOnlyCollection<DaoTaoBoiDuongPreviewItem>> PreviewDaoTaoAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default);
    Task<BaoCaoSnapshotDto?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<BaoCaoSnapshotDto>> GetByKyAsync(long kyBaoCaoId, CancellationToken cancellationToken = default);
    Task<BaoCaoSnapshotDto> CreateDraftAsync(CreateBaoCaoSnapshotRequest request, CancellationToken cancellationToken = default);
    Task<BaoCaoSnapshotDto> UpdateDraftAsync(long id, UpdateBaoCaoSnapshotRequest request, CancellationToken cancellationToken = default);
    Task<BaoCaoSnapshotDto> SubmitCurrentAsync(SubmitCurrentSnapshotRequest request, CancellationToken cancellationToken = default);
    Task<BaoCaoSnapshotDto> MoLaiAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default);
    Task<BaoCaoSnapshotDto> SubmitAsync(long id, SubmitBaoCaoSnapshotRequest request, CancellationToken cancellationToken = default);
    Task<FinalizeBizModuleResult> FinalizeBizModuleAsync(FinalizeBizModuleRequest request, CancellationToken cancellationToken = default);
    Task CancelAsync(long id, CancellationToken cancellationToken = default);
    Task<BaoCaoPdfResultDto> GeneratePdfAsync(long id, CancellationToken cancellationToken = default);
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

    public BaoCaoSnapshotService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        IPdfService pdfService,
        IAuditLogService auditLogService,
        IValidator<CreateBaoCaoSnapshotRequest> createValidator,
        IValidator<UpdateBaoCaoSnapshotRequest> updateValidator,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _pdfService = pdfService;
        _auditLogService = auditLogService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _dateTimeProvider = dateTimeProvider;
    }


    public async Task<IReadOnlyCollection<DaoTaoBoiDuongPreviewItem>> PreviewDaoTaoAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default)
    {
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
        return await _dbContext.BaoCaoSnapshots
            .Where(x => x.Id == id)
            .Select(ToDto())
            .FirstOrDefaultAsync(cancellationToken);
    }


    public async Task<IReadOnlyCollection<BaoCaoSnapshotDto>> GetByKyAsync(long kyBaoCaoId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BaoCaoSnapshots
            .Where(x => x.KyBaoCaoId == kyBaoCaoId)
            .OrderByDescending(x => x.PhienBan)
            .Select(ToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<BaoCaoSnapshotDto> CreateDraftAsync(CreateBaoCaoSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var currentUser = _currentUserService.GetCurrentUser();
        var nextVersion = await _dbContext.BaoCaoSnapshots
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
            throw new InvalidOperationException("Snapshot không còn ở trạng thái nháp.");
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

        var hasActiveSnapshot = await _dbContext.BaoCaoSnapshots
            .AnyAsync(x => x.KyBaoCaoId == request.KyBaoCaoId
                && x.DonViId == request.DonViId
                && (x.TrangThai == SnapshotStatus.Submitted || x.TrangThai == SnapshotStatus.Locked), cancellationToken);
        if (hasActiveSnapshot)
        {
            throw new AppException("SNAPSHOT_ALREADY_SUBMITTED", "Kỳ báo cáo này đã nộp. Vui lòng hủy nộp trước khi nộp lại.", 422);
        }

        var currentUser = _currentUserService.GetCurrentUser();
        var nextVersion = await _dbContext.BaoCaoSnapshots
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
        return await SubmitAsync(entity.Id, new SubmitBaoCaoSnapshotRequest { GhiChu = request.GhiChu }, cancellationToken);
    }

    public async Task<BaoCaoSnapshotDto> MoLaiAsync(long kyBaoCaoId, long donViId, CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var now = _dateTimeProvider.Now;
        var latestSnapshot = await _dbContext.BaoCaoSnapshots
            .Where(x => x.KyBaoCaoId == kyBaoCaoId && x.DonViId == donViId && x.TrangThai == SnapshotStatus.Locked)
            .OrderByDescending(x => x.PhienBan)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy snapshot đã khóa để mở lại.");
        latestSnapshot.TrangThai = SnapshotStatus.Superseded;
        var newSnapshot = new BaoCaoSnapshotEntity
        {
            KyBaoCaoId = latestSnapshot.KyBaoCaoId,
            DonViId = latestSnapshot.DonViId,
            GhiChu = latestSnapshot.GhiChu,
            PhienBan = latestSnapshot.PhienBan + 1,
            TrangThai = SnapshotStatus.Draft,
            CreatedBy = currentUser.UserId,
            UpdatedBy = currentUser.UserId
        };
        await _dbContext.BaoCaoSnapshots.AddAsync(newSnapshot, cancellationToken);
        var kyTrangThai = await _dbContext.KyTrangThaiDonVis
            .FirstOrDefaultAsync(x => x.KyBaoCaoId == kyBaoCaoId && x.DonViId == donViId, cancellationToken);
        if (kyTrangThai is not null)
        {
            kyTrangThai.TrangThai = KyTrangThaiDonViStatus.DangBoSung;
            kyTrangThai.NgayMoLai = now;
            kyTrangThai.MoLaiBy = currentUser.UserId;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
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

        var hasActiveSnapshot = await _dbContext.BaoCaoSnapshots
            .AnyAsync(x => x.Id != entity.Id
                && x.KyBaoCaoId == entity.KyBaoCaoId
                && x.DonViId == entity.DonViId
                && (x.TrangThai == SnapshotStatus.Submitted || x.TrangThai == SnapshotStatus.Locked), cancellationToken);
        if (hasActiveSnapshot)
        {
            throw new AppException("SNAPSHOT_ALREADY_SUBMITTED", "Kỳ báo cáo này đã nộp. Vui lòng hủy nộp trước khi nộp lại.", 422);
        }

        var now = _dateTimeProvider.Now;
        var currentUser = _currentUserService.GetCurrentUser();

        entity.TrangThai = SnapshotStatus.Submitted;
        entity.SubmittedAt = now;
        entity.SubmittedBy = currentUser.UserId;
        entity.LockedAt = now;
        entity.LockedBy = currentUser.UserId;
        entity.GhiChu = request.GhiChu;

        var kyTrangThaiDonVi = await _dbContext.KyTrangThaiDonVis.FirstOrDefaultAsync(
            x => x.KyBaoCaoId == entity.KyBaoCaoId && x.DonViId == entity.DonViId,
            cancellationToken);
        if (kyTrangThaiDonVi is not null)
        {
            kyTrangThaiDonVi.TrangThai = KyTrangThaiDonViStatus.DaNop;
            kyTrangThaiDonVi.NgayXacNhan = now;
            kyTrangThaiDonVi.ConfirmedBy = currentUser.UserId;
        }

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

        entity.TrangThai = SnapshotStatus.Locked;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await GeneratePdfAsync(entity.Id, cancellationToken);

        return await GetAsync(id, cancellationToken) ?? throw new InvalidOperationException();
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
            var totalRows = await CopyLiveToHisAsync(kyCode, request.DonViId, batch.Id, now, currentUser.UserId, cancellationToken);
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
        var entity = await _dbContext.BaoCaoSnapshots.FirstAsync(x => x.Id == id, cancellationToken);
        if (entity.TrangThai == SnapshotStatus.Draft)
            throw new InvalidOperationException("Chỉ được xuất PDF từ snapshot đã nộp.");
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

    private static System.Linq.Expressions.Expression<Func<BaoCaoSnapshotEntity, BaoCaoSnapshotDto>> ToDto() => entity => new BaoCaoSnapshotDto
    {
        Id = entity.Id,
        KyBaoCaoId = entity.KyBaoCaoId,
        DonViId = entity.DonViId,
        TrangThai = entity.TrangThai,
        PhienBan = entity.PhienBan,
        GhiChu = entity.GhiChu,
        SubmittedAt = entity.SubmittedAt,
        LockedAt = entity.LockedAt
    };

    private async Task<int> CopyLiveToHisAsync(
        string kyCode, long donViId, long batchId, DateTime now, long? userId,
        CancellationToken ct)
    {
        int total = 0;

        // DAO_TAO_BOI_DUONG
        if (!await _dbContext.DaoTaoBoiDuongHis.AnyAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, ct))
        {
            var live = await _dbContext.DaoTaoBoiDuongs.Where(x => x.DonViId == donViId).ToListAsync(ct);
            await _dbContext.DaoTaoBoiDuongHis.AddRangeAsync(live.Select(r => new DaoTaoBoiDuongHis
            {
                SourceId = r.Id, DonViId = r.DonViId, TenKhoaHoc = r.TenKhoaHoc,
                DonViToChuc = r.DonViToChuc, HinhThuc = r.HinhThuc, SoLuongHv = r.SoLuongHv,
                ThoiGianTu = r.ThoiGianTu, ThoiGianDen = r.ThoiGianDen, GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode, SnapshotBatchId = batchId, SnapshotCreatedAt = now,
                CreatedBy = userId, UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // DAO_TAO_HOC_VIEN
        if (!await _dbContext.DaoTaoHocVienHis.AnyAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, ct))
        {
            var live = await _dbContext.DaoTaoHocViens.Where(x => x.DonViId == donViId).ToListAsync(ct);
            await _dbContext.DaoTaoHocVienHis.AddRangeAsync(live.Select(r => new DaoTaoHocVienHis
            {
                SourceId = r.Id, DonViId = r.DonViId, Nam = r.Nam, NoiDungDaoTao = r.NoiDungDaoTao,
                SoTienSi = r.SoTienSi, SoThacSi = r.SoThacSi, SoDaiHoc = r.SoDaiHoc,
                SoCaoDang = r.SoCaoDang, SoTrungCap = r.SoTrungCap, GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode, SnapshotBatchId = batchId, SnapshotCreatedAt = now,
                CreatedBy = userId, UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // NANG_LUC_SO
        if (!await _dbContext.NangLucSoHis.AnyAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, ct))
        {
            var live = await _dbContext.NangLucSos.Where(x => x.DonViId == donViId).ToListAsync(ct);
            await _dbContext.NangLucSoHis.AddRangeAsync(live.Select(r => new NangLucSoHis
            {
                SourceId = r.Id, DonViId = r.DonViId, NhomViTri = r.NhomViTri,
                TongSoDienDanhGia = r.TongSoDienDanhGia, TongSoDat = r.TongSoDat,
                TongSoChuaDat = r.TongSoChuaDat, GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode, SnapshotBatchId = batchId, SnapshotCreatedAt = now,
                CreatedBy = userId, UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // VAN_BAN_QPPL
        if (!await _dbContext.VanBanQpplHis.AnyAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, ct))
        {
            var live = await _dbContext.VanBanQppls.Where(x => x.DonViId == donViId).ToListAsync(ct);
            await _dbContext.VanBanQpplHis.AddRangeAsync(live.Select(r => new VanBanQpplHis
            {
                SourceId = r.Id, DonViId = r.DonViId, SoHieu = r.SoHieu, TenVanBan = r.TenVanBan,
                LoaiVanBan = r.LoaiVanBan, CoQuanBanHanh = r.CoQuanBanHanh, NgayBanHanh = r.NgayBanHanh,
                NgayHieuLuc = r.NgayHieuLuc, LinhVuc = r.LinhVuc, TrichYeu = r.TrichYeu,
                TinhTrangTrienKhai = r.TinhTrangTrienKhai, GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode, SnapshotBatchId = batchId, SnapshotCreatedAt = now,
                CreatedBy = userId, UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // DU_AN_CNTT
        if (!await _dbContext.DuAnCnttHis.AnyAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, ct))
        {
            var live = await _dbContext.DuAnCntts.Where(x => x.DonViId == donViId).ToListAsync(ct);
            await _dbContext.DuAnCnttHis.AddRangeAsync(live.Select(r => new DuAnCnttHis
            {
                SourceId = r.Id, DonViId = r.DonViId, TenDuAn = r.TenDuAn, DonViChuTri = r.DonViChuTri,
                NamTrienKhai = r.NamTrienKhai, NamDuaVaoSuDung = r.NamDuaVaoSuDung,
                TongKinhPhi = r.TongKinhPhi, NguonVon = r.NguonVon, GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode, SnapshotBatchId = batchId, SnapshotCreatedAt = now,
                CreatedBy = userId, UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // GIAM_SAT_SOC
        if (!await _dbContext.GiamSatSocHis.AnyAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, ct))
        {
            var live = await _dbContext.GiamSatSocs.Where(x => x.DonViId == donViId).ToListAsync(ct);
            await _dbContext.GiamSatSocHis.AddRangeAsync(live.Select(r => new GiamSatSocHis
            {
                SourceId = r.Id, DonViId = r.DonViId, LoaiMang = r.LoaiMang, LopGiamSat = r.LopGiamSat,
                CoHeThong = r.CoHeThong, ThucTrang = r.ThucTrang, NamThanhLap = r.NamThanhLap,
                SoNhanSu = r.SoNhanSu, CongCuSuDung = r.CongCuSuDung, SoCanhBaoThang = r.SoCanhBaoThang,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode, SnapshotBatchId = batchId, SnapshotCreatedAt = now,
                CreatedBy = userId, UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // GIAM_SAT_NOC
        if (!await _dbContext.GiamSatNocHis.AnyAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, ct))
        {
            var live = await _dbContext.GiamSatNocs.Where(x => x.DonViId == donViId).ToListAsync(ct);
            await _dbContext.GiamSatNocHis.AddRangeAsync(live.Select(r => new GiamSatNocHis
            {
                SourceId = r.Id, DonViId = r.DonViId, LopGiamSat = r.LopGiamSat,
                CoNoc = r.CoNoc, ThucTrang = r.ThucTrang, NamThanhLap = r.NamThanhLap,
                SoNhanSu = r.SoNhanSu, GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode, SnapshotBatchId = batchId, SnapshotCreatedAt = now,
                CreatedBy = userId, UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // ATTT_HTTT_VAN_HANH
        if (!await _dbContext.AtttHtttVanHanhHis.AnyAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, ct))
        {
            var live = await _dbContext.AtttHtttVanHanhs.Where(x => x.DonViId == donViId).ToListAsync(ct);
            await _dbContext.AtttHtttVanHanhHis.AddRangeAsync(live.Select(r => new AtttHtttVanHanhHis
            {
                SourceId = r.Id, DonViId = r.DonViId, HtttId = r.HtttId, ChuQuan = r.ChuQuan,
                DonViVanHanh = r.DonViVanHanh, CapDoDeXuat = r.CapDoDeXuat,
                TinhTrangPheDuyet = r.TinhTrangPheDuyet, QuyetDinhPheDuyet = r.QuyetDinhPheDuyet,
                QuyCheAttt = r.QuyCheAttt, DuKienNgayPheDuyet = r.DuKienNgayPheDuyet,
                DaTrienKhaiPhuongAn = r.DaTrienKhaiPhuongAn, DuKienNgayTrienKhai = r.DuKienNgayTrienKhai,
                KiemTraDanhGia = r.KiemTraDanhGia, GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode, SnapshotBatchId = batchId, SnapshotCreatedAt = now,
                CreatedBy = userId, UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // ATTT_HTTT_DAU_TU
        if (!await _dbContext.AtttHtttDauTuHis.AnyAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, ct))
        {
            var live = await _dbContext.AtttHtttDauTus.Where(x => x.DonViId == donViId).ToListAsync(ct);
            await _dbContext.AtttHtttDauTuHis.AddRangeAsync(live.Select(r => new AtttHtttDauTuHis
            {
                SourceId = r.Id, DonViId = r.DonViId, HtttId = r.HtttId, ChuQuan = r.ChuQuan,
                DonViVanHanh = r.DonViVanHanh, CapDoDeXuat = r.CapDoDeXuat,
                NgayPheDuyetHsdxcd = r.NgayPheDuyetHsdxcd, QuyetDinhPheDuyet = r.QuyetDinhPheDuyet,
                DaLongGhepThuyetMinh = r.DaLongGhepThuyetMinh, GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode, SnapshotBatchId = batchId, SnapshotCreatedAt = now,
                CreatedBy = userId, UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // CAMERA_QUAN_LY
        if (!await _dbContext.CameraQuanLyHis.AnyAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, ct))
        {
            var live = await _dbContext.CameraQuanLies.Where(x => x.DonViId == donViId).ToListAsync(ct);
            await _dbContext.CameraQuanLyHis.AddRangeAsync(live.Select(r => new CameraQuanLyHis
            {
                SourceId = r.Id, DonViId = r.DonViId, NhomCamera = r.NhomCamera,
                TenDonViDiaChi = r.TenDonViDiaChi, BuongGiamTrangBiSl = r.BuongGiamTrangBiSl,
                BuongGiamTrangBiTs = r.BuongGiamTrangBiTs, NhuCauDauTu = r.NhuCauDauTu,
                BaoTri = r.BaoTri, SuaChua = r.SuaChua, SoLanViPham = r.SoLanViPham,
                KetNoiChiaSe = r.KetNoiChiaSe, HoSoCapDoAttt = r.HoSoCapDoAttt,
                CbChuyenTrach = r.CbChuyenTrach, CbKiemNhiem = r.CbKiemNhiem,
                CbDiaPhuong = r.CbDiaPhuong, DaoTaoBo = r.DaoTaoBo, DaoTaoNhuCau = r.DaoTaoNhuCau,
                GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode, SnapshotBatchId = batchId, SnapshotCreatedAt = now,
                CreatedBy = userId, UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // CAMERA_THUC_TRANG
        if (!await _dbContext.CameraThucTrangHis.AnyAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, ct))
        {
            var live = await _dbContext.CameraThucTrangs.Where(x => x.DonViId == donViId).ToListAsync(ct);
            await _dbContext.CameraThucTrangHis.AddRangeAsync(live.Select(r => new CameraThucTrangHis
            {
                SourceId = r.Id, DonViId = r.DonViId, NhomCamera = r.NhomCamera,
                TenHeThong = r.TenHeThong, CauHinhIp = r.CauHinhIp, CauHinhAnalog = r.CauHinhAnalog,
                ThucTrangIp = r.ThucTrangIp, ThucTrangAnalog = r.ThucTrangAnalog,
                ChuDauTu = r.ChuDauTu, NamDauTu = r.NamDauTu, DuongTruyen = r.DuongTruyen,
                PhanMem = r.PhanMem, LuuTru = r.LuuTru, GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode, SnapshotBatchId = batchId, SnapshotCreatedAt = now,
                CreatedBy = userId, UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        // ATTT_GIAI_PHAP
        if (!await _dbContext.GiaiPhapAtttHis.AnyAsync(x => x.DonViId == donViId && x.KyBaoCaoCode == kyCode, ct))
        {
            var live = await _dbContext.GiaiPhapAttts.Where(x => x.DonViId == donViId).ToListAsync(ct);
            await _dbContext.GiaiPhapAtttHis.AddRangeAsync(live.Select(r => new GiaiPhapAtttHis
            {
                SourceId = r.Id, DonViId = r.DonViId, TenGiaiPhap = r.TenGiaiPhap,
                MayTinhBcanetSl = r.MayTinhBcanetSl, MayTinhBcanetTs = r.MayTinhBcanetTs,
                MayTinhInternetSl = r.MayTinhInternetSl, MayTinhInternetTs = r.MayTinhInternetTs,
                MayTinhLocalSl = r.MayTinhLocalSl, MayTinhLocalTs = r.MayTinhLocalTs,
                MayChuBcanetSl = r.MayChuBcanetSl, MayChuBcanetTs = r.MayChuBcanetTs,
                MayChuInternetSl = r.MayChuInternetSl, MayChuInternetTs = r.MayChuInternetTs,
                MayChuLocalSl = r.MayChuLocalSl, MayChuLocalTs = r.MayChuLocalTs, GhiChu = r.GhiChu,
                KyBaoCaoCode = kyCode, SnapshotBatchId = batchId, SnapshotCreatedAt = now,
                CreatedBy = userId, UpdatedBy = userId,
            }), ct);
            total += live.Count;
        }

        await _dbContext.SaveChangesAsync(ct);
        return total;
    }
}