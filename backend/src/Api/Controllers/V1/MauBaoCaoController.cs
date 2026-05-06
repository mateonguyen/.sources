using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.MauBaoCao;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/mau-bao-cao")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class MauBaoCaoController : ControllerBase
{
    private readonly IMauBaoCaoService _mauBaoCaoService;

    public MauBaoCaoController(IMauBaoCaoService mauBaoCaoService)
    {
        _mauBaoCaoService = mauBaoCaoService;
    }

    [HttpGet]
    [HasPermission(Permissions.MauBaoCao.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<MauBaoCaoDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<MauBaoCaoDto>>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mauBaoCaoService.GetAllAsync(includeInactive, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("module-catalog")]
    [HasPermission(Permissions.MauBaoCao.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<MauBaoCaoModuleCatalogGroupDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<MauBaoCaoModuleCatalogGroupDto>>>> GetModuleCatalog(CancellationToken cancellationToken)
    {
        var result = await _mauBaoCaoService.GetModuleCatalogAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.MauBaoCao.Read)]
    [ProducesResponseType(typeof(ApiResponse<MauBaoCaoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MauBaoCaoDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _mauBaoCaoService.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("MAU_BAO_CAO_NOT_FOUND", "Không tìm thấy mẫu báo cáo."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.MauBaoCao.Create)]
    [ProducesResponseType(typeof(ApiResponse<MauBaoCaoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MauBaoCaoDto>>> Create([FromBody] UpsertMauBaoCaoRequest request, CancellationToken cancellationToken)
    {
        var result = await _mauBaoCaoService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.MauBaoCao.Update)]
    [ProducesResponseType(typeof(ApiResponse<MauBaoCaoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MauBaoCaoDto>>> Update(long id, [FromBody] UpsertMauBaoCaoRequest request, CancellationToken cancellationToken)
    {
        var result = await _mauBaoCaoService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.MauBaoCao.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _mauBaoCaoService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}