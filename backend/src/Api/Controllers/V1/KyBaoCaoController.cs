using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Features.KyBaoCao;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/ky-bao-cao")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class KyBaoCaoController : ControllerBase
{
    private readonly IKyBaoCaoService _kyBaoCaoService;
    private readonly ICurrentUserService _currentUserService;

    public KyBaoCaoController(IKyBaoCaoService kyBaoCaoService, ICurrentUserService currentUserService)
    {
        _kyBaoCaoService = kyBaoCaoService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [HasPermission(Permissions.KyBaoCao.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<KyBaoCaoDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<KyBaoCaoDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _kyBaoCaoService.GetAllAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.KyBaoCao.Read)]
    [ProducesResponseType(typeof(ApiResponse<KyBaoCaoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<KyBaoCaoDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _kyBaoCaoService.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("KY_NOT_FOUND", "Không tìm thấy kỳ báo cáo."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}/tien-do")]
    [HasPermission(Permissions.KyBaoCao.Read)]
    [ProducesResponseType(typeof(ApiResponse<KyBaoCaoTienDoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<KyBaoCaoTienDoDto>>> GetTienDo(long id, CancellationToken cancellationToken)
    {
        var result = await _kyBaoCaoService.GetTienDoAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("current")]
    [HasPermission(Permissions.KyBaoCao.Read)]
    [ProducesResponseType(typeof(ApiResponse<KyBaoCaoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<KyBaoCaoDto>>> GetCurrent(CancellationToken cancellationToken)
    {
        var result = await _kyBaoCaoService.GetCurrentAsync(cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("KY_NOT_FOUND", "Không tìm thấy kỳ báo cáo đang mở."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.KyBaoCao.Create)]
    [ProducesResponseType(typeof(ApiResponse<KyBaoCaoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<KyBaoCaoDto>>> Create([FromBody] CreateKyBaoCaoRequest request, CancellationToken cancellationToken)
    {
        var result = await _kyBaoCaoService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}/don-vi-trang-thai")]
    [HasPermission(Permissions.BaoCaoSnapshot.Read)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<int>>> GetDonViTrangThai(long id, CancellationToken cancellationToken)
    {
        var donViId = _currentUserService.GetCurrentUser().DonViId;
        var result = await _kyBaoCaoService.GetDonViTrangThaiAsync(id, donViId, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.KyBaoCao.Update)]
    [ProducesResponseType(typeof(ApiResponse<KyBaoCaoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<KyBaoCaoDto>>> Update(long id, [FromBody] UpdateKyBaoCaoRequest request, CancellationToken cancellationToken)
    {
        var result = await _kyBaoCaoService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.KyBaoCao.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _kyBaoCaoService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object?>(null));
    }

    [HttpPatch("{id:long}/status")]
    [HasPermission(Permissions.KyBaoCao.Approve)]
    [ProducesResponseType(typeof(ApiResponse<KyBaoCaoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<KyBaoCaoDto>>> UpdateStatus(long id, [FromBody] UpdateKyBaoCaoStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _kyBaoCaoService.UpdateStatusAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }
}
