using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Features.NhanLucCntt;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/nhan-luc-cntt")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class NhanLucCnttController : ControllerBase
{
    private readonly INhanLucCnttService _nhanLucCnttService;

    public NhanLucCnttController(INhanLucCnttService nhanLucCnttService)
    {
        _nhanLucCnttService = nhanLucCnttService;
    }

    [HttpGet]
    [HasPermission(Permissions.NhanLucCntt.Read)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NhanLucCnttDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<NhanLucCnttDto>>>> GetAll([FromQuery] NhanLucCnttQuery query, CancellationToken cancellationToken)
    {
        var result = await _nhanLucCnttService.GetPagedAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.NhanLucCntt.Read)]
    [ProducesResponseType(typeof(ApiResponse<NhanLucCnttDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NhanLucCnttDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _nhanLucCnttService.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("NHANLUC_NOT_FOUND", "Không tìm thấy nhân lực CNTT."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("timeline/{nhanSuKey}")]
    [HasPermission(Permissions.NhanLucCntt.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<NhanLucCnttDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<NhanLucCnttDto>>>> GetTimeline(string nhanSuKey, CancellationToken cancellationToken)
    {
        var result = await _nhanLucCnttService.GetTimelineAsync(nhanSuKey, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.NhanLucCntt.Create)]
    [ProducesResponseType(typeof(ApiResponse<NhanLucCnttDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NhanLucCnttDto>>> Create([FromBody] UpsertNhanLucCnttRequest request, CancellationToken cancellationToken)
    {
        var result = await _nhanLucCnttService.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.NhanLucCntt.Update)]
    [ProducesResponseType(typeof(ApiResponse<NhanLucCnttDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NhanLucCnttDto>>> Update(long id, [FromBody] UpsertNhanLucCnttRequest request, CancellationToken cancellationToken)
    {
        var result = await _nhanLucCnttService.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.NhanLucCntt.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _nhanLucCnttService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
