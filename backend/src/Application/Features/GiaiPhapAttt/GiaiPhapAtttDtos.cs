namespace ThucLuc.Application.Features.GiaiPhapAttt;

public sealed class GiaiPhapAtttQuery
{
    public long? DonViId { get; set; }
}

public sealed class GiaiPhapAtttDto
{
    public long Id { get; set; }
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

public sealed class UpsertGiaiPhapAtttRequest
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

public sealed class SaveGiaiPhapAtttMatrixRequest
{
    public long DonViId { get; set; }
    public List<UpsertGiaiPhapAtttRequest> Items { get; set; } = [];
}
