using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.AtttHtttVanHanh;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/attt-httt-van-hanh")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class AtttHtttVanHanhController : ControllerBase
{
    private readonly IAtttHtttVanHanhService _service;

    public AtttHtttVanHanhController(IAtttHtttVanHanhService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.AtttHtttVanHanh.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<AtttHtttVanHanhDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AtttHtttVanHanhDto>>>> GetAll([FromQuery] AtttHtttVanHanhQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.AtttHtttVanHanh.Read)]
    [ProducesResponseType(typeof(ApiResponse<AtttHtttVanHanhDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AtttHtttVanHanhDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("ATTTVH_NOT_FOUND", "Không tìm thấy bản ghi ATTT HTTT vận hành."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.AtttHtttVanHanh.Create)]
    [ProducesResponseType(typeof(ApiResponse<AtttHtttVanHanhDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AtttHtttVanHanhDto>>> Create([FromBody] UpsertAtttHtttVanHanhRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.AtttHtttVanHanh.Update)]
    [ProducesResponseType(typeof(ApiResponse<AtttHtttVanHanhDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AtttHtttVanHanhDto>>> Update(long id, [FromBody] UpsertAtttHtttVanHanhRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.AtttHtttVanHanh.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
