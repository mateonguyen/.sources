using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.NangLucSo;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/nang-luc-so")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class NangLucSoController : ControllerBase
{
    private readonly INangLucSoService _service;

    public NangLucSoController(INangLucSoService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.NangLucSo.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<NangLucSoDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<NangLucSoDto>>>> GetAll(
        [FromQuery] NangLucSoQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.NangLucSo.Read)]
    [ProducesResponseType(typeof(ApiResponse<NangLucSoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NangLucSoDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("NLS_NOT_FOUND", "Không tìm thấy bản ghi năng lực số."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.NangLucSo.Create)]
    [ProducesResponseType(typeof(ApiResponse<NangLucSoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NangLucSoDto>>> Create([FromBody] UpsertNangLucSoRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.NangLucSo.Update)]
    [ProducesResponseType(typeof(ApiResponse<NangLucSoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NangLucSoDto>>> Update(long id, [FromBody] UpsertNangLucSoRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("matrix")]
    [HasPermission(Permissions.NangLucSo.Update)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<NangLucSoDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<NangLucSoDto>>>> SaveMatrix([FromBody] SaveNangLucSoMatrixRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.SaveMatrixAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("finalize")]
    [HasPermission(Permissions.NangLucSo.Update)]
    [ProducesResponseType(typeof(ApiResponse<FinalizeNangLucSoResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FinalizeNangLucSoResult>>> Finalize(
        [FromBody] FinalizeNangLucSoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.FinalizeAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.NangLucSo.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
