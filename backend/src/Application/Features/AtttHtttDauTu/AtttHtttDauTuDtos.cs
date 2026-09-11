namespace ThucLuc.Application.Features.AtttHtttDauTu;

public sealed class AtttHtttDauTuQuery
{
    public long? DonViId { get; set; }
}

public sealed class AtttHtttDauTuDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public long HtttId { get; set; }
    public string LoaiHaTang { get; set; } = "KHAC";
    public string? ChuQuan { get; set; }
    public string? DonViVanHanh { get; set; }
    public string? CapDoDeXuat { get; set; }
    public DateOnly? NgayPheDuyetHsdxcd { get; set; }
    public string? QuyetDinhPheDuyet { get; set; }
    public bool DaLongGhepThuyetMinh { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class UpsertAtttHtttDauTuRequest
{
    public long DonViId { get; set; }
    public long HtttId { get; set; }
    public string LoaiHaTang { get; set; } = "KHAC";
    public string? ChuQuan { get; set; }
    public string? DonViVanHanh { get; set; }
    public string? CapDoDeXuat { get; set; }
    public DateOnly? NgayPheDuyetHsdxcd { get; set; }
    public string? QuyetDinhPheDuyet { get; set; }
    public bool DaLongGhepThuyetMinh { get; set; }
    public string? GhiChu { get; set; }
}
