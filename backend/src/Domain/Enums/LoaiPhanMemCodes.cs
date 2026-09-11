namespace ThucLuc.Domain.Enums;

/// <summary>
/// Phân nhóm phần mềm/CSDL theo đúng hai tiểu mục của biểu mẫu HTTT.
/// CHUA_PHAN_LOAI chỉ dùng để giữ an toàn dữ liệu được tạo trước khi có phân nhóm.
/// </summary>
public static class LoaiPhanMemCodes
{
    public const string DungChung = "DUNG_CHUNG";
    public const string TuPhatTrien = "TU_PHAT_TRIEN";
    public const string ChuaPhanLoai = "CHUA_PHAN_LOAI";

    public static bool IsSelectable(string? value)
        => value is DungChung or TuPhatTrien;
}
