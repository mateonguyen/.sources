using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.Codes;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/codes")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class CodesController : ControllerBase
{
    private readonly ICodeService _codeService;

    public CodesController(ICodeService codeService)
    {
        _codeService = codeService;
    }

    [HttpGet]
    [HasPermission(Permissions.Codes.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<CodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CodeDto>>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _codeService.GetAllAsync(includeInactive, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.Codes.Read)]
    [ProducesResponseType(typeof(ApiResponse<CodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CodeDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _codeService.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("CODE_NOT_FOUND", "Không tìm thấy code."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("by-code/{code}")]
    [HttpGet("{code}")]
    [HasPermission(Permissions.Codes.Read)]
    [ProducesResponseType(typeof(ApiResponse<CodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CodeDto>>> GetByCode(string code, CancellationToken cancellationToken)
    {
        var result = await _codeService.GetByCodeAsync(code, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("CODE_NOT_FOUND", "Không tìm thấy code."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Codes.Create)]
    [ProducesResponseType(typeof(ApiResponse<CodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CodeDto>>> Create([FromBody] UpsertCodeRequest request, CancellationToken cancellationToken)
    {
        var result = await _codeService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Codes.Update)]
    [ProducesResponseType(typeof(ApiResponse<CodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CodeDto>>> Update(long id, [FromBody] UpsertCodeRequest request, CancellationToken cancellationToken)
    {
        var result = await _codeService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Codes.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _codeService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }

    // ── Code Values ──────────────────────────────────────────────────────────

    [HttpGet("{codeId:long}/values")]
    [HasPermission(Permissions.Codes.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<CodeValueDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CodeValueDto>>>> GetValues(
        long codeId,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _codeService.GetValuesAsync(codeId, includeInactive, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("{codeId:long}/values")]
    [HasPermission(Permissions.Codes.Create)]
    [ProducesResponseType(typeof(ApiResponse<CodeValueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CodeValueDto>>> CreateValue(long codeId, [FromBody] UpsertCodeValueRequest request, CancellationToken cancellationToken)
    {
        var result = await _codeService.CreateValueAsync(codeId, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{codeId:long}/values/{valueId:long}")]
    [HasPermission(Permissions.Codes.Update)]
    [ProducesResponseType(typeof(ApiResponse<CodeValueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CodeValueDto>>> UpdateValue(long codeId, long valueId, [FromBody] UpsertCodeValueRequest request, CancellationToken cancellationToken)
    {
        var result = await _codeService.UpdateValueAsync(codeId, valueId, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{codeId:long}/values/{valueId:long}")]
    [HasPermission(Permissions.Codes.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteValue(long codeId, long valueId, CancellationToken cancellationToken)
    {
        await _codeService.DeleteValueAsync(codeId, valueId, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
