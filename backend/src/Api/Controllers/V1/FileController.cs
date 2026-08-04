using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.Files;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/files")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class FileController : ControllerBase
{
    private readonly IFileService _fileService;

    public FileController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpGet]
    [HasPermission(Permissions.Files.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<FileMetadataDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<FileMetadataDto>>>> GetByEntity(
        [FromQuery] string entityType,
        [FromQuery] long entityId,
        CancellationToken cancellationToken)
    {
        var result = await _fileService.GetByEntityAsync(entityType, entityId, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost("upload")]
    [HasPermission(Permissions.Files.Upload)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<FileMetadataDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FileMetadataDto>>> Upload(
        [FromForm] UploadFileRequest request,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var result = await _fileService.UploadAsync(request, file, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}/download-url")]
    [HasPermission(Permissions.Files.Read)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<string>>> GetDownloadUrl(long id, CancellationToken cancellationToken)
    {
        var url = await _fileService.GetDownloadUrlAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(url));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Files.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _fileService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
