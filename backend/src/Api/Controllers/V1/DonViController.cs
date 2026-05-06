using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.DonVi;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/don-vi")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class DonViController : ControllerBase
{
    private readonly IDonViService _donViService;

    public DonViController(IDonViService donViService)
    {
        _donViService = donViService;
    }

    [HttpGet]
    [HasPermission(Permissions.DonVi.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<DonViDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<DonViDto>>>> GetList(CancellationToken cancellationToken)
    {
        var result = await _donViService.GetFlatListAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("tree")]
    [HasPermission(Permissions.DonVi.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<DonViDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<DonViDto>>>> GetTree(CancellationToken cancellationToken)
    {
        var result = await _donViService.GetTreeAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("parent-candidates")]
    [HasPermission(Permissions.DonVi.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<DonViParentCandidateDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<DonViParentCandidateDto>>>> GetParentCandidates([FromQuery] long? excludeId, CancellationToken cancellationToken)
    {
        var result = await _donViService.GetParentCandidatesAsync(excludeId, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.DonVi.Read)]
    [ProducesResponseType(typeof(ApiResponse<DonViDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DonViDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _donViService.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("DONVI_NOT_FOUND", "Không tìm thấy đơn vị."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.DonVi.Create)]
    [ProducesResponseType(typeof(ApiResponse<DonViDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DonViDto>>> Create([FromBody] UpsertDonViRequest request, CancellationToken cancellationToken)
    {
        var result = await _donViService.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.DonVi.Update)]
    [ProducesResponseType(typeof(ApiResponse<DonViDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DonViDto>>> Update(long id, [FromBody] UpsertDonViRequest request, CancellationToken cancellationToken)
    {
        var result = await _donViService.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.DonVi.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _donViService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
