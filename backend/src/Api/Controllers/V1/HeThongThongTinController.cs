using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.HeThongThongTin;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/he-thong-thong-tin")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class HeThongThongTinController : ControllerBase
{
    private readonly IHeThongThongTinService _service;

    public HeThongThongTinController(IHeThongThongTinService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.HeThongThongTin.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<HeThongThongTinDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<HeThongThongTinDto>>>> GetAll([FromQuery] string? loaiPhanMem, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(loaiPhanMem, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.HeThongThongTin.Read)]
    [ProducesResponseType(typeof(ApiResponse<HeThongThongTinDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HeThongThongTinDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("HTTT_NOT_FOUND", "Không tìm thấy hệ thống thông tin."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.HeThongThongTin.Create)]
    [ProducesResponseType(typeof(ApiResponse<HeThongThongTinDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HeThongThongTinDto>>> Create([FromBody] UpsertHeThongThongTinRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.HeThongThongTin.Update)]
    [ProducesResponseType(typeof(ApiResponse<HeThongThongTinDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HeThongThongTinDto>>> Update(long id, [FromBody] UpsertHeThongThongTinRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.HeThongThongTin.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}