using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.CameraThucTrang;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/camera-thuc-trang")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class CameraThucTrangController : ControllerBase
{
    private readonly ICameraThucTrangService _service;

    public CameraThucTrangController(ICameraThucTrangService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.CameraThucTrang.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<CameraThucTrangDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CameraThucTrangDto>>>> GetAll([FromQuery] GetCameraThucTrangQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.CameraThucTrang.Read)]
    [ProducesResponseType(typeof(ApiResponse<CameraThucTrangDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CameraThucTrangDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("CTT_NOT_FOUND", "Không tìm thấy bản ghi camera thực trạng."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.CameraThucTrang.Create)]
    [ProducesResponseType(typeof(ApiResponse<CameraThucTrangDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CameraThucTrangDto>>> Create([FromBody] UpsertCameraThucTrangRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.CameraThucTrang.Update)]
    [ProducesResponseType(typeof(ApiResponse<CameraThucTrangDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CameraThucTrangDto>>> Update(long id, [FromBody] UpsertCameraThucTrangRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.CameraThucTrang.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
