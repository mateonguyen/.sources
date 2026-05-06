using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.YeuCauBoSung;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/yeu-cau-bo-sung")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class YeuCauBoSungController : ControllerBase
{
    private readonly IYeuCauBoSungService _service;

    public YeuCauBoSungController(IYeuCauBoSungService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.YeuCauBoSung.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<YeuCauBoSungDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<YeuCauBoSungDto>>>> GetByKy([FromQuery] long kyBaoCaoId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByKyAsync(kyBaoCaoId, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.YeuCauBoSung.Create)]
    [ProducesResponseType(typeof(ApiResponse<YeuCauBoSungDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<YeuCauBoSungDto>>> Create([FromBody] CreateYeuCauBoSungRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("{id:long}/duyet")]
    [HasPermission(Permissions.YeuCauBoSung.Approve)]
    [ProducesResponseType(typeof(ApiResponse<YeuCauBoSungDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<YeuCauBoSungDto>>> Duyet(long id, [FromBody] DuyetYeuCauRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.DuyetAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("{id:long}/tu-choi")]
    [HasPermission(Permissions.YeuCauBoSung.Approve)]
    [ProducesResponseType(typeof(ApiResponse<YeuCauBoSungDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<YeuCauBoSungDto>>> TuChoi(long id, [FromBody] TuChoiYeuCauRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.TuChoiAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }
}