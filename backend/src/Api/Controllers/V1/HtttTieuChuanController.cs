using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.HtttTieuChuan;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/httt-tieu-chuan")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class HtttTieuChuanController : ControllerBase
{
    private readonly IHtttTieuChuanService _service;

    public HtttTieuChuanController(IHtttTieuChuanService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.HeThongThongTin.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<HtttTieuChuanDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<HtttTieuChuanDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.HeThongThongTin.Read)]
    [ProducesResponseType(typeof(ApiResponse<HtttTieuChuanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HtttTieuChuanDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("HTTTTC_NOT_FOUND", "Không tìm thấy bản ghi HTTT tiêu chuẩn."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.HeThongThongTin.Create)]
    [ProducesResponseType(typeof(ApiResponse<HtttTieuChuanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HtttTieuChuanDto>>> Create([FromBody] UpsertHtttTieuChuanRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.HeThongThongTin.Update)]
    [ProducesResponseType(typeof(ApiResponse<HtttTieuChuanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HtttTieuChuanDto>>> Update(long id, [FromBody] UpsertHtttTieuChuanRequest request, CancellationToken cancellationToken)
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
