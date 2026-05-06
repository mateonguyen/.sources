using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.GiaiPhapAttt;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/giai-phap-attt")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class GiaiPhapAtttController : ControllerBase
{
    private readonly IGiaiPhapAtttService _service;

    public GiaiPhapAtttController(IGiaiPhapAtttService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.GiaiPhapAttt.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<GiaiPhapAtttDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<GiaiPhapAtttDto>>>> GetAll([FromQuery] GiaiPhapAtttQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.GiaiPhapAttt.Read)]
    [ProducesResponseType(typeof(ApiResponse<GiaiPhapAtttDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GiaiPhapAtttDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("GPATTT_NOT_FOUND", "Không tìm thấy bản ghi giải pháp ATTT."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.GiaiPhapAttt.Create)]
    [ProducesResponseType(typeof(ApiResponse<GiaiPhapAtttDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GiaiPhapAtttDto>>> Create([FromBody] UpsertGiaiPhapAtttRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.GiaiPhapAttt.Update)]
    [ProducesResponseType(typeof(ApiResponse<GiaiPhapAtttDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GiaiPhapAtttDto>>> Update(long id, [FromBody] UpsertGiaiPhapAtttRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("matrix")]
    [HasPermission(Permissions.GiaiPhapAttt.Update)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<GiaiPhapAtttDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<GiaiPhapAtttDto>>>> SaveMatrix([FromBody] SaveGiaiPhapAtttMatrixRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.SaveMatrixAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.GiaiPhapAttt.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
