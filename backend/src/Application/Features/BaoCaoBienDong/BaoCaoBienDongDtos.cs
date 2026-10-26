namespace ThucLuc.Application.Features.BaoCaoBienDong;

public sealed class BaoCaoBienDongOptionsDto
{
    public bool CanViewScope { get; set; }
    public long CurrentDonViId { get; set; }
    public IReadOnlyCollection<BaoCaoBienDongDonViOptionDto> DonVis { get; set; } = Array.Empty<BaoCaoBienDongDonViOptionDto>();
    public IReadOnlyCollection<BaoCaoBienDongKyOptionDto> KyBaoCaos { get; set; } = Array.Empty<BaoCaoBienDongKyOptionDto>();
    public IReadOnlyCollection<BaoCaoBienDongModuleOptionDto> Modules { get; set; } = Array.Empty<BaoCaoBienDongModuleOptionDto>();
}

public sealed class BaoCaoBienDongDonViOptionDto
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string MaDonVi { get; set; } = string.Empty;
    public string TenDonVi { get; set; } = string.Empty;
    public bool HasReportData { get; set; }
}

public sealed class BaoCaoBienDongKyOptionDto
{
    public long Id { get; set; }
    public string KyCode { get; set; } = string.Empty;
    public string TenKy { get; set; } = string.Empty;
    public long? MauBaoCaoId { get; set; }
    public string TenMau { get; set; } = string.Empty;
    public int Nam { get; set; }
    public int? Quy { get; set; }
    public int? Thang { get; set; }
    public IReadOnlyCollection<string> ModuleCodes { get; set; } = Array.Empty<string>();
}

public sealed class BaoCaoBienDongModuleOptionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class BaoCaoBienDongOverviewDto
{
    public long FromKyBaoCaoId { get; set; }
    public string FromKyCode { get; set; } = string.Empty;
    public long ToKyBaoCaoId { get; set; }
    public string ToKyCode { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
    public int ImprovingCount { get; set; }
    public int WorseningCount { get; set; }
    public int StableCount { get; set; }
    public int ChangedCount { get; set; }
    public int MissingDataCount { get; set; }
    public IReadOnlyCollection<BaoCaoBienDongUnitDto> Units { get; set; } = Array.Empty<BaoCaoBienDongUnitDto>();
}

public sealed class BaoCaoHienTrangOverviewDto
{
    public long KyBaoCaoId { get; set; }
    public string KyCode { get; set; } = string.Empty;
    public string TenKy { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
    public int TotalModules { get; set; }
    public int TotalMetrics { get; set; }
    public IReadOnlyCollection<BaoCaoHienTrangUnitDto> Units { get; set; } = Array.Empty<BaoCaoHienTrangUnitDto>();
}

public sealed class BaoCaoHienTrangUnitDto
{
    public long DonViId { get; set; }
    public string MaDonVi { get; set; } = string.Empty;
    public string TenDonVi { get; set; } = string.Empty;
    public int ModuleCount { get; set; }
    public int MetricCount { get; set; }
    public IReadOnlyCollection<BaoCaoHienTrangModuleDto> Modules { get; set; } = Array.Empty<BaoCaoHienTrangModuleDto>();
}

public sealed class BaoCaoHienTrangModuleDto
{
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public IReadOnlyCollection<BaoCaoHienTrangMetricDto> Metrics { get; set; } = Array.Empty<BaoCaoHienTrangMetricDto>();
}

public sealed class BaoCaoHienTrangMetricDto
{
    public string MetricCode { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public decimal? Value { get; set; }
}

public sealed class BaoCaoBienDongUnitDto
{
    public long DonViId { get; set; }
    public string MaDonVi { get; set; } = string.Empty;
    public string TenDonVi { get; set; } = string.Empty;
    public string Status { get; set; } = "STABLE";
    public int ImprovingCount { get; set; }
    public int WorseningCount { get; set; }
    public int ChangedCount { get; set; }
    public int MissingCount { get; set; }
    public IReadOnlyCollection<BaoCaoBienDongModuleDto> Modules { get; set; } = Array.Empty<BaoCaoBienDongModuleDto>();
}

public sealed class BaoCaoBienDongModuleDto
{
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string Status { get; set; } = "STABLE";
    public IReadOnlyCollection<BaoCaoBienDongMetricDto> Metrics { get; set; } = Array.Empty<BaoCaoBienDongMetricDto>();
}

public sealed class BaoCaoBienDongMetricDto
{
    public string MetricCode { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public decimal? FromValue { get; set; }
    public decimal? ToValue { get; set; }
    public decimal? Delta { get; set; }
    public decimal? DeltaPercent { get; set; }
    public string Status { get; set; } = "STABLE";
}

public sealed class BaoCaoBienDongTrendDto
{
    public long DonViId { get; set; }
    public string TenDonVi { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string MetricCode { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public IReadOnlyCollection<BaoCaoBienDongTrendPointDto> Points { get; set; } = Array.Empty<BaoCaoBienDongTrendPointDto>();
}

public sealed class BaoCaoBienDongTrendPointDto
{
    public long KyBaoCaoId { get; set; }
    public string KyCode { get; set; } = string.Empty;
    public string TenKy { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
