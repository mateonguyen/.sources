using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class AtttHtttVanHanh : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }
    public long HtttId { get; set; }
    public string? ChuQuan { get; set; }
    public string? DonViVanHanh { get; set; }
    public string? CapDoDeXuat { get; set; }
    public string? TinhTrangPheDuyet { get; set; }
    public string? QuyetDinhPheDuyet { get; set; }
    public string? QuyCheAttt { get; set; }
    public DateOnly? DuKienNgayPheDuyet { get; set; }
    public bool DaTrienKhaiPhuongAn { get; set; }
    public DateOnly? DuKienNgayTrienKhai { get; set; }
    public string? KiemTraDanhGia { get; set; }
    public string? GhiChu { get; set; }
}
