namespace ThucLuc.Domain.Entities.Business;

public sealed class ThietBiUngDung
{
    public long ThietBiId { get; set; }
    public long HeThongId { get; set; }
    public ThietBiCntt? ThietBi { get; set; }
    public HeThongThongTin? HeThong { get; set; }
}