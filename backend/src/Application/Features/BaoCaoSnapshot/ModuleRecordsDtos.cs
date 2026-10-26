namespace ThucLuc.Application.Features.BaoCaoSnapshot;

public sealed class ModuleRecordsDto
{
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public IReadOnlyCollection<ModuleRecordSectionDto> Sections { get; set; } = Array.Empty<ModuleRecordSectionDto>();
}

/// <summary>
/// 1 bảng con trong danh sách chi tiết module. Đa số module chỉ có 1 section
/// (Title rỗng); vài module chia theo nhóm/loại (VD Nhân lực CNTT theo
/// Chuyên trách/Kiêm nhiệm, Hệ thống thông tin theo Dùng chung/Tự phát
/// triển) nên mỗi nhóm là 1 section riêng, có thể khác Headers nhau.
/// </summary>
public sealed class ModuleRecordSectionDto
{
    public string Title { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Headers { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<object?[]> Rows { get; set; } = Array.Empty<object?[]>();
}
