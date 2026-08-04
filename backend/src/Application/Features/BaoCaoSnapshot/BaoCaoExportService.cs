using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Features.DonVi;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Enums;
using BaoCaoSnapshotEntity = ThucLuc.Domain.Entities.Reporting.BaoCaoSnapshot;
using BaoCaoFileEntity = ThucLuc.Domain.Entities.Reporting.BaoCaoFile;
using SnapshotBatchEntity = ThucLuc.Domain.Entities.Reporting.SnapshotBatch;

namespace ThucLuc.Application.Features.BaoCaoSnapshot;

public interface IBaoCaoExportService
{
    /// <summary>Xuất biểu mẫu báo cáo (theo mẫu H05) từ dữ liệu ĐÃ CHỐT của snapshot. format: xlsx | pdf.</summary>
    Task<BaoCaoExportResultDto> ExportAsync(long snapshotId, string format, CancellationToken ct = default);
}

public sealed class BaoCaoExportResultDto
{
    public long SnapshotId { get; set; }
    public string Format { get; set; } = "xlsx";
    public string FileName { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}

public sealed class BaoCaoExportService : IBaoCaoExportService
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Nâng version khi đổi layout biểu mẫu để bỏ qua file đã cache theo template cũ.</summary>
    private const int TemplateVersion = 6;
    private static readonly string FilePrefix = $"bieu-mau-t{TemplateVersion}";

    private const string HeaderFill = "#DCE6F1";
    private const string BandFill = "#F2F2F2";

    /// <summary>Thứ tự các mục trong sheet Tổng hợp — đúng thứ tự file "Bieu mau_Sua 18.3.docx".</summary>
    private static readonly string[] DocxModuleOrder =
    [
        "VAN_BAN_QPPL",
        "NHAN_LUC_CNTT",
        "DAO_TAO_BOI_DUONG",
        "DAO_TAO_HOC_VIEN",
        "NANG_LUC_SO",
        "THIET_BI_CNTT",
        "HA_TANG_MANG",
        "HE_THONG_THONG_TIN",
        "HTTT_TIEU_CHUAN",
        "GIAM_SAT_NOC",
        "GIAM_SAT_SOC",
        "ATTT_HTTT_VAN_HANH",
        "ATTT_HTTT_DAU_TU",
        "ATTT_GIAI_PHAP",
        "CAMERA_QUAN_LY",
        "CAMERA_THUC_TRANG",
        "DU_AN_CNTT",
    ];

    private readonly IApplicationDbContext _db;
    private readonly IDonViDataScopeService _donViDataScopeService;
    private readonly IDonViInputModeService _donViInputModeService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IPdfService _pdfService;
    private readonly ICurrentUserService _currentUserService;

    public BaoCaoExportService(
        IApplicationDbContext db,
        IDonViDataScopeService donViDataScopeService,
        IDonViInputModeService donViInputModeService,
        IFileStorageService fileStorageService,
        IPdfService pdfService,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _donViDataScopeService = donViDataScopeService;
        _donViInputModeService = donViInputModeService;
        _fileStorageService = fileStorageService;
        _pdfService = pdfService;
        _currentUserService = currentUserService;
    }

