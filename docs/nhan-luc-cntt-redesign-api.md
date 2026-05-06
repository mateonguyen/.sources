# Đề xuất API .NET cho màn Nhân lực CNTT

## Mục tiêu

- Giảm tải cho frontend bằng cách đưa lọc, phân trang, sắp xếp về backend.
- Giữ tương thích với cấu trúc hiện có của `NhanLucCnttController`.
- Sẵn sàng tích hợp cho màn hình danh sách, tra cứu nhanh và form thêm/sửa.

## Đề xuất controller

```csharp
using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Features.NhanLucCntt;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/nhan-luc-cntt")]
[ApiExplorerSettings(GroupName = "snapshot")]
public sealed class NhanLucCnttController : ControllerBase
{
    private readonly INhanLucCnttService _service;

    public NhanLucCnttController(INhanLucCnttService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(Permissions.NhanLucCntt.Read)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NhanLucCnttListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<NhanLucCnttListItemDto>>>> GetList(
        [FromQuery] NhanLucCnttListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetPagedAsync(query, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpGet("{id:long}")]
    [HasPermission(Permissions.NhanLucCntt.Read)]
    [ProducesResponseType(typeof(ApiResponse<NhanLucCnttDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NhanLucCnttDetailDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _service.GetDetailAsync(id, cancellationToken);
        return result is null
            ? NotFound(ApiResponseFactory.Error("NHANLUC_NOT_FOUND", "Không tìm thấy nhân lực CNTT."))
            : Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.NhanLucCntt.Create)]
    [ProducesResponseType(typeof(ApiResponse<NhanLucCnttDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NhanLucCnttDetailDto>>> Create(
        [FromBody] UpsertNhanLucCnttRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(null, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.NhanLucCntt.Update)]
    [ProducesResponseType(typeof(ApiResponse<NhanLucCnttDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NhanLucCnttDetailDto>>> Update(
        long id,
        [FromBody] UpsertNhanLucCnttRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.NhanLucCntt.Delete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
```

## DTO và query đề xuất

```csharp
namespace ThucLuc.Application.Features.NhanLucCntt;

public sealed class NhanLucCnttListQuery : PagingRequest
{
    public string? TuKhoa { get; set; }
    public int? NamBaoCao { get; set; }
    public long? DonViCongTacId { get; set; }
    public string? GioiTinh { get; set; }
    public string? CapBac { get; set; }
    public string? LoaiNhanLuc { get; set; }
    public string? TrinhDoCntt { get; set; }
}

public sealed class NhanLucCnttListItemDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public long? DonViCongTacId { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public DateOnly? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string? CapBac { get; set; }
    public string? ChucVu { get; set; }
    public string? DienThoai { get; set; }
    public string? LoaiNhanLuc { get; set; }
    public string? TrinhDoCntt { get; set; }
    public string? DonViCongTacTen { get; set; }
    public int? NamBaoCao { get; set; }
}

public sealed class NhanLucCnttDetailDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public long? DonViCongTacId { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public DateOnly? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string? CapBac { get; set; }
    public string? ChucVu { get; set; }
    public string? DienThoai { get; set; }
    public string? LoaiNhanLuc { get; set; }
    public string? TrinhDoCntt { get; set; }
    public string? TrinhDoLlct { get; set; }
    public string? GhiChu { get; set; }
    public int? NamBaoCao { get; set; }
}

public sealed class UpsertNhanLucCnttRequest
{
    public long DonViId { get; set; }
    public long? DonViCongTacId { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public DateOnly? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string? CapBac { get; set; }
    public string? ChucVu { get; set; }
    public string? DienThoai { get; set; }
    public string? LoaiNhanLuc { get; set; }
    public string? TrinhDoCntt { get; set; }
    public string? TrinhDoLlct { get; set; }
    public string? GhiChu { get; set; }
    public int? NamBaoCao { get; set; }
}
```

## Lưu ý triển khai

- `GetPagedAsync` nên lọc theo phạm vi đơn vị của người dùng như service hiện tại.
- `DonViCongTacTen` nên join trực tiếp từ bảng `DonVi` để frontend không phải tự map tên.
- `NamBaoCao` nên bổ sung ở entity hoặc lấy từ ngữ cảnh kỳ báo cáo nếu hệ thống đã có bảng snapshot theo kỳ.
- Response nên trả về `PagedResult<T>` để frontend bỏ lọc và phân trang client-side khi backend sẵn sàng.
