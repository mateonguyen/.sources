using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.CameraQuanLy;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/camera-quan-ly")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class CameraQuanLyController : ControllerBase
{
    private readonly ICameraQuanLyService _service;

    public CameraQuanLyController(ICameraQuanLyService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.CameraQuanLy.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<CameraQuanLyDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CameraQuanLyDto>>>> GetAll([FromQuery] GetCameraQuanLyQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.CameraQuanLy.Read)]
    [ProducesResponseType(typeof(ApiResponse<CameraQuanLyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CameraQuanLyDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("CQL_NOT_FOUND", "Không tìm thấy bản ghi camera quản lý."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.CameraQuanLy.Create)]
    [ProducesResponseType(typeof(ApiResponse<CameraQuanLyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CameraQuanLyDto>>> Create([FromBody] UpsertCameraQuanLyRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.CameraQuanLy.Update)]
    [ProducesResponseType(typeof(ApiResponse<CameraQuanLyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CameraQuanLyDto>>> Update(long id, [FromBody] UpsertCameraQuanLyRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.CameraQuanLy.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