    public async Task<BaoCaoExportResultDto> ExportAsync(long snapshotId, string format, CancellationToken ct = default)
    {
        var normalizedFormat = (format ?? "xlsx").Trim().ToLowerInvariant();
        if (normalizedFormat is not ("xlsx" or "pdf"))
        {
            throw new AppException("EXPORT_FORMAT_INVALID", "Định dạng xuất chỉ hỗ trợ xlsx hoặc pdf.", 400);
        }

        var snapshot = await _db.BaoCaoSnapshots
            .AsNoTracking()
            .Include(x => x.KyBaoCao)!.ThenInclude(k => k!.MauBaoCao)
            .Include(x => x.DonVi)
            .FirstOrDefaultAsync(x => x.Id == snapshotId, ct)
            ?? throw new AppException("SNAPSHOT_NOT_FOUND", "Không tìm thấy snapshot.", 404);

        var scope = await _donViDataScopeService.GetScopeAsync(ct);
        if (!scope.Contains(snapshot.DonViId))
        {
            throw new AppException("SNAPSHOT_NOT_FOUND", "Không tìm thấy snapshot.", 404);
        }

        if (snapshot.TrangThai == SnapshotStatus.Draft)
        {
            throw new AppException("SNAPSHOT_NOT_SUBMITTED", "Chỉ xuất biểu mẫu từ báo cáo đã nộp.", 422);
        }

        var mime = normalizedFormat == "xlsx" ? XlsxMime : "application/pdf";
        var cachedPrefix = $"{FilePrefix}-{snapshot.Id}";
        var cached = await _db.BaoCaoFiles
            .Where(x => x.BaoCaoSnapshotId == snapshot.Id
                     && x.MimeType == mime
                     && x.FileName.StartsWith(cachedPrefix))
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        if (cached is not null)
        {
            return new BaoCaoExportResultDto
            {
                SnapshotId = snapshot.Id,
                Format = normalizedFormat,
                FileName = cached.FileName,
                DownloadUrl = await _fileStorageService.GetPresignedDownloadUrlAsync(cached.FilePath, TimeSpan.FromMinutes(15), ct),
            };
        }

        // PDF: chỉ convert sheet TỔNG HỢP (bản in); Excel: đủ các sheet chi tiết
        byte[] outputBytes;
        string extension;
        if (normalizedFormat == "pdf")
        {
            var tongHopOnly = await BuildWorkbookAsync(snapshot, tongHopOnly: true, ct);
            outputBytes = await _pdfService.ConvertOfficeToPdfAsync(tongHopOnly, $"{cachedPrefix}.xlsx", ct);
            extension = "pdf";
        }
        else
        {
            outputBytes = await BuildWorkbookAsync(snapshot, tongHopOnly: false, ct);
            extension = "xlsx";
        }

        var fileName = $"{cachedPrefix}-{snapshot.KyBaoCao?.KyCode ?? "ky"}-v{snapshot.PhienBan}.{extension}";
        var objectKey = $"{snapshot.DonViId}/{snapshot.KyBaoCaoId}/bieu-mau/{Guid.NewGuid():N}.{extension}";
        await using var stream = new MemoryStream(outputBytes);
        var filePath = await _fileStorageService.UploadAsync(objectKey, stream, mime, ct);

        var currentUser = _currentUserService.GetCurrentUser();
        _db.BaoCaoFiles.Add(new BaoCaoFileEntity
        {
            BaoCaoSnapshotId = snapshot.Id,
            FileName = fileName,
            FilePath = filePath,
            MimeType = mime,
            FileSize = outputBytes.Length,
            CreatedBy = currentUser.UserId,
            UpdatedBy = currentUser.UserId,
        });
        await _db.SaveChangesAsync(ct);

        return new BaoCaoExportResultDto
        {
            SnapshotId = snapshot.Id,
            Format = normalizedFormat,
            FileName = fileName,
            DownloadUrl = await _fileStorageService.GetPresignedDownloadUrlAsync(filePath, TimeSpan.FromMinutes(15), ct),
        };
    }

    // ==================================================================
    // Dữ liệu chung cho mọi sheet
    // ==================================================================
    private sealed record NhanLucRow(
        long DonViId,
        string HoTen,
        DateOnly? NgaySinh,
        string? CapBac,
        string? ChucVu,
        string? DienThoai,
        string? LoaiNhanLuc,
        string? TrinhDoCntt,
        string? TrinhDoLlct);

    private sealed record NlsGroup(string Nhom, int DienDanhGia, int Dat, int ChuaDat, string GhiChu);

    private sealed class ExportData
    {
        public required BaoCaoSnapshotEntity Snapshot { get; init; }
        public required DonViInputModeContext ModeContext { get; init; }
        public required IReadOnlyCollection<string> ModuleList { get; init; }
        public required IReadOnlyDictionary<string, Dictionary<string, string>> CodeLabels { get; init; }
        public required IReadOnlyDictionary<long, string> DonViNames { get; init; }
        public required List<NhanLucRow> NhanLuc { get; init; }
        public required List<NlsGroup> NangLucSo { get; init; }

        public bool HasModule(string code) => ModuleList.Count == 0 || ModuleList.Contains(code);
    }

