using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.DonVi;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/don-vi/changes")]
[Authorize(Roles = "SYSTEM_ADMIN")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class DonViChangeController : ControllerBase
{
    private readonly IDonViChangeService _service;
    public DonViChangeController(IDonViChangeService service) => _service = service;

    [HttpPost("merge/preview")]
    public async Task<ActionResult<ApiResponse<DonViChangePreviewDto>>> PreviewMerge([FromBody] MergeDonViRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponseFactory.Success(await _service.PreviewMergeAsync(request, cancellationToken)));

    [HttpPost("merge")]
    public async Task<ActionResult<ApiResponse<DonViChangeResultDto>>> Merge([FromBody] MergeDonViRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponseFactory.Success(await _service.MergeAsync(request, cancellationToken)));

    [HttpPost("split/preview")]
    public async Task<ActionResult<ApiResponse<DonViChangePreviewDto>>> PreviewSplit([FromBody] SplitDonViRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponseFactory.Success(await _service.PreviewSplitAsync(request, cancellationToken)));

    [HttpPost("split")]
    public async Task<ActionResult<ApiResponse<DonViChangeResultDto>>> Split([FromBody] SplitDonViRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponseFactory.Success(await _service.SplitAsync(request, cancellationToken)));

    [HttpGet("{unitId:long}/versions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<DonViVersion>>>> Versions(long unitId, CancellationToken cancellationToken)
        => Ok(ApiResponseFactory.Success(await _service.GetVersionsAsync(unitId, cancellationToken)));

    [HttpGet("{unitId:long}/successors")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<DonViSuccessor>>>> Successors(long unitId, CancellationToken cancellationToken)
        => Ok(ApiResponseFactory.Success(await _service.GetSuccessorsAsync(unitId, cancellationToken)));
}
