using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.VanBanQppl;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/van-ban-qppl")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class VanBanQpplController : ControllerBase
{
    private readonly IVanBanQpplService _service;

    public VanBanQpplController(IVanBanQpplService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.VanBanQppl.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<VanBanQpplDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<VanBanQpplDto>>>> GetAll([FromQuery] GetVanBanQpplQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.VanBanQppl.Read)]
    [ProducesResponseType(typeof(ApiResponse<VanBanQpplDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VanBanQpplDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("VANBAN_NOT_FOUND", "Không tìm thấy văn bản QPPL."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.VanBanQppl.Create)]
    [ProducesResponseType(typeof(ApiResponse<VanBanQpplDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VanBanQpplDto>>> Create([FromBody] UpsertVanBanQpplRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.VanBanQppl.Update)]
    [ProducesResponseType(typeof(ApiResponse<VanBanQpplDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VanBanQpplDto>>> Update(long id, [FromBody] UpsertVanBanQpplRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.VanBanQppl.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
