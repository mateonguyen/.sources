using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.AtttHtttDauTu;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/attt-httt-dau-tu")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class AtttHtttDauTuController : ControllerBase
{
    private readonly IAtttHtttDauTuService _service;

    public AtttHtttDauTuController(IAtttHtttDauTuService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.AtttHtttDauTu.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<AtttHtttDauTuDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AtttHtttDauTuDto>>>> GetAll([FromQuery] AtttHtttDauTuQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.AtttHtttDauTu.Read)]
    [ProducesResponseType(typeof(ApiResponse<AtttHtttDauTuDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AtttHtttDauTuDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("ATTTDT_NOT_FOUND", "Không tìm thấy bản ghi ATTT HTTT đầu tư."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.AtttHtttDauTu.Create)]
    [ProducesResponseType(typeof(ApiResponse<AtttHtttDauTuDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AtttHtttDauTuDto>>> Create([FromBody] UpsertAtttHtttDauTuRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.AtttHtttDauTu.Update)]
    [ProducesResponseType(typeof(ApiResponse<AtttHtttDauTuDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AtttHtttDauTuDto>>> Update(long id, [FromBody] UpsertAtttHtttDauTuRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.AtttHtttDauTu.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