    private async Task<byte[]> BuildWorkbookAsync(BaoCaoSnapshotEntity snapshot, bool tongHopOnly, CancellationToken ct)
    {
        var modeContext = await _donViInputModeService.GetContextAsync(snapshot.DonViId, ct);
        var targetDonViIds = modeContext.IsTongHop
            ? modeContext.AggregateDonViIds.ToArray()
            : new[] { snapshot.DonViId };

        var batch = await ResolveBatchAsync(snapshot, ct);
        var asOf = batch?.FinishedAt ?? snapshot.SubmittedAt ?? snapshot.LockedAt ?? DateTime.Now;
        var kyCode = snapshot.KyBaoCao?.KyCode ?? string.Empty;

        var data = new ExportData
        {
            Snapshot = snapshot,
            ModeContext = modeContext,
            ModuleList = ParseModuleList(snapshot.KyBaoCao?.MauBaoCao?.DanhSachModule),
            CodeLabels = await LoadCodeLabelsAsync(ct),
            DonViNames = await _db.DonVis
                .AsNoTracking()
                .Select(x => new { x.Id, x.TenDonVi })
                .ToDictionaryAsync(x => x.Id, x => x.TenDonVi, ct),
            NhanLuc = await LoadNhanLucAsync(targetDonViIds, asOf, ct),
            NangLucSo = await LoadNangLucSoAsync(targetDonViIds, kyCode, ct),
        };

        using var workbook = new XLWorkbook();

        // Sheet TỔNG HỢP luôn đứng đầu — layout dọc như file docx, in được ngay
        BuildTongHopSheet(workbook, data);

        if (!tongHopOnly)
        {
            if (data.HasModule("NHAN_LUC_CNTT"))
            {
                var ws = AddSheet(workbook, "NHÂN LỰC CNTT", landscape: true);
                SetNhanLucColumnWidths(ws);
                var row = WriteTitleBlock(ws, 1, data.Snapshot, totalCols: 9);
                WriteNhanLucSection(ws, row + 1, data);
                ws.SheetView.FreezeRows(row + 2);
            }

            if (data.HasModule("NANG_LUC_SO"))
            {
                var ws = AddSheet(workbook, "NĂNG LỰC SỐ", landscape: false);
                SetNangLucSoColumnWidths(ws);
                var row = WriteTitleBlock(ws, 1, data.Snapshot, totalCols: 5);
                WriteNangLucSoSection(ws, row + 1, data);
            }
        }

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    // ==================================================================
    // Sheet TỔNG HỢP — layout dọc như file docx, in được ngay
    // ==================================================================
    private void BuildTongHopSheet(XLWorkbook workbook, ExportData data)
    {
        const int totalCols = 9; // theo bảng rộng nhất hiện có (Nhân lực CNTT)

        var ws = AddSheet(workbook, "TỔNG HỢP", landscape: true);
        SetNhanLucColumnWidths(ws);

        var row = WriteTitleBlock(ws, 1, data.Snapshot, totalCols);
        row++; // dòng trống

        // Thông tin đơn vị + tên báo cáo LUÔN hiển thị
        row = WriteThongTinDonViSection(ws, row, data, totalCols);
        row++;

        // Các module theo đúng thứ tự docx, chỉ hiện module thuộc mẫu báo cáo
        foreach (var code in DocxModuleOrder)
        {
            if (!data.HasModule(code))
            {
                continue;
            }

            switch (code)
            {
                case "NHAN_LUC_CNTT":
                    row = WriteNhanLucSection(ws, row, data);
                    row++;
                    break;
                case "NANG_LUC_SO":
                    row = WriteNangLucSoSection(ws, row, data, wideGrid: true);
                    row++;
                    break;
                // Các biểu còn lại bổ sung dần theo cùng pattern
                default:
                    break;
            }
        }
    }

    // ==================================================================
    // Section writers — ghi vào (ws, startRow), trả về dòng kế tiếp
    // ==================================================================
    private static int WriteTitleBlock(IXLWorksheet ws, int row, BaoCaoSnapshotEntity snapshot, int totalCols)
    {
        ws.Cell(row, 1).Value = "PHỤ LỤC";
        ws.Range(row, 1, row, totalCols).Merge().Style
            .Font.SetBold().Font.SetFontSize(14)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        row++;

        ws.Cell(row, 1).Value = "THỐNG KÊ SỐ LIỆU VỀ ỨNG DỤNG, PHÁT TRIỂN CÔNG NGHỆ THÔNG TIN";
        ws.Range(row, 1, row, totalCols).Merge().Style
            .Font.SetBold()
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        row++;

        ws.Cell(row, 1).Value =
            $"Kỳ báo cáo: {snapshot.KyBaoCao?.TenKy ?? snapshot.KyBaoCao?.KyCode} — Đơn vị: {snapshot.DonVi?.TenDonVi} — Phiên bản nộp: v{snapshot.PhienBan} — Nộp lúc: {snapshot.SubmittedAt:dd/MM/yyyy HH:mm}";
        ws.Range(row, 1, row, totalCols).Merge().Style
            .Font.SetItalic()
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        row++;

        return row;
    }

    private static int WriteThongTinDonViSection(IXLWorksheet ws, int row, ExportData data, int totalCols)
    {
        var donVi = data.Snapshot.DonVi;
        var infoRows = new (string Label, string? Value)[]
        {
            ("Tên đơn vị", donVi?.TenDonVi),
            ("Địa chỉ Đơn vị", donVi?.DiaChi),
            ("Website nội bộ", donVi?.WebsiteNoiBo),
            ("Website Internet", donVi?.WebsiteInternet),
            ("Tổng biên chế", donVi?.TongBienChe?.ToString()),
            ("Số lượng đơn vị trực thuộc (theo cây)", data.ModeContext.DescendantDonViIds.Count.ToString()),
            ("Chế độ nhập liệu", data.ModeContext.IsTongHop ? "Tổng hợp từ đơn vị cấp dưới" : "Tự nhập"),
        };

        var start = row;
        var labelEnd = Math.Min(3, totalCols - 1);

        ws.Cell(row, 1).Value = "THÔNG TIN ĐƠN VỊ";
        ws.Range(row, 1, row, totalCols).Merge().Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderFill));
        row++;

