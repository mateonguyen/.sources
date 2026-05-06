using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.TongHopTienDo;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/tong-hop-tien-do")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class TongHopTienDoController : ControllerBase
{
    private readonly ITongHopTienDoService _service;

    public TongHopTienDoController(ITongHopTienDoService service)
    {
        _service = service;
    }

    /// <summary>CA tỉnh xem tiến độ PHONG/XA con — số liệu live từ BIZ_*.</summary>
    [HttpGet]
    [HasPermission(Permissions.TongHopTienDo.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<TienDoDonViDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TienDoDonViDto>>>> GetTienDo(
        [FromQuery] TienDoDonViQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetTienDoAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    /// <summary>Đơn vị xem tổng hợp dữ liệu của chính mình (số bản ghi + DaXacNhan).</summary>
    [HttpGet("my-tien-do")]
    [HasPermission(Permissions.TongHopTienDo.XacNhan)]
    [ProducesResponseType(typeof(ApiResponse<TienDoDonViDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TienDoDonViDto>>> GetMyTienDo(
        [FromQuery] string kyBaoCaoCode, CancellationToken cancellationToken)
    {
        var result = await _service.GetMyTienDoAsync(kyBaoCaoCode, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    /// <summary>PHONG/XA bật/tắt flag "Đã xác nhận xong" (không lock BIZ_*).</summary>
    [HttpPost("xac-nhan")]
    [HasPermission(Permissions.TongHopTienDo.XacNhan)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> XacNhan(
        [FromBody] XacNhanRequest request, CancellationToken cancellationToken)
    {
        await _service.XacNhanAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
