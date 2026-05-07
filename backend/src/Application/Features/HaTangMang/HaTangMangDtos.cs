namespace ThucLuc.Application.Features.HaTangMang;

public sealed class HaTangMangDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public int SoDonViTrucThuoc { get; set; }
    public int SoDaKetNoiBcanet { get; set; }
    public int SoDuongTruyenVnpt { get; set; }
    public int SoDuongTruyenKhac { get; set; }
    public int SoKetNoiInternet { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class UpsertHaTangMangRequest
{
    public long DonViId { get; set; }
    public int SoDonViTrucThuoc { get; set; }
    public int SoDaKetNoiBcanet { get; set; }
    public int SoDuongTruyenVnpt { get; set; }
    public int SoDuongTruyenKhac { get; set; }
    public int SoKetNoiInternet { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class SaveHaTangMangMatrixRequest
{
    public long DonViId { get; set; }
    public List<UpsertHaTangMangRequest> Items { get; set; } = [];
}