        foreach (var (label, value) in infoRows)
        {
            ws.Range(row, 1, row, labelEnd).Merge();
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.SetBold();
            ws.Range(row, labelEnd + 1, row, totalCols).Merge();
            ws.Cell(row, labelEnd + 1).Value = value ?? string.Empty;
            row++;
        }

        ApplyTableBorders(ws, start, 1, row - 1, totalCols);
        return row;
    }

    private int WriteNhanLucSection(IXLWorksheet ws, int row, ExportData data)
    {
        const int cols = 9;
        var start = row;

        ws.Cell(row, 1).Value = "NHÂN LỰC CÔNG NGHỆ THÔNG TIN";
        ws.Range(row, 1, row, cols).Merge().Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderFill));
        row++;

        var headers = new[]
        {
            "TT", "Họ và tên", "Ngày, tháng, năm sinh", "Cấp bậc", "Chức vụ",
            "Đơn vị", "Điện thoại liên hệ", "Trình độ công nghệ thông tin", "Trình độ lý luận chính trị",
        };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(row, c + 1).Value = headers[c];
        }

        ws.Range(row, 1, row, cols).Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderFill))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Alignment.SetWrapText(true);
        row++;

        var groups = data.NhanLuc
            .GroupBy(x => x.LoaiNhanLuc ?? string.Empty)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in groups)
        {
            var groupLabel = Translate(data.CodeLabels, "LOAI_NHAN_LUC", group.Key);
            ws.Cell(row, 1).Value = string.IsNullOrWhiteSpace(groupLabel) ? "Chưa phân loại" : groupLabel;
            ws.Range(row, 1, row, cols).Merge().Style
                .Font.SetBold().Font.SetItalic()
                .Fill.SetBackgroundColor(XLColor.FromHtml(BandFill));
            row++;

            var stt = 1;
            foreach (var item in group)
            {
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = item.HoTen;
                ws.Cell(row, 3).Value = item.NgaySinh?.ToString("dd/MM/yyyy") ?? string.Empty;
                ws.Cell(row, 4).Value = Translate(data.CodeLabels, "CAP_BAC_CONG_AN", item.CapBac);
                ws.Cell(row, 5).Value = item.ChucVu ?? string.Empty;
                ws.Cell(row, 6).Value = data.DonViNames.GetValueOrDefault(item.DonViId, string.Empty);
                ws.Cell(row, 7).Value = item.DienThoai ?? string.Empty;
                ws.Cell(row, 8).Value = Translate(data.CodeLabels, "TRINH_DO_CNTT", item.TrinhDoCntt);
                ws.Cell(row, 9).Value = Translate(data.CodeLabels, "TRINH_DO_LLCT", item.TrinhDoLlct);
                ws.Cell(row, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                row++;
            }
        }

        if (data.NhanLuc.Count == 0)
        {
            ws.Cell(row, 1).Value = "Không có dữ liệu";
            ws.Range(row, 1, row, cols).Merge().Style
                .Font.SetItalic()
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row++;
        }

        ApplyTableBorders(ws, start, 1, row - 1, cols);
        return row;
    }

    private int WriteNangLucSoSection(IXLWorksheet ws, int row, ExportData data, bool wideGrid = false)
    {
        // wideGrid (sheet Tổng hợp): map 5 cột nghiệp vụ lên lưới 9 cột của Nhân lực
        // bằng merge để tên nhóm/ghi chú đủ rộng, không gãy chữ dọc.
        var spans = wideGrid
            ? new (int S, int E)[] { (1, 3), (4, 4), (5, 5), (6, 6), (7, 9) }
            : new (int S, int E)[] { (1, 1), (2, 2), (3, 3), (4, 4), (5, 5) };
        var lastCol = spans[^1].E;
        var start = row;

        IXLCell Put(int r, int index, XLCellValue value)
        {
            var (s, e) = spans[index];
            if (s != e)
            {
                ws.Range(r, s, r, e).Merge();
            }

            var cell = ws.Cell(r, s);
            cell.Value = value;
            return cell;
        }

        ws.Cell(row, 1).Value = "PHÁT TRIỂN NGUỒN NHÂN LỰC — NĂNG LỰC SỐ THEO NHÓM VỊ TRÍ";
        ws.Range(row, 1, row, lastCol).Merge().Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderFill));
        row++;

        var headers = new[]
        {
            "Nhóm", "Tổng số cán bộ thuộc diện đánh giá", "Tổng số cán bộ đạt năng lực số",
            "Tổng số cán bộ chưa đạt năng lực số", "Ghi chú",
        };
        for (var c = 0; c < headers.Length; c++)
        {
            Put(row, c, headers[c]);
        }

        ws.Range(row, 1, row, lastCol).Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderFill))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Alignment.SetWrapText(true);
        row++;

        foreach (var item in data.NangLucSo)
        {
            var nhomCell = Put(row, 0, Translate(data.CodeLabels, "NHOM_NANG_LUC_SO", item.Nhom));
            nhomCell.Style.Alignment.SetWrapText(true);
            Put(row, 1, item.DienDanhGia).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            Put(row, 2, item.Dat).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            Put(row, 3, item.ChuaDat).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            Put(row, 4, item.GhiChu);
            row++;
        }

        if (data.NangLucSo.Count > 0)
        {
            Put(row, 0, "TỔNG CỘNG").Style.Font.SetBold();
            Put(row, 1, data.NangLucSo.Sum(x => x.DienDanhGia)).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            Put(row, 2, data.NangLucSo.Sum(x => x.Dat)).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            Put(row, 3, data.NangLucSo.Sum(x => x.ChuaDat)).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            Put(row, 4, string.Empty);
            row++;
        }
        else
        {
            ws.Cell(row, 1).Value = "Không có dữ liệu";
            ws.Range(row, 1, row, lastCol).Merge().Style
                .Font.SetItalic()
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row++;
        }

        ApplyTableBorders(ws, start, 1, row - 1, lastCol);
        return row;
    }

    // ==================================================================
    // Data loaders
    // ==================================================================
    private async Task<List<NhanLucRow>> LoadNhanLucAsync(long[] targetDonViIds, DateTime asOf, CancellationToken ct)
    {
        // As-of thời điểm chốt: live có hiệu lực trước asOf + version cũ còn hiệu lực tại asOf
        var live = await _db.NhanLucCntts
            .AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.ValidFrom <= asOf)
            .ToListAsync(ct);
        var his = await _db.NhanLucCnttHis
            .AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.ValidFrom <= asOf && x.ValidTo > asOf)
            .ToListAsync(ct);

        return live
            .Select(x => new NhanLucRow(x.DonViId, x.HoTen, x.NgaySinh, x.CapBac, x.ChucVu, x.DienThoai, x.LoaiNhanLuc, x.TrinhDoCntt, x.TrinhDoLlct))
            .Concat(his.Select(x => new NhanLucRow(x.DonViId, x.HoTen, x.NgaySinh, x.CapBac, x.ChucVu, x.DienThoai, x.LoaiNhanLuc, x.TrinhDoCntt, x.TrinhDoLlct)))
            .OrderBy(x => x.DonViId)
            .ThenBy(x => x.HoTen, StringComparer.Create(new System.Globalization.CultureInfo("vi-VN"), false))
            .ToList();
    }

    private async Task<List<NlsGroup>> LoadNangLucSoAsync(long[] targetDonViIds, string kyCode, CancellationToken ct)
    {
        // His theo (KyBaoCaoCode, DonViId) — mỗi cặp chỉ giữ 1 bộ chốt gần nhất.
        // Chưa từng chốt thì fallback live.
        var raw = (await _db.NangLucSoHis
            .AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .ToListAsync(ct))
            .Select(x => (x.NhomViTri, x.TongSoDienDanhGia, x.TongSoDat, x.TongSoChuaDat, x.GhiChu))
            .ToList();

        if (raw.Count == 0)
        {
            raw = (await _db.NangLucSos
                .AsNoTracking()
                .Where(x => targetDonViIds.Contains(x.DonViId))
                .ToListAsync(ct))
                .Select(x => (x.NhomViTri, x.TongSoDienDanhGia, x.TongSoDat, x.TongSoChuaDat, x.GhiChu))
                .ToList();
        }

        return raw
            .GroupBy(x => x.NhomViTri, StringComparer.OrdinalIgnoreCase)
            .Select(g => new NlsGroup(
                g.Key,
                g.Sum(x => x.TongSoDienDanhGia),
                g.Sum(x => x.TongSoDat),
                g.Sum(x => x.TongSoChuaDat),
                string.Join("; ", g.Select(x => x.GhiChu).Where(x => !string.IsNullOrWhiteSpace(x)))))
            .OrderBy(x => x.Nhom, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ==================================================================
    // Helpers
    // ==================================================================
    private static IXLWorksheet AddSheet(XLWorkbook workbook, string name, bool landscape)
    {
        var ws = workbook.Worksheets.Add(name);
        ws.PageSetup.PageOrientation = landscape ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.Margins.Top = 0.5;
        ws.PageSetup.Margins.Bottom = 0.5;
        ws.PageSetup.Margins.Left = 0.4;
        ws.PageSetup.Margins.Right = 0.4;
        ws.Style.Font.SetFontName("Times New Roman").Font.SetFontSize(11);
        ws.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        return ws;
    }

    private static void SetNhanLucColumnWidths(IXLWorksheet ws)
    {
        ws.Column(1).Width = 5;
        ws.Column(2).Width = 24;
        ws.Column(3).Width = 14;
        ws.Column(4).Width = 14;
        ws.Column(5).Width = 20;
        ws.Column(6).Width = 24;
        ws.Column(7).Width = 14;
        ws.Column(8).Width = 18;
        ws.Column(9).Width = 18;
    }

    private static void SetNangLucSoColumnWidths(IXLWorksheet ws)
    {
        ws.Column(1).Width = 50;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 16;
        ws.Column(4).Width = 16;
        ws.Column(5).Width = 28;
    }

    private static void ApplyTableBorders(IXLWorksheet ws, int r1, int c1, int r2, int c2)
    {
        var range = ws.Range(r1, c1, r2, c2);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private async Task<SnapshotBatchEntity?> ResolveBatchAsync(BaoCaoSnapshotEntity snapshot, CancellationToken ct)
    {
        var query = _db.SnapshotBatches
            .AsNoTracking()
            .Where(x => x.KyBaoCaoId == snapshot.KyBaoCaoId && x.DonViId == snapshot.DonViId && x.Status == "SUCCEEDED");

        if (snapshot.SubmittedAt.HasValue)
        {
            query = query.Where(x => x.FinishedAt != null && x.FinishedAt <= snapshot.SubmittedAt);
        }

        return await query
            .OrderByDescending(x => x.FinishedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<IReadOnlyDictionary<string, Dictionary<string, string>>> LoadCodeLabelsAsync(CancellationToken ct)
    {
        var keys = new[] { "CAP_BAC_CONG_AN", "TRINH_DO_CNTT", "TRINH_DO_LLCT", "LOAI_NHAN_LUC", "NHOM_NANG_LUC_SO" };
        var values = await _db.CodeValues
            .AsNoTracking()
            .Where(v => v.Code != null && keys.Contains(v.Code.CodeKey))
            .Select(v => new { v.Code!.CodeKey, v.Value, v.Name })
            .ToListAsync(ct);

        return values
            .GroupBy(x => x.CodeKey)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First().Name, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string Translate(
        IReadOnlyDictionary<string, Dictionary<string, string>> codeLabels,
        string codeKey,
        string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        return codeLabels.TryGetValue(codeKey, out var map) && map.TryGetValue(rawValue, out var name)
            ? name
            : rawValue;
    }

    private static IReadOnlyCollection<string> ParseModuleList(string? json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? [];
        }
        catch
        {
            return [];
        }
    }
}
