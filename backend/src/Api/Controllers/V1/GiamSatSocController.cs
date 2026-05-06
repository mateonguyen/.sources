using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.GiamSatSoc;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/giam-sat-soc")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class GiamSatSocController : ControllerBase
{
    private readonly IGiamSatSocService _service;

    public GiamSatSocController(IGiamSatSocService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.GiamSatSoc.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<GiamSatSocDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<GiamSatSocDto>>>> GetAll([FromQuery] GiamSatSocQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.GiamSatSoc.Read)]
    [ProducesResponseType(typeof(ApiResponse<GiamSatSocDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GiamSatSocDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("GSS_NOT_FOUND", "Không tìm thấy bản ghi giám sát SOC."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.GiamSatSoc.Create)]
    [ProducesResponseType(typeof(ApiResponse<GiamSatSocDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GiamSatSocDto>>> Create([FromBody] UpsertGiamSatSocRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.GiamSatSoc.Update)]
    [ProducesResponseType(typeof(ApiResponse<GiamSatSocDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GiamSatSocDto>>> Update(long id, [FromBody] UpsertGiamSatSocRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("matrix")]
    [HasPermission(Permissions.GiamSatSoc.Update)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<GiamSatSocDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<GiamSatSocDto>>>> SaveMatrix([FromBody] SaveGiamSatSocMatrixRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.SaveMatrixAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.GiamSatSoc.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
