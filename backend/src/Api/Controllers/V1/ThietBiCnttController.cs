using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.ThietBiCntt;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/thiet-bi-cntt")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class ThietBiCnttController : ControllerBase
{
    private readonly IThietBiCnttService _service;

    public ThietBiCnttController(IThietBiCnttService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.ThietBiCntt.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<ThietBiCnttDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ThietBiCnttDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("catalog")]
    [HasPermission(Permissions.ThietBiCntt.Read)]
    [ProducesResponseType(typeof(ApiResponse<ThietBiCatalogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ThietBiCatalogDto>>> GetCatalog(CancellationToken cancellationToken)
    {
        var result = await _service.GetCatalogAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.ThietBiCntt.Read)]
    [ProducesResponseType(typeof(ApiResponse<ThietBiCnttDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ThietBiCnttDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("TBCNTT_NOT_FOUND", "Không tìm thấy bản ghi thiết bị CNTT."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.ThietBiCntt.Create)]
    [ProducesResponseType(typeof(ApiResponse<ThietBiCnttDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ThietBiCnttDto>>> Create([FromBody] UpsertThietBiCnttRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.ThietBiCntt.Update)]
    [ProducesResponseType(typeof(ApiResponse<ThietBiCnttDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ThietBiCnttDto>>> Update(long id, [FromBody] UpsertThietBiCnttRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.ThietBiCntt.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
