using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.HaTangMang;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/ha-tang-mang")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class HaTangMangController : ControllerBase
{
    private readonly IHaTangMangService _service;

    public HaTangMangController(IHaTangMangService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.HaTangMang.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<HaTangMangDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<HaTangMangDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.HaTangMang.Read)]
    [ProducesResponseType(typeof(ApiResponse<HaTangMangDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HaTangMangDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("HTM_NOT_FOUND", "Không tìm thấy bản ghi hạ tầng mạng."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.HaTangMang.Create)]
    [ProducesResponseType(typeof(ApiResponse<HaTangMangDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HaTangMangDto>>> Create([FromBody] UpsertHaTangMangRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.HaTangMang.Update)]
    [ProducesResponseType(typeof(ApiResponse<HaTangMangDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HaTangMangDto>>> Update(long id, [FromBody] UpsertHaTangMangRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.HaTangMang.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
