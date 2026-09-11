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
    public string PreviewUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}

public sealed class BaoCaoExportService : IBaoCaoExportService
{
    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Nâng version khi đổi layout biểu mẫu để bỏ qua file đã cache theo template cũ.</summary>
    private const int TemplateVersion = 15;
    private static readonly string FilePrefix = $"bieu-mau-t{TemplateVersion}";

    private const string HeaderFill = "#DCE6F1";
    private const string BandFill = "#F2F2F2";

    // Tất cả section trong sheet TỔNG HỢP dùng chung một lưới vật lý.
    // Mỗi cột nghiệp vụ sẽ chiếm một hoặc nhiều cột vật lý (merge theo chiều ngang),
    // nhờ đó bảng ít/nhiều cột vẫn thẳng mép và không ghi đè độ rộng của nhau.
    private const int SummaryGridColumns = 30;
    private const double SummaryGridColumnWidth = 5.2d;

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
            var cachedFriendlyName = BuildReadableFileName(snapshot, normalizedFormat == "pdf" ? "pdf" : "xlsx");
            var cachedPreviewUrl = await _fileStorageService.GetPresignedDownloadUrlAsync(cached.FilePath, TimeSpan.FromMinutes(15), ct);
            return new BaoCaoExportResultDto
            {
                SnapshotId = snapshot.Id,
                Format = normalizedFormat,
                FileName = cachedFriendlyName,
                PreviewUrl = cachedPreviewUrl,
                DownloadUrl = await _fileStorageService.GetPresignedDownloadUrlAsync(cached.FilePath, TimeSpan.FromMinutes(15), ct, cachedFriendlyName),
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

        // FileName lưu DB dùng làm khóa cache theo TemplateVersion; tên hiển thị/tải về dùng bản thân thiện riêng.
        var fileName = $"{cachedPrefix}-{snapshot.KyBaoCao?.KyCode ?? "ky"}-v{snapshot.PhienBan}.{extension}";
        var friendlyFileName = BuildReadableFileName(snapshot, extension);
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
            FileName = friendlyFileName,
            PreviewUrl = await _fileStorageService.GetPresignedDownloadUrlAsync(filePath, TimeSpan.FromMinutes(15), ct),
            DownloadUrl = await _fileStorageService.GetPresignedDownloadUrlAsync(filePath, TimeSpan.FromMinutes(15), ct, friendlyFileName),
        };
    }

    /// <summary>Tên file thân thiện khi tải về, ví dụ: Bao-cao-Du-an-CNTT_Cong-an-TP-Ha-Noi_2026Q3_DU_AN_v3.xlsx</summary>
    private static string BuildReadableFileName(BaoCaoSnapshotEntity snapshot, string extension)
    {
        var tenMau = Slugify(snapshot.KyBaoCao?.MauBaoCao?.TenMau ?? snapshot.KyBaoCao?.TenKy ?? "Bao-cao");
        var tenDonVi = Slugify(snapshot.DonVi?.TenDonVi ?? string.Empty);
        var kyCode = snapshot.KyBaoCao?.KyCode ?? "ky";
        var parts = new[] { "Bao-cao", tenMau, tenDonVi, kyCode, $"v{snapshot.PhienBan}" }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        return $"{string.Join('_', parts)}.{extension}";
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var ascii = sb.ToString().Normalize(System.Text.NormalizationForm.FormC)
            .Replace('Đ', 'D').Replace('đ', 'd');
        var cleaned = System.Text.RegularExpressions.Regex.Replace(ascii, "[^a-zA-Z0-9]+", "-").Trim('-');
        return cleaned;
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
        public required int SoDonViCapPhong { get; init; }
        public required int SoDonViCapXa { get; init; }
        public required IReadOnlyCollection<string> ModuleList { get; init; }
        public required IReadOnlyDictionary<string, Dictionary<string, string>> CodeLabels { get; init; }
        public required IReadOnlyDictionary<long, string> DonViNames { get; init; }
        public required List<NhanLucRow> NhanLuc { get; init; }
        public required List<NlsGroup> NangLucSo { get; init; }

        // Các module còn lại — mỗi hàng đã format sẵn theo đúng thứ tự cột hiển thị (không gồm cột TT).
        public required Dictionary<string, List<object?[]>> ModuleRows { get; init; }

        // Module hiển thị dạng bảng có nhóm băng (band) — key = module code, value = danh sách (nhãn nhóm, các hàng trong nhóm).
        public required Dictionary<string, List<(string Label, List<object?[]> Rows)>> ModuleGroupedRows { get; init; }

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

        var codeLabels = await LoadCodeLabelsAsync(ct);

        var data = new ExportData
        {
            Snapshot = snapshot,
            ModeContext = modeContext,
            SoDonViCapPhong = await CountDirectChildrenAsync(snapshot.DonViId, isCapPhong: true, ct),
            SoDonViCapXa = await CountDirectChildrenAsync(snapshot.DonViId, isCapPhong: false, ct),
            ModuleList = ParseModuleList(snapshot.KyBaoCao?.MauBaoCao?.DanhSachModule),
            CodeLabels = codeLabels,
            DonViNames = await _db.DonVis
                .AsNoTracking()
                .Select(x => new { x.Id, x.TenDonVi })
                .ToDictionaryAsync(x => x.Id, x => x.TenDonVi, ct),
            NhanLuc = await LoadNhanLucAsync(targetDonViIds, asOf, ct),
            NangLucSo = await LoadNangLucSoAsync(targetDonViIds, kyCode, ct),
            ModuleRows = await LoadModuleRowsAsync(targetDonViIds, kyCode, asOf, codeLabels, ct),
            ModuleGroupedRows = await LoadGroupedModuleRowsAsync(targetDonViIds, kyCode, asOf, codeLabels, ct),
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
                WriteNhanLucSection(ws, row + 1, data, totalCols: 9);
                ws.SheetView.FreezeRows(row + 2);
            }

            if (data.HasModule("NANG_LUC_SO"))
            {
                var ws = AddSheet(workbook, "NĂNG LỰC SỐ", landscape: false);
                SetNangLucSoColumnWidths(ws);
                var row = WriteTitleBlock(ws, 1, data.Snapshot, totalCols: 5);
                WriteNangLucSoSection(ws, row + 1, data, totalCols: 5);
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
        const int totalCols = SummaryGridColumns;

        var ws = AddSheet(workbook, "TỔNG HỢP", landscape: true);
        SetSummaryGridColumnWidths(ws, totalCols);

        var row = WriteTitleBlock(ws, 1, data.Snapshot, totalCols);
        row++; // dòng trống

        // Thông tin đơn vị + tên báo cáo LUÔN hiển thị
        row = WriteThongTinDonViSection(ws, row, data, totalCols);
        row++;

        // Các module theo đúng thứ tự docx, chỉ hiện module thuộc mẫu báo cáo
        var sectionNumber = 2;
        foreach (var code in DocxModuleOrder)
        {
            if (code == "HE_THONG_THONG_TIN")
            {
                if (data.HasModule("HE_THONG_THONG_TIN") || data.HasModule("HTTT_TIEU_CHUAN"))
                {
                    row = WriteHeThongThongTinSection(ws, row, data, totalCols, sectionNumber++);
                    row++;
                }

                continue;
            }

            // HTTT tiêu chuẩn là tiểu mục (3) của phần Hệ thống thông tin,
            // đã được ghi cùng HE_THONG_THONG_TIN ở nhánh phía trên.
            if (code == "HTTT_TIEU_CHUAN")
            {
                continue;
            }

            if (!data.HasModule(code))
            {
                continue;
            }

            switch (code)
            {
                case "NHAN_LUC_CNTT":
                    row = WriteNhanLucSection(ws, row, data, totalCols, sectionNumber++);
                    row++;
                    break;
                case "NANG_LUC_SO":
                    row = WriteNangLucSoSection(ws, row, data, totalCols, sectionNumber++);
                    row++;
                    break;
                default:
                    if (GroupedModuleDefs.TryGetValue(code, out var groupedDef) && data.ModuleGroupedRows.TryGetValue(code, out var groups))
                    {
                        row = WriteGridSectionGrouped(ws, row, $"{sectionNumber++}. {groupedDef.Title}", groupedDef.Headers, groups, totalCols);
                        row++;
                    }
                    else if (ModuleGridDefs.TryGetValue(code, out var def) && data.ModuleRows.TryGetValue(code, out var rows))
                    {
                        row = WriteGridSection(ws, row, $"{sectionNumber++}. {def.Title}", def.Headers, rows, totalCols);
                        row++;
                    }
                    break;
            }
        }
    }

    // ==================================================================
    // Định nghĩa tiêu đề + cột cho các module dạng bảng phẳng (grid)
    // ==================================================================
    private sealed record ModuleGridDef(string Title, string[] Headers);

    private static readonly string[] HtttDungChungHeaders =
    [
        "TT",
        "Tên phần mềm, CSDL",
        "Đơn vị nghiệp vụ quản lý, sử dụng",
        "Năm triển khai/Năm đưa vào sử dụng",
        "Ứng dụng công nghệ mới, hiện đại",
        "Phạm vi hoạt động trên hệ thống mạng",
        "Khả năng tích hợp, kết nối liên thông, chia sẻ, dùng chung",
        "Ghi chú",
    ];

    private static readonly string[] HtttTuPhatTrienHeaders =
    [
        "TT",
        "Tên phần mềm, CSDL",
        "Đơn vị nghiên cứu, phát triển",
        "Đơn vị quản lý, đơn vị sử dụng",
        "Năm triển khai",
        "Ứng dụng công nghệ mới, hiện đại",
        "Phạm vi, lĩnh vực nghiệp vụ khai thác sử dụng",
        "Khả năng tích hợp liên thông",
        "Đã được Hội đồng xét, công nhận sáng kiến, sáng tạo BCA",
        "Ghi chú",
    ];

    private static readonly string[] HtttTieuChuanHeaders =
    [
        "TT",
        "Tên hệ thống",
        "ĐVT",
        "H05",
        "Tỉnh (PV01)",
        "Xã",
        "Đơn vị trực thuộc Bộ",
        "Ghi chú",
    ];

    private static readonly Dictionary<string, ModuleGridDef> ModuleGridDefs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VAN_BAN_QPPL"] = new("CƠ CHẾ CHÍNH SÁCH, VBQPPL", new[]
        {
            "TT", "Số, ký hiệu văn bản", "Ngày, tháng, năm ban hành", "Trích yếu", "Tình trạng triển khai", "Ghi chú",
        }),
        ["DAO_TAO_BOI_DUONG"] = new("ĐÀO TẠO BỒI DƯỠNG KIẾN THỨC VỀ CNTT, CÔNG NGHỆ CAO", new[]
        {
            "TT", "Đơn vị chủ trì tổ chức", "Tên khóa học", "Số học viên tham gia", "Thời gian tổ chức", "Hình thức tổ chức", "Ghi chú",
        }),
        ["DAO_TAO_HOC_VIEN"] = new("ĐÀO TẠO HỌC VIỆN", new[]
        {
            "TT", "Nội dung đào tạo", "Tiến sĩ", "Thạc sĩ", "Đại học", "Cao đẳng", "Trung cấp", "Ghi chú",
        }),
        ["HA_TANG_MANG"] = new("HẠ TẦNG MẠNG", new[]
        {
            "TT", "Số đơn vị trực thuộc", "Đã kết nối BCANet", "Đường truyền VNPT", "Đường truyền khác",
            "Kết nối Internet", "Ghi chú",
        }),
        ["GIAM_SAT_NOC"] = new("GIÁM SÁT NOC", new[]
        {
            "TT", "Lớp giám sát", "Có NOC", "Thực trạng", "Tổng số đối tượng", "Số đã giám sát", "Ghi chú",
        }),
        ["ATTT_HTTT_DAU_TU"] = new("CÁC HTTT TRONG DỰ ÁN ĐANG ĐẦU TƯ, XÂY DỰNG", new[]
        {
            "TT", "Tên HTTT", "Chủ quản HTTT", "Đơn vị vận hành HTTT", "Cấp độ đề xuất", "DK thời điểm phê duyệt HSĐXCĐ",
            "Quyết định phê duyệt dự án", "Đã lồng ghép thuyết minh", "Ghi chú",
        }),
        ["ATTT_GIAI_PHAP"] = new("TRIỂN KHAI GIẢI PHÁP BẢO ĐẢM AN TOÀN THÔNG TIN", new[]
        {
            "TT", "Tên giải pháp",
            "Máy tính — Kết nối BCANet", "Máy tính — Kết nối Internet", "Máy tính — Kết nối local/độc lập",
            "Máy chủ — Kết nối BCANet", "Máy chủ — Kết nối Internet", "Máy chủ — Kết nối local",
            "Ghi chú",
        }),
        ["CAMERA_THUC_TRANG"] = new("CAMERA — THỰC TRẠNG", new[]
        {
            "TT", "Nhóm camera", "Tên hệ thống", "Cấu hình IP", "Cấu hình Analog", "Thực trạng IP",
            "Thực trạng Analog", "Chủ đầu tư", "Năm đầu tư", "Đường truyền", "Phần mềm", "Lưu trữ", "Ghi chú",
        }),
        ["DU_AN_CNTT"] = new("DỰ ÁN ĐẦU TƯ CNTT", new[]
        {
            "TT", "Tên dự án", "Đơn vị chủ trì", "Năm triển khai", "Năm đưa vào sử dụng",
            "Tổng kinh phí (tỷ VNĐ)", "Nguồn vốn", "Ghi chú",
        }),
    };

    /// <summary>Module hiển thị dạng bảng có nhóm băng (band) theo mẫu gốc — nhóm thay cho 1 cột định danh.</summary>
    private static readonly Dictionary<string, ModuleGridDef> GroupedModuleDefs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ATTT_HTTT_VAN_HANH"] = new("CÁC HTTT ĐANG VẬN HÀNH, KHAI THÁC, SỬ DỤNG", new[]
        {
            "TT", "Tên HTTT", "Chủ quản HTTT", "Đơn vị vận hành HTTT", "Cấp độ đề xuất", "Tình trạng phê duyệt cấp độ",
            "Quyết định phê duyệt cấp độ", "Quy chế bảo đảm ATTT cho hệ thống", "Dự kiến thời điểm phê duyệt hồ sơ đề xuất cấp độ",
            "Đã triển khai đầy đủ phương án bảo đảm ATTT", "Dự kiến thời điểm triển khai đầy đủ phương án bảo đảm ATTT",
            "Kiểm tra, đánh giá ATTT",
        }),
        ["ATTT_HTTT_DAU_TU"] = new("CÁC HTTT TRONG DỰ ÁN ĐANG ĐẦU TƯ, XÂY DỰNG", new[]
        {
            "TT", "Tên HTTT", "Chủ quản HTTT", "Đơn vị vận hành HTTT", "Cấp độ đề xuất", "DK thời điểm phê duyệt HSĐXCĐ",
            "Quyết định phê duyệt dự án", "Đã lồng ghép thuyết minh", "Ghi chú",
        }),
        ["THIET_BI_CNTT"] = new("TRANG THIẾT BỊ CNTT", new[]
        {
            "TT", "Tên thiết bị", "SL hiện dùng / Tổng", "Hãng sản xuất", "Đơn vị sử dụng",
            "Cấu hình", "Ứng dụng đang chạy", "Hệ điều hành", "Tình trạng sử dụng", "Ghi chú",
        }),
        ["GIAM_SAT_SOC"] = new("HỆ THỐNG GIÁM SÁT AN TOÀN THÔNG TIN MẠNG — SOC", new[]
        {
            "TT", "Lớp giám sát", "Có hệ thống giám sát", "Thực trạng giám sát", "Tổng số đối tượng",
            "GS một phần", "GS cơ bản", "GS đầy đủ", "Số sự cố", "Sự cố đã khắc phục", "Lực lượng ứng cứu", "Ghi chú",
        }),
        ["CAMERA_QUAN_LY"] = new("KIỂM SOÁT AN NINH — CÔNG TÁC QUẢN LÝ", new[]
        {
            "TT", "Tên đơn vị/địa chỉ", "Buồng giam trang bị (SL/TS)",
            "Nhu cầu đầu tư", "Bảo trì", "Sửa chữa", "Số lần vi phạm", "Kết nối chia sẻ",
            "Hồ sơ cấp độ ATTT", "CB chuyên trách", "CB kiêm nhiệm", "CB địa phương",
            "Đào tạo (Bộ)", "Đào tạo (nhu cầu)", "Ghi chú",
        }),
    };

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
        var start = row;
        var spans = BuildBalancedSpans(totalCols, [24, 24, 16, 16, 13, 17, 17]);

        ws.Cell(row, 1).Value = "1. THÔNG TIN ĐƠN VỊ";
        ws.Range(row, 1, row, totalCols).Merge().Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderFill));
        row++;

        var headers = new[]
        {
            "Tên đơn vị",
            "Địa chỉ Đơn vị",
            "Website nội bộ",
            "Website Internet",
            "Tổng biên chế",
            "Số lượng đơn vị cấp Phòng",
            "Số lượng đơn vị cấp xã",
        };

        for (var c = 0; c < headers.Length; c++)
        {
            PutSpannedCell(ws, row, spans[c], headers[c]);
        }

        ws.Range(row, 1, row, totalCols).Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderFill))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Alignment.SetWrapText(true);
        row++;

        var values = new object?[]
        {
            donVi?.TenDonVi ?? string.Empty,
            donVi?.DiaChi ?? string.Empty,
            donVi?.WebsiteNoiBo ?? string.Empty,
            donVi?.WebsiteInternet ?? string.Empty,
            donVi?.TongBienChe?.ToString() ?? string.Empty,
            data.SoDonViCapPhong,
            data.SoDonViCapXa,
        };
        for (var c = 0; c < values.Length; c++)
        {
            PutSpannedCell(ws, row, spans[c], values[c]);
        }

        ws.Range(row, 1, row, totalCols).Style
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Alignment.SetWrapText(true);
        for (var c = 0; c < 4; c++)
        {
            ws.Cell(row, spans[c].Start).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
        }
        row++;

        ApplyTableBorders(ws, start, 1, row - 1, totalCols);
        ApplyBodyPrintLayout(ws, start + 1, row - 1, 1, totalCols);
        return row;
    }

    private async Task<int> CountDirectChildrenAsync(long parentId, bool isCapPhong, CancellationToken ct)
    {
        // Oracle NUMBER(1) is mapped through bool -> short. Keep the bool as a
        // parameter instead of a SQL literal to avoid OracleBoolTypeMapping
        // trying to cast the converted Int16 value back to Boolean.
        var activeFlag = true;
        var children = await _db.DonVis
            .AsNoTracking()
            .Where(x => x.ParentId == parentId && x.IsActive == activeFlag)
            .Select(x => x.TenDonVi)
            .ToListAsync(ct);

        return children.Count(name => isCapPhong ? IsCapPhongByTen(name) : IsCapXaByTen(name));
    }

    private static bool IsCapPhongByTen(string tenDonVi)
    {
        return StartsWithAny(tenDonVi, ["Phòng", "Phong"]);
    }

    private static bool IsCapXaByTen(string tenDonVi)
    {
        return StartsWithAny(tenDonVi, ["Công an xã", "Công an phường", "Công an thị trấn", "Cong an xa", "Cong an phuong", "Cong an thi tran"]);
    }

    private static bool StartsWithAny(string value, IReadOnlyCollection<string> prefixes)
    {
        var normalized = RemoveDiacritics(value).Trim();
        foreach (var prefix in prefixes)
        {
            if (normalized.StartsWith(RemoveDiacritics(prefix), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private int WriteNhanLucSection(
        IXLWorksheet ws,
        int row,
        ExportData data,
        int totalCols,
        int? sectionNumber = null)
    {
        var spans = BuildBalancedSpans(totalCols, [5, 24, 14, 14, 20, 22, 12, 24, 24]);
        var start = row;

        ws.Cell(row, 1).Value = FormatSectionTitle("NHÂN LỰC CÔNG NGHỆ THÔNG TIN", sectionNumber);
        ws.Range(row, 1, row, totalCols).Merge().Style
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
            PutSpannedCell(ws, row, spans[c], headers[c]);
        }

        ws.Range(row, 1, row, totalCols).Style
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
            ws.Range(row, 1, row, totalCols).Merge().Style
                .Font.SetBold().Font.SetItalic()
                .Fill.SetBackgroundColor(XLColor.FromHtml(BandFill));
            row++;

            var stt = 1;
            foreach (var item in group)
            {
                var values = new object?[]
                {
                    stt++,
                    item.HoTen,
                    item.NgaySinh?.ToString("dd/MM/yyyy") ?? string.Empty,
                    Translate(data.CodeLabels, "CAP_BAC_CONG_AN", item.CapBac),
                    item.ChucVu ?? string.Empty,
                    data.DonViNames.GetValueOrDefault(item.DonViId, string.Empty),
                    item.DienThoai ?? string.Empty,
                    Translate(data.CodeLabels, "TRINH_DO_CNTT", item.TrinhDoCntt),
                    Translate(data.CodeLabels, "TRINH_DO_LLCT", item.TrinhDoLlct),
                };
                for (var c = 0; c < values.Length; c++)
                {
                    PutSpannedCell(ws, row, spans[c], values[c]);
                }
                ws.Cell(row, spans[0].Start).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                row++;
            }
        }

        if (data.NhanLuc.Count == 0)
        {
            ws.Cell(row, 1).Value = "Không có dữ liệu";
            ws.Range(row, 1, row, totalCols).Merge().Style
                .Font.SetItalic()
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row++;
        }

        ApplyTableBorders(ws, start, 1, row - 1, totalCols);
        ApplyBodyPrintLayout(ws, start + 1, row - 1, 1, totalCols);
        return row;
    }

    private int WriteNangLucSoSection(
        IXLWorksheet ws,
        int row,
        ExportData data,
        int totalCols,
        int? sectionNumber = null)
    {
        var spans = BuildBalancedSpans(totalCols, [50, 16, 16, 16, 28]);
        var start = row;

        IXLCell Put(int r, int index, object? value)
        {
            return PutSpannedCell(ws, r, spans[index], value);
        }

        ws.Cell(row, 1).Value = FormatSectionTitle(
            "PHÁT TRIỂN NGUỒN NHÂN LỰC — NĂNG LỰC SỐ THEO NHÓM VỊ TRÍ",
            sectionNumber);
        ws.Range(row, 1, row, totalCols).Merge().Style
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

        ws.Range(row, 1, row, totalCols).Style
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
            ws.Range(row, 1, row, totalCols).Merge().Style
                .Font.SetItalic()
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row++;
        }

        ApplyTableBorders(ws, start, 1, row - 1, totalCols);
        ApplyBodyPrintLayout(ws, start + 1, row - 1, 1, totalCols);
        return row;
    }

    /// <summary>
    /// Phần HTTT trong biểu mẫu gồm hai nhóm PM/CSDL có bộ cột khác nhau và
    /// một bảng HTTT tiêu chuẩn. Ghi chung dưới một số thứ tự phần để bám sát mẫu gốc.
    /// </summary>
    private static int WriteHeThongThongTinSection(
        IXLWorksheet ws,
        int row,
        ExportData data,
        int totalCols,
        int sectionNumber)
    {
        ws.Cell(row, 1).Value = $"{sectionNumber}. HỆ THỐNG THÔNG TIN, PHẦN MỀM, CSDL NGHIỆP VỤ";
        ws.Range(row, 1, row, totalCols).Merge().Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderFill));
        row++;

        if (data.HasModule("HE_THONG_THONG_TIN"))
        {
            data.ModuleRows.TryGetValue("HE_THONG_THONG_TIN_DUNG_CHUNG", out var dungChungRows);
            row = WriteGridSection(
                ws,
                row,
                "(1) Phần mềm, CSDL nghiệp vụ do các Cục nghiệp vụ triển khai dùng chung",
                HtttDungChungHeaders,
                dungChungRows ?? [],
                totalCols);
            row++;

            data.ModuleRows.TryGetValue("HE_THONG_THONG_TIN_TU_PHAT_TRIEN", out var tuPhatTrienRows);
            row = WriteGridSection(
                ws,
                row,
                "(2) Do đơn vị, địa phương tự nghiên cứu, phát triển",
                HtttTuPhatTrienHeaders,
                tuPhatTrienRows ?? [],
                totalCols);

            data.ModuleRows.TryGetValue("HE_THONG_THONG_TIN_CHUA_PHAN_LOAI", out var unclassifiedRows);
            if (unclassifiedRows is { Count: > 0 })
            {
                row++;
                row = WriteGridSection(
                    ws,
                    row,
                    "(*) Dữ liệu cũ chưa phân loại — cần rà soát trước khi nộp báo cáo",
                    HtttTuPhatTrienHeaders,
                    unclassifiedRows,
                    totalCols);
            }

            row++;
        }

        if (data.HasModule("HTTT_TIEU_CHUAN"))
        {
            data.ModuleRows.TryGetValue("HTTT_TIEU_CHUAN", out var standardRows);
            row = WriteGridSection(
                ws,
                row,
                "(3) Danh mục Hệ thống thông tin và tiêu chuẩn, định mức trang thiết bị CNTT chuyên dùng trong CAND",
                HtttTieuChuanHeaders,
                standardRows ?? [],
                totalCols);
        }

        return row;
    }

    /// <summary>Bảng phẳng dùng chung cho các module còn lại: cột TT tự sinh + các cột theo row đã format sẵn.</summary>
    private static int WriteGridSection(
        IXLWorksheet ws,
        int row,
        string title,
        string[] headers,
        IReadOnlyList<object?[]> rows,
        int totalCols)
    {
        var spans = BuildBalancedSpans(totalCols, GetGridColumnWeights(title, headers));
        var start = row;

        ws.Cell(row, 1).Value = title;
        ws.Range(row, 1, row, totalCols).Merge().Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderFill));
        row++;

        for (var c = 0; c < headers.Length; c++)
        {
            PutSpannedCell(ws, row, spans[c], headers[c]);
        }

        ws.Range(row, 1, row, totalCols).Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderFill))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Alignment.SetWrapText(true);
        row++;

        if (rows.Count == 0)
        {
            ws.Cell(row, 1).Value = "Không có dữ liệu";
            ws.Range(row, 1, row, totalCols).Merge().Style
                .Font.SetItalic()
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row++;
        }
        else
        {
            var stt = 1;
            foreach (var dataRow in rows)
            {
                PutSpannedCell(ws, row, spans[0], stt++);
                ws.Cell(row, spans[0].Start).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                for (var c = 0; c < dataRow.Length && c + 1 < spans.Length; c++)
                {
                    var cell = PutSpannedCell(ws, row, spans[c + 1], null);
                    SetGridCell(cell, dataRow[c]);
                }
                row++;
            }
        }

        ApplyTableBorders(ws, start, 1, row - 1, totalCols);
        ApplyBodyPrintLayout(ws, start + 1, row - 1, 1, totalCols);
        return row;
    }

    /// <summary>Bảng phẳng có nhóm băng (band row) theo nhãn — vd nhóm theo Loại mạng, Loại thiết bị, Nhóm camera.</summary>
    private static int WriteGridSectionGrouped(
        IXLWorksheet ws,
        int row,
        string title,
        string[] headers,
        IReadOnlyList<(string Label, List<object?[]> Rows)> groups,
        int totalCols)
    {
        var spans = BuildBalancedSpans(totalCols, GetGridColumnWeights(title, headers));
        var start = row;

        ws.Cell(row, 1).Value = title;
        ws.Range(row, 1, row, totalCols).Merge().Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderFill));
        row++;

        for (var c = 0; c < headers.Length; c++)
        {
            PutSpannedCell(ws, row, spans[c], headers[c]);
        }

        ws.Range(row, 1, row, totalCols).Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderFill))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Alignment.SetWrapText(true);
        row++;

        var hasAnyRow = false;
        var preserveEmptyBands = title.Contains("HTTT ĐANG VẬN HÀNH", StringComparison.OrdinalIgnoreCase);
        foreach (var (label, groupRows) in groups)
        {
            if (groupRows.Count == 0 && !preserveEmptyBands)
            {
                continue;
            }

            hasAnyRow = true;
            ws.Cell(row, 1).Value = label;
            ws.Range(row, 1, row, totalCols).Merge().Style
                .Font.SetBold().Font.SetItalic()
                .Fill.SetBackgroundColor(XLColor.FromHtml(BandFill));
            row++;

            if (groupRows.Count == 0)
            {
                ws.Cell(row, 1).Value = "Không có dữ liệu";
                ws.Range(row, 1, row, totalCols).Merge().Style
                    .Font.SetItalic()
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                row++;
                continue;
            }

            var stt = 1;
            foreach (var dataRow in groupRows)
            {
                PutSpannedCell(ws, row, spans[0], stt++);
                ws.Cell(row, spans[0].Start).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                for (var c = 0; c < dataRow.Length && c + 1 < spans.Length; c++)
                {
                    var cell = PutSpannedCell(ws, row, spans[c + 1], null);
                    SetGridCell(cell, dataRow[c]);
                }
                row++;
            }
        }

        if (!hasAnyRow)
        {
            ws.Cell(row, 1).Value = "Không có dữ liệu";
            ws.Range(row, 1, row, totalCols).Merge().Style
                .Font.SetItalic()
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row++;
        }

        ApplyTableBorders(ws, start, 1, row - 1, totalCols);
        ApplyBodyPrintLayout(ws, start + 1, row - 1, 1, totalCols);
        return row;
    }

    private static void SetGridCell(IXLCell cell, object? value)
    {
        cell.Style.Alignment.SetWrapText(true);
        cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        cell.Style.Alignment.SetShrinkToFit(false);

        switch (value)
        {
            case null:
                cell.Value = string.Empty;
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
                break;
            case bool b:
                cell.Value = b ? "Có" : "Không";
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                break;
            case int i:
                cell.Value = i;
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                break;
            case long l:
                cell.Value = l;
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                break;
            case decimal dec:
                cell.Value = dec;
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                break;
            default:
                // Giữ nguyên nội dung; WrapText + độ rộng span sẽ để Excel tự ngắt
                // tại vị trí tự nhiên. Chèn newline cứng theo ký tự '_'/'-' làm dữ liệu
                // bị thò thụt và khiến chiều cao hàng không ổn định khi in.
                cell.Value = value.ToString() ?? string.Empty;
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
                break;
        }
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

    private async Task<Dictionary<string, List<object?[]>>> LoadModuleRowsAsync(
        long[] targetDonViIds, string kyCode, DateTime asOf,
        IReadOnlyDictionary<string, Dictionary<string, string>> codeLabels, CancellationToken ct)
    {
        var result = new Dictionary<string, List<object?[]>>(StringComparer.OrdinalIgnoreCase);

        // ---- Nhóm batch (theo KyBaoCaoCode, chốt qua CopyLiveToHisAsync) ----
        result["VAN_BAN_QPPL"] = (await _db.VanBanQpplHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .OrderBy(x => x.NgayBanHanh).ToListAsync(ct))
            .Select(x => new object?[]
            {
                x.SoHieu, FmtDate(x.NgayBanHanh), x.TrichYeu, x.TinhTrangTrienKhai, x.GhiChu,
            }).ToList<object?[]>();

        result["DAO_TAO_BOI_DUONG"] = (await _db.DaoTaoBoiDuongHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .OrderBy(x => x.TenKhoaHoc).ToListAsync(ct))
            .Select(x => new object?[]
            {
                x.DonViToChuc, x.TenKhoaHoc, x.SoLuongHv, FmtDateRange(x.ThoiGianTu, x.ThoiGianDen), x.HinhThuc, x.GhiChu,
            }).ToList<object?[]>();

        result["DAO_TAO_HOC_VIEN"] = (await _db.DaoTaoHocVienHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .OrderBy(x => x.NoiDungDaoTao).ToListAsync(ct))
            .Select(x => new object?[]
            {
                x.NoiDungDaoTao, x.SoTienSi, x.SoThacSi, x.SoDaiHoc, x.SoCaoDang, x.SoTrungCap, x.GhiChu,
            }).ToList<object?[]>();

        result["HA_TANG_MANG"] = (await _db.HaTangMangHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .ToListAsync(ct))
            .Select(x => new object?[]
            {
                x.SoDonViTrucThuoc, x.SoDaKetNoiBcanet, x.SoDuongTruyenVnpt, x.SoDuongTruyenKhac, x.SoKetNoiInternet, x.GhiChu,
            }).ToList<object?[]>();

        result["GIAM_SAT_NOC"] = (await _db.GiamSatNocHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .OrderBy(x => x.LopGiamSat).ToListAsync(ct))
            .Select(x => new object?[]
            {
                x.LopGiamSat, x.CoNoc, x.ThucTrang, x.TongSoDoiTuong, x.SoDaGiamSat, x.GhiChu,
            }).ToList<object?[]>();

        var htttNames = await _db.HeThongThongTins.AsNoTracking()
            .Select(x => new { x.Id, x.TenPhanMem }).ToDictionaryAsync(x => x.Id, x => x.TenPhanMem, ct);

        result["ATTT_HTTT_DAU_TU"] = (await _db.AtttHtttDauTuHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .OrderBy(x => x.HtttId).ToListAsync(ct))
            .Select(x => new object?[]
            {
                htttNames.GetValueOrDefault(x.HtttId, $"#{x.HtttId}"), x.ChuQuan, x.DonViVanHanh, x.CapDoDeXuat, FmtDate(x.NgayPheDuyetHsdxcd), x.QuyetDinhPheDuyet, x.DaLongGhepThuyetMinh, x.GhiChu,
            }).ToList<object?[]>();

        result["ATTT_GIAI_PHAP"] = (await _db.GiaiPhapAtttHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .OrderBy(x => x.TenGiaiPhap).ToListAsync(ct))
            .Select(x => new object?[]
            {
                x.TenGiaiPhap,
                FmtRatio(x.MayTinhBcanetSl, x.MayTinhBcanetTs), FmtRatio(x.MayTinhInternetSl, x.MayTinhInternetTs), FmtRatio(x.MayTinhLocalSl, x.MayTinhLocalTs),
                FmtRatio(x.MayChuBcanetSl, x.MayChuBcanetTs), FmtRatio(x.MayChuInternetSl, x.MayChuInternetTs), FmtRatio(x.MayChuLocalSl, x.MayChuLocalTs),
                x.GhiChu,
            }).ToList<object?[]>();

        result["CAMERA_THUC_TRANG"] = (await _db.CameraThucTrangHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .OrderBy(x => x.TenHeThong).ToListAsync(ct))
            .Select(x => new object?[]
            {
                Translate(codeLabels, "NHOM_CAMERA", x.NhomCamera), x.TenHeThong, x.CauHinhIp, x.CauHinhAnalog, x.ThucTrangIp, x.ThucTrangAnalog,
                x.ChuDauTu, x.NamDauTu, x.DuongTruyen, x.PhanMem, x.LuuTru, x.GhiChu,
            }).ToList<object?[]>();

        result["DU_AN_CNTT"] = (await _db.DuAnCnttHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .OrderBy(x => x.TenDuAn).ToListAsync(ct))
            .Select(x => new object?[]
            {
                x.TenDuAn, x.DonViChuTri, x.NamTrienKhai, x.NamDuaVaoSuDung, x.TongKinhPhi,
                Translate(codeLabels, "NGUON_VON_DU_AN", x.NguonVon), x.GhiChu,
            }).ToList<object?[]>();

        // ---- Nhóm versioned (ValidFrom/ValidTo, không qua CopyLiveToHisAsync) ----
        var htttLive = await _db.HeThongThongTins.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.ValidFrom <= asOf).ToListAsync(ct);
        var htttHis = await _db.HeThongThongTinHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.ValidFrom <= asOf && x.ValidTo > asOf).ToListAsync(ct);
        result["HE_THONG_THONG_TIN_DUNG_CHUNG"] = htttLive
            .Where(x => x.LoaiPhanMem == LoaiPhanMemCodes.DungChung)
            .Select(x => new object?[]
            {
                x.TenPhanMem, x.DonViQuanLy, x.NamTrienKhai, x.UngDungCnMoi,
                x.PhamViHoatDongKyThuat, x.KhaNangTichHop, x.GhiChu,
            })
            .Concat(htttHis
            .Where(x => x.LoaiPhanMem == LoaiPhanMemCodes.DungChung)
            .Select(x => new object?[]
            {
                x.TenPhanMem, x.DonViQuanLy, x.NamTrienKhai, x.UngDungCnMoi,
                x.PhamViHoatDongKyThuat, x.KhaNangTichHop, x.GhiChu,
            }))
            .ToList();

        result["HE_THONG_THONG_TIN_TU_PHAT_TRIEN"] = htttLive
            .Where(x => x.LoaiPhanMem == LoaiPhanMemCodes.TuPhatTrien)
            .Select(x => new object?[]
            {
                x.TenPhanMem, x.DonViPhatTrien, x.DonViQuanLy, x.NamTrienKhai,
                x.UngDungCnMoi, x.PhamViHoatDong, x.KhaNangTichHop,
                x.DaCongNhanSangKien, x.GhiChu,
            })
            .Concat(htttHis
            .Where(x => x.LoaiPhanMem == LoaiPhanMemCodes.TuPhatTrien)
            .Select(x => new object?[]
            {
                x.TenPhanMem, x.DonViPhatTrien, x.DonViQuanLy, x.NamTrienKhai,
                x.UngDungCnMoi, x.PhamViHoatDong, x.KhaNangTichHop,
                x.DaCongNhanSangKien, x.GhiChu,
            }))
            .ToList();

        result["HE_THONG_THONG_TIN_CHUA_PHAN_LOAI"] = htttLive
            .Where(x => x.LoaiPhanMem == LoaiPhanMemCodes.ChuaPhanLoai)
            .Select(x => new object?[]
            {
                x.TenPhanMem, x.DonViPhatTrien, x.DonViQuanLy, x.NamTrienKhai,
                x.UngDungCnMoi, x.PhamViHoatDong ?? x.PhamViHoatDongKyThuat,
                x.KhaNangTichHop, x.DaCongNhanSangKien, x.GhiChu,
            })
            .Concat(htttHis
            .Where(x => x.LoaiPhanMem == LoaiPhanMemCodes.ChuaPhanLoai)
            .Select(x => new object?[]
            {
                x.TenPhanMem, x.DonViPhatTrien, x.DonViQuanLy, x.NamTrienKhai,
                x.UngDungCnMoi, x.PhamViHoatDong ?? x.PhamViHoatDongKyThuat,
                x.KhaNangTichHop, x.DaCongNhanSangKien, x.GhiChu,
            }))
            .ToList();

        var tieuChuanLive = await _db.HtttTieuChuans.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.ValidFrom <= asOf).ToListAsync(ct);
        var tieuChuanHis = await _db.HtttTieuChuanHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.ValidFrom <= asOf && x.ValidTo > asOf).ToListAsync(ct);
        result["HTTT_TIEU_CHUAN"] = tieuChuanLive
            .Select(x => new object?[] { x.TenHeThong, x.Dvt, x.SoH05, x.SoTinh, x.SoXa, x.SoDvTrucThuocBo, x.GhiChu })
            .Concat(tieuChuanHis.Select(x => new object?[] { x.TenHeThong, x.Dvt, x.SoH05, x.SoTinh, x.SoXa, x.SoDvTrucThuocBo, x.GhiChu }))
            .ToList();

        return result;
    }

    private static string FmtDate(DateOnly? d) => d?.ToString("dd/MM/yyyy") ?? string.Empty;

    private static string FmtAtttCapDo(string? value) => value?.ToUpperInvariant() switch
    {
        "CAP_1" => "1",
        "CAP_2" => "2",
        "CAP_3" => "3",
        "CAP_4" => "4",
        "CAP_5" => "5",
        _ => value ?? string.Empty,
    };

    private static string FmtAtttPheDuyet(string? value) => value?.ToUpperInvariant() switch
    {
        "CHUA_PHE_DUYET" => "Chưa phê duyệt",
        "DANG_XU_LY" => "Đang xử lý",
        "DA_PHE_DUYET" => "Đã được phê duyệt",
        _ => value ?? string.Empty,
    };

    private static string FmtAtttTrienKhai(string? status, bool legacyCompleted, string? details)
    {
        var effectiveStatus = string.IsNullOrWhiteSpace(status)
            ? (legacyCompleted ? "DA_TRIEN_KHAI_DAY_DU" : "CHUA_TRIEN_KHAI")
            : status.ToUpperInvariant();
        var label = effectiveStatus switch
        {
            "DA_TRIEN_KHAI_DAY_DU" => "Đã triển khai đầy đủ",
            "DANG_TRIEN_KHAI" => "Đang triển khai",
            _ => "Chưa triển khai",
        };

        return string.IsNullOrWhiteSpace(details) ? label : $"{label}: {details.Trim()}";
    }

    private static string FmtDateRange(DateOnly? from, DateOnly? to)
    {
        if (from is null && to is null)
        {
            return string.Empty;
        }

        return $"{FmtDate(from)} - {FmtDate(to)}".Trim(' ', '-');
    }

    private static string FmtRatio(int used, int total)
    {
        if (total <= 0)
        {
            return used > 0 ? used.ToString() : string.Empty;
        }

        var pct = Math.Round(used * 100.0 / total);
        return $"{used}/{total} ({pct:0}%)";
    }

    /// <summary>3 module trình bày dạng bảng có nhóm băng theo mẫu gốc: SOC (theo Loại mạng),
    /// Camera quản lý (theo Nhóm camera), Thiết bị CNTT (theo tổ tiên cấp cao nhất trong cây Loại thiết bị).</summary>
    private async Task<Dictionary<string, List<(string Label, List<object?[]> Rows)>>> LoadGroupedModuleRowsAsync(
        long[] targetDonViIds, string kyCode, DateTime asOf,
        IReadOnlyDictionary<string, Dictionary<string, string>> codeLabels, CancellationToken ct)
    {
        var result = new Dictionary<string, List<(string Label, List<object?[]> Rows)>>(StringComparer.OrdinalIgnoreCase);

        // ---- ATTT HTTT vận hành: chia đúng ba nhóm I/II/III của biểu mẫu gốc ----
        var atttHtttNames = await _db.HeThongThongTins.AsNoTracking()
            .Select(x => new { x.Id, x.TenPhanMem })
            .ToDictionaryAsync(x => x.Id, x => x.TenPhanMem, ct);
        var atttVanHanhRows = await _db.AtttHtttVanHanhHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .OrderBy(x => x.HtttId)
            .ToListAsync(ct);
        var atttGroups = new[]
        {
            (Code: "BCANET", Prefix: "I", Label: "HTTT triển khai trên hạ tầng mạng máy tính nội bộ có kết nối BCANET"),
            (Code: "INTERNET", Prefix: "II", Label: "HTTT triển khai trên hạ tầng mạng Internet"),
            (Code: "KHAC", Prefix: "III", Label: "HTTT triển khai trên hạ tầng khác"),
        };
        result["ATTT_HTTT_VAN_HANH"] = atttGroups
            .Select(group =>
            {
                var rows = atttVanHanhRows
                    .Where(x => string.Equals(x.LoaiHaTang ?? "KHAC", group.Code, StringComparison.OrdinalIgnoreCase))
                    .Select(x => new object?[]
                    {
                        atttHtttNames.GetValueOrDefault(x.HtttId, $"#{x.HtttId}"),
                        x.ChuQuan,
                        x.DonViVanHanh,
                        FmtAtttCapDo(x.CapDoDeXuat),
                        FmtAtttPheDuyet(x.TinhTrangPheDuyet),
                        x.QuyetDinhPheDuyet,
                        x.QuyCheAttt,
                        FmtDate(x.DuKienNgayPheDuyet),
                        FmtAtttTrienKhai(x.TrangThaiTrienKhaiPhuongAn, x.DaTrienKhaiPhuongAn, x.NoiDungPhuongAnDaTrienKhai),
                        FmtDate(x.DuKienNgayTrienKhai),
                        x.KiemTraDanhGia,
                    })
                    .ToList<object?[]>();
                return (Label: $"{group.Prefix}. {group.Label} (Tổng {rows.Count} hệ thống)", Rows: rows);
            })
            .ToList();

        // ---- ATTT HTTT đầu tư: nhóm hạ tầng là thuộc tính của bản khai ATTT,
        // không suy diễn từ mô tả tự do trong danh mục Hệ thống thông tin. ----
        var atttDauTuRows = await _db.AtttHtttDauTuHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .OrderBy(x => x.HtttId)
            .ToListAsync(ct);
        result["ATTT_HTTT_DAU_TU"] = atttGroups
            .Select(group =>
            {
                var rows = atttDauTuRows
                    .Where(x => string.Equals(x.LoaiHaTang, group.Code, StringComparison.OrdinalIgnoreCase))
                    .Select(x => new object?[]
                    {
                        atttHtttNames.GetValueOrDefault(x.HtttId, $"#{x.HtttId}"),
                        x.ChuQuan,
                        x.DonViVanHanh,
                        FmtAtttCapDo(x.CapDoDeXuat),
                        FmtDate(x.NgayPheDuyetHsdxcd),
                        x.QuyetDinhPheDuyet,
                        x.DaLongGhepThuyetMinh ? "Đã lồng ghép" : "Chưa lồng ghép",
                        x.GhiChu,
                    })
                    .ToList<object?[]>();
                return (Label: $"{group.Prefix}. {group.Label} (Tổng {rows.Count} hệ thống)", Rows: rows);
            })
            .ToList();

        // ---- GIAM_SAT_SOC: nhóm theo Loại mạng (BCANet/Mật/Internet/Độc lập) ----
        var socRows = await _db.GiamSatSocHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .OrderBy(x => x.LopGiamSat).ToListAsync(ct);
        result["GIAM_SAT_SOC"] = socRows
            .GroupBy(x => x.LoaiMang, StringComparer.OrdinalIgnoreCase)
            .Select(g => (
                Label: string.IsNullOrWhiteSpace(g.Key) ? "Chưa xác định" : g.Key,
                Rows: g.Select(x => new object?[]
                {
                    x.LopGiamSat, x.CoHeThong, x.ThucTrang, x.TongSoDoiTuong,
                    x.SoGiamSatMotPhan, x.SoGiamSatCoBan, x.SoGiamSatDayDu, x.SoSuCo, x.SoSuCoDaKhacPhuc, x.LucLuongUngCuu, x.GhiChu,
                }).ToList<object?[]>()))
            .ToList();

        // ---- CAMERA_QUAN_LY: nhóm theo Nhóm camera ----
        var cameraRows = await _db.CameraQuanLyHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.KyBaoCaoCode == kyCode)
            .OrderBy(x => x.TenDonViDiaChi).ToListAsync(ct);
        result["CAMERA_QUAN_LY"] = cameraRows
            .GroupBy(x => x.NhomCamera ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(g => (
                Label: string.IsNullOrWhiteSpace(Translate(codeLabels, "NHOM_CAMERA", g.Key)) ? "Chưa phân loại" : Translate(codeLabels, "NHOM_CAMERA", g.Key),
                Rows: g.Select(x => new object?[]
                {
                    x.TenDonViDiaChi, FmtRatio(x.BuongGiamTrangBiSl, x.BuongGiamTrangBiTs),
                    x.NhuCauDauTu, x.BaoTri, x.SuaChua, x.SoLanViPham, x.KetNoiChiaSe, x.HoSoCapDoAttt,
                    x.CbChuyenTrach, x.CbKiemNhiem, x.CbDiaPhuong, x.DaoTaoBo, x.DaoTaoNhuCau, x.GhiChu,
                }).ToList<object?[]>()))
            .ToList();

        // ---- THIET_BI_CNTT: nhóm theo tổ tiên cấp cao nhất trong cây Loại thiết bị ----
        var loaiThietBiAll = await _db.RefLoaiThietBis.AsNoTracking()
            .Select(x => new { x.Id, x.ParentId, x.TenLoai }).ToListAsync(ct);
        var loaiById = loaiThietBiAll.ToDictionary(x => x.Id);

        string RootLabel(long loaiId)
        {
            if (!loaiById.TryGetValue(loaiId, out var node))
            {
                return "Khác";
            }

            var current = node;
            var guard = 0;
            while (current.ParentId.HasValue && loaiById.TryGetValue(current.ParentId.Value, out var parent) && guard++ < 20)
            {
                current = parent;
            }

            return current.TenLoai;
        }

        var appNamesByThietBi = (await _db.ThietBiUngDungs.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.ThietBi!.DonViId))
            .Select(x => new { x.ThietBiId, TenPhanMem = x.HeThong!.TenPhanMem })
            .ToListAsync(ct))
            .GroupBy(x => x.ThietBiId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.TenPhanMem).Where(n => !string.IsNullOrWhiteSpace(n))));

        var thietBiLive = await _db.ThietBiCntts.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.ValidFrom <= asOf).ToListAsync(ct);
        var thietBiHis = await _db.ThietBiCnttHis.AsNoTracking()
            .Where(x => targetDonViIds.Contains(x.DonViId) && x.ValidFrom <= asOf && x.ValidTo > asOf).ToListAsync(ct);

        object?[] ThietBiRow(long id, long loaiThietBiId, string? tenThietBi, string? hangSanXuat, string? donViSuDung,
            string? cauHinh, string? heDieuHanh, int soLuongHienDung, int soLuongTong, string? tinhTrang, string? ghiChu)
            => new object?[]
            {
                tenThietBi, $"{soLuongHienDung}/{soLuongTong}", hangSanXuat, donViSuDung,
                cauHinh, appNamesByThietBi.GetValueOrDefault(id, string.Empty), heDieuHanh, tinhTrang, ghiChu,
            };

        var thietBiFlat = thietBiLive
            .Select(x => (Loai: RootLabel(x.LoaiThietBiId), Row: ThietBiRow(x.Id, x.LoaiThietBiId, x.TenThietBi, x.HangSanXuat, x.DonViSuDung, x.CauHinh, x.HeDieuHanh, x.SoLuongHienDung, x.SoLuongTong, x.TinhTrang, x.GhiChu)))
            .Concat(thietBiHis.Select(x => (Loai: RootLabel(x.LoaiThietBiId), Row: ThietBiRow(x.SourceId, x.LoaiThietBiId, x.TenThietBi, x.HangSanXuat, x.DonViSuDung, x.CauHinh, x.HeDieuHanh, x.SoLuongHienDung, x.SoLuongTong, x.TinhTrang, x.GhiChu))))
            .ToList();

        result["THIET_BI_CNTT"] = thietBiFlat
            .GroupBy(x => x.Loai)
            .Select(g => (Label: g.Key, Rows: g.Select(x => x.Row).ToList()))
            .ToList();

        return result;
    }

    // ==================================================================
    // Helpers
    // ==================================================================
    private static string FormatSectionTitle(string title, int? sectionNumber)
        => sectionNumber.HasValue ? $"{sectionNumber.Value}. {title}" : title;

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
        ws.Column(6).Width = 22;
        ws.Column(7).Width = 12;
        ws.Column(8).Width = 24;
        ws.Column(9).Width = 24;
    }

    private static void SetNangLucSoColumnWidths(IXLWorksheet ws)
    {
        ws.Column(1).Width = 50;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 16;
        ws.Column(4).Width = 16;
        ws.Column(5).Width = 28;
    }

    private static void SetSummaryGridColumnWidths(IXLWorksheet ws, int totalCols)
    {
        for (var column = 1; column <= totalCols; column++)
        {
            ws.Column(column).Width = SummaryGridColumnWidth;
        }
    }

    private static (int Start, int End)[] BuildBalancedSpans(int totalCols, IReadOnlyList<double> weights)
    {
        if (weights.Count == 0)
        {
            return [];
        }

        if (totalCols < weights.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCols), "Số cột vật lý phải lớn hơn hoặc bằng số cột nghiệp vụ.");
        }

        var allocations = Enumerable.Repeat(1, weights.Count).ToArray();
        var remaining = totalCols - weights.Count;
        var safeWeights = weights.Select(weight => Math.Max(1d, weight)).ToArray();
        var totalWeight = safeWeights.Sum();
        var exactExtras = safeWeights.Select(weight => remaining * weight / totalWeight).ToArray();

        for (var index = 0; index < allocations.Length; index++)
        {
            allocations[index] += (int)Math.Floor(exactExtras[index]);
        }

        var unassigned = totalCols - allocations.Sum();
        foreach (var index in exactExtras
                     .Select((value, index) => new { index, fraction = value - Math.Floor(value) })
                     .OrderByDescending(item => item.fraction)
                     .ThenBy(item => item.index)
                     .Take(unassigned)
                     .Select(item => item.index))
        {
            allocations[index]++;
        }

        var spans = new (int Start, int End)[allocations.Length];
        var start = 1;
        for (var index = 0; index < allocations.Length; index++)
        {
            var end = start + allocations[index] - 1;
            spans[index] = (start, end);
            start = end + 1;
        }

        return spans;
    }

    private static IXLCell PutSpannedCell(
        IXLWorksheet ws,
        int row,
        (int Start, int End) span,
        object? value)
    {
        if (span.Start != span.End)
        {
            ws.Range(row, span.Start, row, span.End).Merge();
        }

        var cell = ws.Cell(row, span.Start);
        SetGridCell(cell, value);
        return cell;
    }

    private static void ApplyTableBorders(IXLWorksheet ws, int r1, int c1, int r2, int c2)
    {
        var range = ws.Range(r1, c1, r2, c2);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Alignment.WrapText = true;
    }

    private static double[] GetGridColumnWeights(string title, string[] headers)
    {
        var normalizedTitle = RemoveDiacritics(title).Trim().ToUpperInvariant();
        if (normalizedTitle.Contains("DU AN DAU TU CNTT"))
        {
            return [5, 17, 16, 10, 10, 13, 16, 42];
        }

        var weights = new double[headers.Length];
        for (var c = 0; c < headers.Length; c++)
        {
            var header = headers[c];
            var normalized = RemoveDiacritics(header).Trim().ToUpperInvariant();

            if (c == 0)
            {
                weights[c] = 5;
                continue;
            }

            if (normalized.Contains("GHI CHU"))
            {
                weights[c] = 34;
                continue;
            }

            if (normalized.Contains("TRICH YEU") || normalized.Contains("NOI DUNG") || normalized.Contains("THUC TRANG") || normalized.Contains("PHAM VI") || normalized.Contains("QUYET DINH") || normalized.Contains("CAU HINH"))
            {
                weights[c] = 26;
                continue;
            }

            if (normalized.Contains("TEN") || normalized.Contains("ĐƠN VỊ") || normalized.Contains("DON VI") || normalized.Contains("CHU QUAN") || normalized.Contains("PHAN MEM") || normalized.Contains("LUU TRU") || normalized.Contains("DUONG TRUYEN"))
            {
                weights[c] = 22;
                continue;
            }

            if (normalized.Contains("NAM") || normalized.Contains("SỐ") || normalized.Contains("SO ") || normalized.Contains("SL") || normalized.Contains("TONG KINH PHI") || normalized.Contains("KINH PHI") || normalized.Contains("TONG SO"))
            {
                weights[c] = 14;
                continue;
            }

            weights[c] = 18;
        }

        return weights;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    private static void ApplyBodyPrintLayout(IXLWorksheet ws, int r1, int r2, int c1, int c2)
    {
        if (r2 < r1)
        {
            return;
        }

        var body = ws.Range(r1, c1, r2, c2);
        body.Style.Alignment.WrapText = true;
        body.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        for (var r = r1; r <= r2; r++)
        {
            var estimatedHeight = EstimateWrappedRowHeight(ws, r, c1, c2);
            ws.Row(r).AdjustToContents();
            if (ws.Row(r).Height < estimatedHeight)
            {
                ws.Row(r).Height = estimatedHeight;
            }
        }
    }

    private static double EstimateWrappedRowHeight(IXLWorksheet ws, int row, int c1, int c2)
    {
        const double baseHeight = 18d;
        const double lineHeight = 15d;
        const double minWidth = 4d;

        var maxLines = 1d;
        for (var c = c1; c <= c2; c++)
        {
            var text = ws.Cell(row, c).GetFormattedString();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var mergedRange = ws.MergedRanges.FirstOrDefault(range =>
            {
                var address = range.RangeAddress;
                return row >= address.FirstAddress.RowNumber
                    && row <= address.LastAddress.RowNumber
                    && c >= address.FirstAddress.ColumnNumber
                    && c <= address.LastAddress.ColumnNumber;
            });

            var firstColumn = c;
            var lastColumn = c;
            if (mergedRange is not null)
            {
                var address = mergedRange.RangeAddress;
                firstColumn = address.FirstAddress.ColumnNumber;
                lastColumn = address.LastAddress.ColumnNumber;
                if (c != firstColumn)
                {
                    continue;
                }
            }

            text = text.Replace("\r", string.Empty)
                .Trim();

            var columnWidth = Math.Max(
                minWidth,
                Enumerable.Range(firstColumn, lastColumn - firstColumn + 1)
                    .Sum(column => ws.Column(column).Width));
            var linesForCell = 1d;

            foreach (var segment in text.Replace("\r", string.Empty).Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    linesForCell += 1d;
                    continue;
                }

                var effectiveWidth = Math.Max(minWidth, columnWidth - 1d);
                linesForCell += Math.Ceiling(segment.Length / effectiveWidth) - 1d;
            }

            if (linesForCell > maxLines)
            {
                maxLines = linesForCell;
            }
        }

        return Math.Max(baseHeight, Math.Min(180d, maxLines * lineHeight));
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
        var keys = new[]
        {
            "CAP_BAC_CONG_AN",
            "TRINH_DO_CNTT",
            "TRINH_DO_LLCT",
            "LOAI_NHAN_LUC",
            "NHOM_NANG_LUC_SO",
            "NHOM_CAMERA",
            "NGUON_VON_DU_AN",
        };
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
