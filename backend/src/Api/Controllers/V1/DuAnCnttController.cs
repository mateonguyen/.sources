using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.DuAnCntt;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/du-an-cntt")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class DuAnCnttController : ControllerBase
{
    private readonly IDuAnCnttService _service;

    public DuAnCnttController(IDuAnCnttService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.DuAnCntt.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<DuAnCnttDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<DuAnCnttDto>>>> GetAll([FromQuery] GetDuAnCnttQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.DuAnCntt.Read)]
    [ProducesResponseType(typeof(ApiResponse<DuAnCnttDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DuAnCnttDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("DU_AN_NOT_FOUND", "Không tìm thấy bản ghi dự án CNTT."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.DuAnCntt.Create)]
    [ProducesResponseType(typeof(ApiResponse<DuAnCnttDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DuAnCnttDto>>> Create([FromBody] UpsertDuAnCnttRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.DuAnCntt.Update)]
    [ProducesResponseType(typeof(ApiResponse<DuAnCnttDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DuAnCnttDto>>> Update(long id, [FromBody] UpsertDuAnCnttRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.DuAnCntt.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}