using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class GiaiPhapAttt : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }
    public string TenGiaiPhap { get; set; } = string.Empty;
    public int MayTinhBcanetSl { get; set; }
    public int MayTinhBcanetTs { get; set; }
    public int MayTinhInternetSl { get; set; }
    public int MayTinhInternetTs { get; set; }
    public int MayTinhLocalSl { get; set; }
    public int MayTinhLocalTs { get; set; }
    public int MayChuBcanetSl { get; set; }
    public int MayChuBcanetTs { get; set; }
    public int MayChuInternetSl { get; set; }
    public int MayChuInternetTs { get; set; }
    public int MayChuLocalSl { get; set; }
    public int MayChuLocalTs { get; set; }
    public string? GhiChu { get; set; }
}
