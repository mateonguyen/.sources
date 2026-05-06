using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.GiamSatNoc;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/giam-sat-noc")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class GiamSatNocController : ControllerBase
{
    private readonly IGiamSatNocService _service;

    public GiamSatNocController(IGiamSatNocService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.GiamSatNoc.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<GiamSatNocDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<GiamSatNocDto>>>> GetAll([FromQuery] GiamSatNocQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.GiamSatNoc.Read)]
    [ProducesResponseType(typeof(ApiResponse<GiamSatNocDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GiamSatNocDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("GSN_NOT_FOUND", "Không tìm thấy bản ghi giám sát NOC."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.GiamSatNoc.Create)]
    [ProducesResponseType(typeof(ApiResponse<GiamSatNocDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GiamSatNocDto>>> Create([FromBody] UpsertGiamSatNocRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.GiamSatNoc.Update)]
    [ProducesResponseType(typeof(ApiResponse<GiamSatNocDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GiamSatNocDto>>> Update(long id, [FromBody] UpsertGiamSatNocRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("matrix")]
    [HasPermission(Permissions.GiamSatNoc.Update)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<GiamSatNocDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<GiamSatNocDto>>>> SaveMatrix([FromBody] SaveGiamSatNocMatrixRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.SaveMatrixAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.GiamSatNoc.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
