namespace ThucLuc.Application.Features.TongHopTienDo;

public interface ITongHopTienDoService
{
    /// <summary>CA tỉnh xem tiến độ các PHONG/XA con — số liệu live từ BIZ_*.</summary>
    Task<IReadOnlyCollection<TienDoDonViDto>> GetTienDoAsync(
        TienDoDonViQuery query, CancellationToken ct);

    /// <summary>Đơn vị xem tổng hợp dữ liệu của chính mình (số bản ghi live + trạng thái DaXacNhan).</summary>
    Task<TienDoDonViDto> GetMyTienDoAsync(string kyBaoCaoCode, CancellationToken ct);

    /// <summary>PHONG/XA bật/tắt flag "Đã xác nhận xong" (không lock BIZ_*).</summary>
    Task XacNhanAsync(XacNhanRequest request, CancellationToken ct);

    /// <summary>Cấp trên xem chi tiết bản ghi live 1 module của đơn vị con trực thuộc.</summary>
    Task<ChiTietModuleDto> GetChiTietModuleAsync(long donViId, string moduleCode, CancellationToken ct);
}
