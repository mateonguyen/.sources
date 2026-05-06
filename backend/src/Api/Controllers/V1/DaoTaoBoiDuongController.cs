using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.DaoTaoBoiDuong;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/dao-tao-boi-duong")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class DaoTaoBoiDuongController : ControllerBase
{
    private readonly IDaoTaoBoiDuongService _service;

    public DaoTaoBoiDuongController(IDaoTaoBoiDuongService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.DaoTaoBoiDuong.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<DaoTaoBoiDuongDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<DaoTaoBoiDuongDto>>>> GetAll(
        [FromQuery] DaoTaoBoiDuongQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.DaoTaoBoiDuong.Read)]
    [ProducesResponseType(typeof(ApiResponse<DaoTaoBoiDuongDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DaoTaoBoiDuongDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("DTBD_NOT_FOUND", "Không tìm thấy bản ghi đào tạo bồi dưỡng."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.DaoTaoBoiDuong.Create)]
    [ProducesResponseType(typeof(ApiResponse<DaoTaoBoiDuongDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DaoTaoBoiDuongDto>>> Create([FromBody] UpsertDaoTaoBoiDuongRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.DaoTaoBoiDuong.Update)]
    [ProducesResponseType(typeof(ApiResponse<DaoTaoBoiDuongDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DaoTaoBoiDuongDto>>> Update(long id, [FromBody] UpsertDaoTaoBoiDuongRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("finalize")]
    [HasPermission(Permissions.DaoTaoBoiDuong.Update)]
    [ProducesResponseType(typeof(ApiResponse<FinalizeDaoTaoBoiDuongResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FinalizeDaoTaoBoiDuongResult>>> Finalize(
        [FromBody] FinalizeDaoTaoBoiDuongRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.FinalizeAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.DaoTaoBoiDuong.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
