namespace ThucLuc.Application.Features.BaoCaoBienDong;

public static class BaoCaoMetricDirections
{
    public const string UpIsGood = "UP_GOOD";
    public const string DownIsGood = "DOWN_GOOD";
    public const string Neutral = "NEUTRAL";
}

public sealed record BaoCaoMetricDefinition(
    string ModuleCode,
    string ModuleName,
    string MetricCode,
    string MetricName,
    string Unit,
    string Direction,
    bool IsPrimary = false,
    int SortOrder = 0);

/// <summary>
/// Danh mục chỉ số nghiệp vụ dùng chung cho bước chốt snapshot và màn phân tích.
/// Tỷ lệ luôn được lưu kèm tử số/mẫu số để tổng hợp nhiều đơn vị không bị sai do
/// lấy trung bình của các tỷ lệ thành phần.
/// </summary>
public static class BaoCaoMetricCatalog
{
    public static readonly IReadOnlyCollection<BaoCaoMetricDefinition> All =
    [
        M("NHAN_LUC_CNTT", "Nhân lực CNTT", "TONG_NHAN_SU", "Tổng nhân sự CNTT", "người", BaoCaoMetricDirections.UpIsGood, true),

        M("NANG_LUC_SO", "Năng lực số", "SO_DUOC_DANH_GIA", "Số được đánh giá", "người", BaoCaoMetricDirections.UpIsGood, true),
        M("NANG_LUC_SO", "Năng lực số", "SO_DAT", "Số đạt", "người", BaoCaoMetricDirections.UpIsGood),
        M("NANG_LUC_SO", "Năng lực số", "SO_CHUA_DAT", "Số chưa đạt", "người", BaoCaoMetricDirections.DownIsGood),
        M("NANG_LUC_SO", "Năng lực số", "TY_LE_DAT", "Tỷ lệ đạt", "%", BaoCaoMetricDirections.UpIsGood),

        M("DAO_TAO_BOI_DUONG", "Đào tạo bồi dưỡng", "SO_KHOA", "Số khóa đào tạo", "khóa", BaoCaoMetricDirections.UpIsGood, true),
        M("DAO_TAO_BOI_DUONG", "Đào tạo bồi dưỡng", "SO_HOC_VIEN", "Số lượt học viên", "người", BaoCaoMetricDirections.UpIsGood),
        M("DAO_TAO_HOC_VIEN", "Đào tạo học viên", "TONG_HOC_VIEN", "Tổng học viên", "người", BaoCaoMetricDirections.UpIsGood, true),

        M("HE_THONG_THONG_TIN", "Hệ thống thông tin", "SO_HE_THONG", "Số hệ thống", "hệ thống", BaoCaoMetricDirections.Neutral, true),
        M("HE_THONG_THONG_TIN", "Hệ thống thông tin", "SO_SANG_KIEN", "Số hệ thống được công nhận sáng kiến", "hệ thống", BaoCaoMetricDirections.UpIsGood),
        M("HTTT_TIEU_CHUAN", "HTTT tiêu chuẩn", "TONG_DIEM_TRIEN_KHAI", "Tổng điểm triển khai", "điểm", BaoCaoMetricDirections.UpIsGood, true),

        M("DU_AN_CNTT", "Dự án CNTT", "SO_DU_AN", "Số dự án", "dự án", BaoCaoMetricDirections.Neutral, true),
        M("DU_AN_CNTT", "Dự án CNTT", "TONG_KINH_PHI", "Tổng kinh phí", "đồng", BaoCaoMetricDirections.Neutral),

        M("THIET_BI_CNTT", "Thiết bị CNTT", "TONG_THIET_BI", "Tổng thiết bị", "thiết bị", BaoCaoMetricDirections.Neutral, true),
        M("THIET_BI_CNTT", "Thiết bị CNTT", "DANG_SU_DUNG", "Thiết bị đang sử dụng", "thiết bị", BaoCaoMetricDirections.UpIsGood),
        M("THIET_BI_CNTT", "Thiết bị CNTT", "THIET_BI_HONG", "Thiết bị hỏng", "thiết bị", BaoCaoMetricDirections.DownIsGood),
        M("THIET_BI_CNTT", "Thiết bị CNTT", "TY_LE_HONG", "Tỷ lệ thiết bị hỏng", "%", BaoCaoMetricDirections.DownIsGood),

        M("HA_TANG_MANG", "Hạ tầng mạng", "SO_DON_VI_KET_NOI_BCANET", "Đơn vị đã kết nối BCANet", "đơn vị", BaoCaoMetricDirections.UpIsGood, true),
        M("HA_TANG_MANG", "Hạ tầng mạng", "SO_DUONG_TRUYEN", "Tổng đường truyền", "đường truyền", BaoCaoMetricDirections.UpIsGood),
        M("HA_TANG_MANG", "Hạ tầng mạng", "SO_KET_NOI_INTERNET", "Kết nối Internet", "kết nối", BaoCaoMetricDirections.UpIsGood),

        M("GIAM_SAT_NOC", "Giám sát NOC", "TONG_DOI_TUONG", "Tổng đối tượng", "đối tượng", BaoCaoMetricDirections.Neutral),
        M("GIAM_SAT_NOC", "Giám sát NOC", "DA_GIAM_SAT", "Đối tượng đã giám sát", "đối tượng", BaoCaoMetricDirections.UpIsGood, true),
        M("GIAM_SAT_NOC", "Giám sát NOC", "TY_LE_GIAM_SAT", "Tỷ lệ giám sát", "%", BaoCaoMetricDirections.UpIsGood),

        M("GIAM_SAT_SOC", "Giám sát SOC", "TONG_DOI_TUONG", "Tổng đối tượng", "đối tượng", BaoCaoMetricDirections.Neutral),
        M("GIAM_SAT_SOC", "Giám sát SOC", "DA_GIAM_SAT", "Đối tượng đã giám sát", "đối tượng", BaoCaoMetricDirections.UpIsGood, true),
        M("GIAM_SAT_SOC", "Giám sát SOC", "SO_SU_CO", "Số sự cố", "sự cố", BaoCaoMetricDirections.DownIsGood),
        M("GIAM_SAT_SOC", "Giám sát SOC", "TY_LE_KHAC_PHUC", "Tỷ lệ sự cố đã khắc phục", "%", BaoCaoMetricDirections.UpIsGood),

        M("CAMERA_QUAN_LY", "Camera quản lý", "SO_CAMERA_TRANG_BI", "Camera đã trang bị", "camera", BaoCaoMetricDirections.UpIsGood, true),
        M("CAMERA_QUAN_LY", "Camera quản lý", "NHU_CAU_DAU_TU", "Nhu cầu đầu tư", "camera", BaoCaoMetricDirections.DownIsGood),
        M("CAMERA_QUAN_LY", "Camera quản lý", "CAN_BAO_TRI_SUA_CHUA", "Cần bảo trì/sửa chữa", "camera", BaoCaoMetricDirections.DownIsGood),
        M("CAMERA_THUC_TRANG", "Camera thực trạng", "SO_CAMERA_CAU_HINH", "Camera theo cấu hình", "camera", BaoCaoMetricDirections.Neutral),
        M("CAMERA_THUC_TRANG", "Camera thực trạng", "SO_CAMERA_HOAT_DONG", "Camera hoạt động", "camera", BaoCaoMetricDirections.UpIsGood, true),

        M("ATTT_HTTT_VAN_HANH", "ATTT hệ thống vận hành", "SO_HE_THONG", "Số hệ thống", "hệ thống", BaoCaoMetricDirections.Neutral),
        M("ATTT_HTTT_VAN_HANH", "ATTT hệ thống vận hành", "DA_TRIEN_KHAI_PHUONG_AN", "Đã triển khai phương án", "hệ thống", BaoCaoMetricDirections.UpIsGood, true),
        M("ATTT_HTTT_DAU_TU", "ATTT hệ thống đầu tư", "SO_HE_THONG", "Số hệ thống", "hệ thống", BaoCaoMetricDirections.Neutral),
        M("ATTT_HTTT_DAU_TU", "ATTT hệ thống đầu tư", "DA_PHE_DUYET_HO_SO", "Đã phê duyệt hồ sơ", "hệ thống", BaoCaoMetricDirections.UpIsGood, true),
        M("ATTT_HTTT_DAU_TU", "ATTT hệ thống đầu tư", "DA_LONG_GHEP_THUYET_MINH", "Đã lồng ghép thuyết minh", "hệ thống", BaoCaoMetricDirections.UpIsGood),

        M("ATTT_GIAI_PHAP", "Giải pháp ATTT", "TONG_DOI_TUONG", "Tổng đối tượng cần bảo vệ", "thiết bị", BaoCaoMetricDirections.Neutral),
        M("ATTT_GIAI_PHAP", "Giải pháp ATTT", "DA_DUOC_BAO_VE", "Đối tượng đã được bảo vệ", "thiết bị", BaoCaoMetricDirections.UpIsGood, true),
        M("ATTT_GIAI_PHAP", "Giải pháp ATTT", "TY_LE_BAO_VE", "Tỷ lệ được bảo vệ", "%", BaoCaoMetricDirections.UpIsGood),

        M("VAN_BAN_QPPL", "Văn bản QPPL", "SO_VAN_BAN", "Số văn bản", "văn bản", BaoCaoMetricDirections.Neutral, true),
    ];

    public static BaoCaoMetricDefinition? Find(string moduleCode, string metricCode)
        => All.FirstOrDefault(x => x.ModuleCode.Equals(moduleCode, StringComparison.OrdinalIgnoreCase)
            && x.MetricCode.Equals(metricCode, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyCollection<BaoCaoMetricDefinition> ForModule(string moduleCode)
        => All.Where(x => x.ModuleCode.Equals(moduleCode, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.SortOrder)
            .ToArray();

    private static BaoCaoMetricDefinition M(
        string moduleCode,
        string moduleName,
        string metricCode,
        string metricName,
        string unit,
        string direction,
        bool primary = false,
        int sortOrder = 0)
        => new(moduleCode, moduleName, metricCode, metricName, unit, direction, primary, sortOrder);
}
