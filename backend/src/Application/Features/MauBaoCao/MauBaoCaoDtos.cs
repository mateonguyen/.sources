using ThucLuc.Domain.Enums;

namespace ThucLuc.Application.Features.MauBaoCao;

public sealed class MauBaoCaoDto
{
    public long Id { get; set; }
    public string MaMau { get; set; } = string.Empty;
    public string TenMau { get; set; } = string.Empty;
    public string? MoTa { get; set; }
    public TanSuat TanSuat { get; set; }
    public IReadOnlyCollection<string> DanhSachModule { get; set; } = [];
    public bool IsActive { get; set; }
}

public sealed class UpsertMauBaoCaoRequest
{
    public string MaMau { get; set; } = string.Empty;
    public string TenMau { get; set; } = string.Empty;
    public string? MoTa { get; set; }
    public TanSuat TanSuat { get; set; }
    public IReadOnlyCollection<string> DanhSachModule { get; set; } = [];
    public bool IsActive { get; set; } = true;
}

public sealed class MauBaoCaoModuleCatalogGroupDto
{
    public string GroupKey { get; set; } = string.Empty;
    public string GroupLabel { get; set; } = string.Empty;
    public IReadOnlyCollection<MauBaoCaoModuleCatalogItemDto> Items { get; set; } = [];
}

public sealed class MauBaoCaoModuleCatalogItemDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public string GroupLabel { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}