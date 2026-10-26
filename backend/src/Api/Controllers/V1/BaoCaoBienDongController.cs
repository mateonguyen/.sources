using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.BaoCaoBienDong;
using ThucLuc.Application.Features.BaoCaoSnapshot;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/bien-dong-bao-cao")]
[ApiExplorerSettings(GroupName = "reporting")]
[HasPermission(Permissions.BaoCaoBienDong.Read)]
public sealed class BaoCaoBienDongController : ControllerBase
{
    private readonly IBaoCaoBienDongService _service;
    private readonly IBaoCaoExportService _exportService;

    public BaoCaoBienDongController(IBaoCaoBienDongService service, IBaoCaoExportService exportService)
    {
        _service = service;
        _exportService = exportService;
    }

    [HttpGet("options")]
    [ProducesResponseType(typeof(ApiResponse<BaoCaoBienDongOptionsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BaoCaoBienDongOptionsDto>>> GetOptions(CancellationToken cancellationToken)
        => Ok(ApiResponseFactory.Success(await _service.GetOptionsAsync(cancellationToken)));

    [HttpGet("current")]
    [ProducesResponseType(typeof(ApiResponse<BaoCaoHienTrangOverviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BaoCaoHienTrangOverviewDto>>> GetCurrent(
        [FromQuery] long kyBaoCaoId,
        [FromQuery] long? donViId,
        [FromQuery] string? moduleCode,
        CancellationToken cancellationToken)
        => Ok(ApiResponseFactory.Success(await _service.GetCurrentAsync(kyBaoCaoId, donViId, moduleCode, cancellationToken)));

    [HttpGet("overview")]
    [ProducesResponseType(typeof(ApiResponse<BaoCaoBienDongOverviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BaoCaoBienDongOverviewDto>>> GetOverview(
        [FromQuery] long toKyBaoCaoId,
        [FromQuery] long? fromKyBaoCaoId,
        [FromQuery] long? donViId,
        [FromQuery] string? moduleCode,
        CancellationToken cancellationToken)
        => Ok(ApiResponseFactory.Success(await _service.GetOverviewAsync(toKyBaoCaoId, fromKyBaoCaoId, donViId, moduleCode, cancellationToken)));

    [HttpGet("records")]
    [ProducesResponseType(typeof(ApiResponse<ModuleRecordsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ModuleRecordsDto>>> GetRecords(
        [FromQuery] long kyBaoCaoId,
        [FromQuery] long donViId,
        [FromQuery] string moduleCode,
        CancellationToken cancellationToken)
        => Ok(ApiResponseFactory.Success(await _exportService.GetModuleRecordsAsync(kyBaoCaoId, donViId, moduleCode, cancellationToken)));

    [HttpGet("trend")]
    [ProducesResponseType(typeof(ApiResponse<BaoCaoBienDongTrendDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BaoCaoBienDongTrendDto>>> GetTrend(
        [FromQuery] long donViId,
        [FromQuery] string moduleCode,
        [FromQuery] string metricCode,
        [FromQuery] int limit = 8,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponseFactory.Success(await _service.GetTrendAsync(donViId, moduleCode, metricCode, limit, cancellationToken)));
}
