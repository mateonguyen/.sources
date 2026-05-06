using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.DaoTaoHocVien;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/dao-tao-hoc-vien")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class DaoTaoHocVienController : ControllerBase
{
    private readonly IDaoTaoHocVienService _service;

    public DaoTaoHocVienController(IDaoTaoHocVienService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.DaoTaoHocVien.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<DaoTaoHocVienDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<DaoTaoHocVienDto>>>> GetAll(
        [FromQuery] DaoTaoHocVienQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.DaoTaoHocVien.Read)]
    [ProducesResponseType(typeof(ApiResponse<DaoTaoHocVienDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DaoTaoHocVienDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("DTHV_NOT_FOUND", "Không tìm thấy bản ghi đào tạo học viện."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.DaoTaoHocVien.Create)]
    [ProducesResponseType(typeof(ApiResponse<DaoTaoHocVienDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DaoTaoHocVienDto>>> Create([FromBody] UpsertDaoTaoHocVienRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.DaoTaoHocVien.Update)]
    [ProducesResponseType(typeof(ApiResponse<DaoTaoHocVienDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DaoTaoHocVienDto>>> Update(long id, [FromBody] UpsertDaoTaoHocVienRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("matrix")]
    [HasPermission(Permissions.DaoTaoHocVien.Update)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<DaoTaoHocVienDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<DaoTaoHocVienDto>>>> SaveMatrix([FromBody] SaveDaoTaoHocVienMatrixRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.SaveMatrixAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("finalize")]
    [HasPermission(Permissions.DaoTaoHocVien.Update)]
    [ProducesResponseType(typeof(ApiResponse<FinalizeDaoTaoHocVienResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FinalizeDaoTaoHocVienResult>>> Finalize(
        [FromBody] FinalizeDaoTaoHocVienRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.FinalizeAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.DaoTaoHocVien.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
