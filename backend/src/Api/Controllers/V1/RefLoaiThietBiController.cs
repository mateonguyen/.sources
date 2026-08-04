using Microsoft.AspNetCore.Mvc;
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
        var result = await _service.GetTreeAsync(includeInactive: false, cancellationToken: cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    // Dung boi man quan tri danh muc Loai thiet bi - tra ca loai da vo hieu
    // hoa de admin xem/kich hoat lai.
    [HttpGet("admin-tree")]
    [HasPermission(Permissions.RefLoaiThietBi.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<RefLoaiThietBiDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<RefLoaiThietBiDto>>>> GetAdminTree(CancellationToken cancellationToken)
    {
        var result = await _service.GetTreeAsync(includeInactive: true, cancellationToken: cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
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
    [HasPermission(Permissions.RefLoaiThietBi.Create)]
    [ProducesResponseType(typeof(ApiResponse<RefLoaiThietBiDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RefLoaiThietBiDto>>> Create([FromBody] UpsertRefLoaiThietBiRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.RefLoaiThietBi.Update)]
    [ProducesResponseType(typeof(ApiResponse<RefLoaiThietBiDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RefLoaiThietBiDto>>> Update(long id, [FromBody] UpsertRefLoaiThietBiRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.RefLoaiThietBi.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
