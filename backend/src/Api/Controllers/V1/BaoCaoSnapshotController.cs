using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Features.BaoCaoSnapshot;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/snapshot")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class BaoCaoSnapshotController : ControllerBase
{
    private readonly IBaoCaoSnapshotService _snapshotService;
    private readonly IBaoCaoExportService _exportService;
    private readonly ICurrentUserService _currentUserService;

    public BaoCaoSnapshotController(
        IBaoCaoSnapshotService snapshotService,
        IBaoCaoExportService exportService,
        ICurrentUserService currentUserService)
    {
        _snapshotService = snapshotService;
        _exportService = exportService;
        _currentUserService = currentUserService;
    }

    [HttpGet("preview/dao-tao")]
    [HasPermission(Permissions.BaoCaoSnapshot.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<DaoTaoBoiDuongPreviewItem>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<DaoTaoBoiDuongPreviewItem>>>> PreviewDaoTao(
        [FromQuery] long kyBaoCaoId,
        [FromQuery] long donViId,
        CancellationToken cancellationToken)
    {
        var result = await _snapshotService.PreviewDaoTaoAsync(kyBaoCaoId, donViId, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet]
    [HasPermission(Permissions.BaoCaoSnapshot.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<BaoCaoSnapshotDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<BaoCaoSnapshotDto>>>> GetByKy(
        [FromQuery] long kyBaoCaoId,
        CancellationToken cancellationToken)
    {
        var result = await _snapshotService.GetByKyAsync(kyBaoCaoId, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("latest-by-don-vi")]
    [HasPermission(Permissions.BaoCaoSnapshot.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<BaoCaoSnapshotDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<BaoCaoSnapshotDto>>>> GetLatestByDonVi(
        [FromQuery] long? kyBaoCaoId,
        CancellationToken cancellationToken)
    {
        var result = await _snapshotService.GetLatestByDonViAsync(kyBaoCaoId, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("compare")]
    [HasPermission(Permissions.BaoCaoSnapshot.Read)]
    [ProducesResponseType(typeof(ApiResponse<SnapshotCompareDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SnapshotCompareDto>>> CompareTwoKy(
        [FromQuery] long donViId,
        [FromQuery] long fromKyBaoCaoId,
        [FromQuery] long toKyBaoCaoId,
        CancellationToken cancellationToken)
    {
        var result = await _snapshotService.CompareTwoKyAsync(donViId, fromKyBaoCaoId, toKyBaoCaoId, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("build")]
    [HasPermission(Permissions.BaoCaoSnapshot.Read)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<string>>> BuildSnapshotJson(
        [FromQuery] long kyId,
        [FromQuery] long donViId,
        CancellationToken cancellationToken)
    {
        var result = await _snapshotService.BuildSnapshotJsonAsync(kyId, donViId, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("create-draft")]
    [HasPermission(Permissions.BaoCaoSnapshot.Create)]
    [ProducesResponseType(typeof(ApiResponse<BaoCaoSnapshotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BaoCaoSnapshotDto>>> CreateDraft(
        [FromBody] CreateBaoCaoSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _snapshotService.CreateDraftAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.BaoCaoSnapshot.Read)]
    [ProducesResponseType(typeof(ApiResponse<BaoCaoSnapshotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BaoCaoSnapshotDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _snapshotService.GetAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("SNAPSHOT_NOT_FOUND", "Không tìm thấy snapshot."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPatch("{id:long}")]
    [HasPermission(Permissions.BaoCaoSnapshot.Update)]
    [ProducesResponseType(typeof(ApiResponse<BaoCaoSnapshotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BaoCaoSnapshotDto>>> Update(
        long id,
        [FromBody] UpdateBaoCaoSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _snapshotService.UpdateDraftAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("{id:long}/submit")]
    [HasPermission(Permissions.BaoCaoSnapshot.Submit)]
    [ProducesResponseType(typeof(ApiResponse<BaoCaoSnapshotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BaoCaoSnapshotDto>>> Submit(
        long id,
        [FromBody] SubmitBaoCaoSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _snapshotService.SubmitAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("submit-current")]
    [HasPermission(Permissions.BaoCaoSnapshot.Submit)]
    [ProducesResponseType(typeof(ApiResponse<BaoCaoSnapshotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BaoCaoSnapshotDto>>> SubmitCurrent(
        [FromBody] SubmitCurrentSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _snapshotService.SubmitCurrentAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("finalize-module")]
    [HasPermission(Permissions.BaoCaoSnapshot.Submit)]
    [ProducesResponseType(typeof(ApiResponse<FinalizeBizModuleResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FinalizeBizModuleResult>>> FinalizeModule(
        [FromBody] FinalizeBizModuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _snapshotService.FinalizeBizModuleAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("mo-lai")]
    [HasPermission(Permissions.BaoCaoSnapshot.Update)]
    [ProducesResponseType(typeof(ApiResponse<BaoCaoSnapshotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BaoCaoSnapshotDto>>> MoLai(
        [FromQuery] long kyBaoCaoId,
        [FromQuery] long donViId,
        CancellationToken cancellationToken)
    {
        var result = await _snapshotService.MoLaiAsync(kyBaoCaoId, donViId, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.BaoCaoSnapshot.Update)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(long id, CancellationToken cancellationToken)
    {
        await _snapshotService.CancelAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }

    [HttpGet("module-status")]
    [HasPermission(Permissions.BaoCaoSnapshot.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<ModuleStatusDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ModuleStatusDto>>>> GetModuleStatus(
        [FromQuery] long kyBaoCaoId,
        CancellationToken cancellationToken)
    {
        var donViId = _currentUserService.GetCurrentUser().DonViId;
        var result = await _snapshotService.GetModuleStatusAsync(kyBaoCaoId, donViId, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("submit-context")]
    [HasPermission(Permissions.BaoCaoSnapshot.Read)]
    [ProducesResponseType(typeof(ApiResponse<SubmitSnapshotContextDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SubmitSnapshotContextDto>>> GetSubmitContext(
        [FromQuery] long kyBaoCaoId,
        CancellationToken cancellationToken)
    {
        var donViId = _currentUserService.GetCurrentUser().DonViId;
        var result = await _snapshotService.GetSubmitContextAsync(kyBaoCaoId, donViId, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}/breakdown")]
    [HasPermission(Permissions.BaoCaoSnapshot.Read)]
    [ProducesResponseType(typeof(ApiResponse<SnapshotBreakdownDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SnapshotBreakdownDto>>> GetBreakdown(long id, CancellationToken cancellationToken)
    {
        var result = await _snapshotService.GetBreakdownAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}/pdf")]
    [HasPermission(Permissions.BaoCaoSnapshot.Export)]
    [ProducesResponseType(typeof(ApiResponse<BaoCaoPdfResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BaoCaoPdfResultDto>>> GetPdf(long id, CancellationToken cancellationToken)
    {
        var result = await _snapshotService.GeneratePdfAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    /// <summary>Xuất biểu mẫu báo cáo (mẫu H05) từ dữ liệu đã chốt. format = xlsx | pdf.</summary>
    [HttpGet("{id:long}/export")]
    [HasPermission(Permissions.BaoCaoSnapshot.Export)]
    [ProducesResponseType(typeof(ApiResponse<BaoCaoExportResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BaoCaoExportResultDto>>> Export(
        long id,
        [FromQuery] string format,
        CancellationToken cancellationToken)
    {
        var result = await _exportService.ExportAsync(id, format, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }
}