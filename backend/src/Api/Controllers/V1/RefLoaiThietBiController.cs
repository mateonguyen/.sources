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

    [HttpGet("tree")]
    [HasPermission(Permissions.ThietBiCntt.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<RefLoaiThietBiDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<RefLoaiThietBiDto>>>> GetTree(CancellationToken cancellationToken)
    {
        var result = await _service.GetTreeAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }
}