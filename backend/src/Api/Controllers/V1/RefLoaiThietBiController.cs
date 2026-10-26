using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.RefLoaiThietBi;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/ref-loai-thiet-bi")]
[Route("api/v1/dm-loai-thiet-bi")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class RefLoaiThietBiController : ControllerBase
{
    private readonly IRefLoaiThietBiService _service;

    public RefLoaiThietBiController(IRefLoaiThietBiService service)
    {
        _service = service;
    }

    // Dung boi man nhap lieu Thiet bi CNTT (dropdown chon loai) - chi tra
    // loai dang active, giu nguyen quyen cu de khong pha vo consumer hien tai.
    [HttpGet("tree")]
    [HasPermission(Permissions.ThietBiCntt.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<RefLoaiThietBiDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<RefLoaiThietBiDto>>>> GetTree(CancellationToken cancellationToken)
    {
        var result = await _service.GetEffectiveTreeAsync(includeHidden: false, cancellationToken: cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    // Dung boi man quan tri danh muc Loai thiet bi - tra ca loai da vo hieu
    // hoa de admin xem/kich hoat lai.
    [HttpGet("admin-tree")]
    [Authorize(Roles = "SYSTEM_ADMIN,QUAN_LY")]
    [HasPermission(Permissions.RefLoaiThietBi.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<RefLoaiThietBiDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<RefLoaiThietBiDto>>>> GetAdminTree(CancellationToken cancellationToken)
    {
        var result = await _service.GetTreeAsync(includeInactive: true, cancellationToken: cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("unit-proposals")]
    [Authorize(Roles = "SYSTEM_ADMIN,QUAN_LY")]
    [HasPermission(Permissions.RefLoaiThietBi.Update)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<UnitRefLoaiThietBiProposalDto>>>> GetUnitProposals(
        CancellationToken cancellationToken)
    {
        var result = await _service.GetUnitProposalsAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("unit-proposals/{id:long}/standardize")]
    [Authorize(Roles = "SYSTEM_ADMIN,QUAN_LY")]
    [HasPermission(Permissions.RefLoaiThietBi.Update)]
    public async Task<ActionResult<ApiResponse<object>>> StandardizeUnitProposal(
        long id,
        [FromBody] StandardizeUnitRefLoaiThietBiRequest request,
        CancellationToken cancellationToken)
    {
        await _service.StandardizeUnitProposalAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.RefLoaiThietBi.Read)]
    [ProducesResponseType(typeof(ApiResponse<RefLoaiThietBiDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RefLoaiThietBiDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("REF_LOAI_THIET_BI_NOT_FOUND", "Không tìm thấy loại thiết bị."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [Authorize(Roles = "SYSTEM_ADMIN,QUAN_LY")]
    [HasPermission(Permissions.RefLoaiThietBi.Create)]
    [ProducesResponseType(typeof(ApiResponse<RefLoaiThietBiDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RefLoaiThietBiDto>>> Create([FromBody] UpsertRefLoaiThietBiRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "SYSTEM_ADMIN,QUAN_LY")]
    [HasPermission(Permissions.RefLoaiThietBi.Update)]
    [ProducesResponseType(typeof(ApiResponse<RefLoaiThietBiDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RefLoaiThietBiDto>>> Update(long id, [FromBody] UpsertRefLoaiThietBiRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "SYSTEM_ADMIN,QUAN_LY")]
    [HasPermission(Permissions.RefLoaiThietBi.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }

    [HttpGet("unit-tree")]
    [HasPermission(Permissions.RefLoaiThietBi.Read)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<RefLoaiThietBiDto>>>> GetUnitTree(
        [FromQuery] bool includeHidden = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetEffectiveTreeAsync(includeHidden, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("unit-items")]
    [HasPermission(Permissions.RefLoaiThietBi.Create)]
    public async Task<ActionResult<ApiResponse<RefLoaiThietBiDto>>> CreateUnitItem(
        [FromBody] UpsertUnitRefLoaiThietBiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateUnitAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("unit-items/{id:long}")]
    [HasPermission(Permissions.RefLoaiThietBi.Update)]
    public async Task<ActionResult<ApiResponse<RefLoaiThietBiDto>>> UpdateUnitItem(
        long id,
        [FromBody] UpsertUnitRefLoaiThietBiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateUnitAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("unit-items/{id:long}")]
    [HasPermission(Permissions.RefLoaiThietBi.Delete)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteUnitItem(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteUnitAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }

    [HttpPut("unit-items/{id:long}/visibility")]
    [HasPermission(Permissions.RefLoaiThietBi.Update)]
    public async Task<ActionResult<ApiResponse<object>>> SetUnitItemVisibility(
        long id,
        [FromBody] SetRefLoaiThietBiVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        await _service.SetUnitVisibilityAsync(id, request.IsVisible, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
