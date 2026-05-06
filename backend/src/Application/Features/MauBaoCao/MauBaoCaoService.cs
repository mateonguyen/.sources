using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using MauBaoCaoEntity = ThucLuc.Domain.Entities.Reporting.MauBaoCao;

namespace ThucLuc.Application.Features.MauBaoCao;

public interface IMauBaoCaoService
{
    Task<IReadOnlyCollection<MauBaoCaoDto>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MauBaoCaoModuleCatalogGroupDto>> GetModuleCatalogAsync(CancellationToken cancellationToken = default);

    Task<MauBaoCaoDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<MauBaoCaoDto> CreateAsync(UpsertMauBaoCaoRequest request, CancellationToken cancellationToken = default);

    Task<MauBaoCaoDto> UpdateAsync(long id, UpsertMauBaoCaoRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class MauBaoCaoService : IMauBaoCaoService
{
    private static readonly IReadOnlyCollection<MauBaoCaoModuleCatalogGroupDto> ModuleCatalog =
    [
        new MauBaoCaoModuleCatalogGroupDto
        {
            GroupKey = "hr_it",
            GroupLabel = "Nhân lực CNTT",
            Items =
            [
                CreateModuleItem("NHAN_LUC_CNTT", "Nhân lực CNTT", "hr_it", "Nhân lực CNTT"),
                CreateModuleItem("NANG_LUC_SO", "Năng lực số", "hr_it", "Nhân lực CNTT"),
                CreateModuleItem("DAO_TAO_BOI_DUONG", "Đào tạo bồi dưỡng", "hr_it", "Nhân lực CNTT"),
                CreateModuleItem("DAO_TAO_HOC_VIEN", "Đào tạo học viện", "hr_it", "Nhân lực CNTT")
            ]
        },
        new MauBaoCaoModuleCatalogGroupDto
        {
            GroupKey = "infrastructure",
            GroupLabel = "Hạ tầng & hệ thống",
            Items =
            [
                CreateModuleItem("HE_THONG_THONG_TIN", "Hệ thống thông tin", "infrastructure", "Hạ tầng & hệ thống"),
                CreateModuleItem("HTTT_TIEU_CHUAN", "HTTT tiêu chuẩn", "infrastructure", "Hạ tầng & hệ thống"),
                CreateModuleItem("DU_AN_CNTT", "Dự án CNTT", "infrastructure", "Hạ tầng & hệ thống"),
                CreateModuleItem("THIET_BI_CNTT", "Thiết bị CNTT", "infrastructure", "Hạ tầng & hệ thống"),
                CreateModuleItem("HA_TANG_MANG", "Hạ tầng mạng", "infrastructure", "Hạ tầng & hệ thống"),
                CreateModuleItem("GIAM_SAT_NOC", "Giám sát NOC", "infrastructure", "Hạ tầng & hệ thống"),
                CreateModuleItem("CAMERA_QUAN_LY", "Camera quản lý", "infrastructure", "Hạ tầng & hệ thống"),
                CreateModuleItem("CAMERA_THUC_TRANG", "Camera thực trạng", "infrastructure", "Hạ tầng & hệ thống")
            ]
        },
        new MauBaoCaoModuleCatalogGroupDto
        {
            GroupKey = "security",
            GroupLabel = "An toàn thông tin",
            Items =
            [
                CreateModuleItem("GIAM_SAT_SOC", "Giám sát SOC", "security", "An toàn thông tin"),
                CreateModuleItem("ATTT_HTTT_VAN_HANH", "ATTT hệ thống vận hành", "security", "An toàn thông tin"),
                CreateModuleItem("ATTT_HTTT_DAU_TU", "ATTT hệ thống đầu tư", "security", "An toàn thông tin"),
                CreateModuleItem("ATTT_GIAI_PHAP", "Giải pháp ATTT", "security", "An toàn thông tin")
            ]
        },
        new MauBaoCaoModuleCatalogGroupDto
        {
            GroupKey = "documents",
            GroupLabel = "Văn bản & quản lý",
            Items =
            [
                CreateModuleItem("VAN_BAN_QPPL", "Văn bản QPPL", "documents", "Văn bản & quản lý")
            ]
        }
    ];

    private static readonly HashSet<string> KnownModuleCodes = ModuleCatalog
        .SelectMany(group => group.Items)
        .Select(item => item.Code)
        .ToHashSet(StringComparer.Ordinal);

    private readonly IApplicationDbContext _dbContext;
    private readonly IValidator<UpsertMauBaoCaoRequest> _validator;

    public MauBaoCaoService(IApplicationDbContext dbContext, IValidator<UpsertMauBaoCaoRequest> validator)
    {
        _dbContext = dbContext;
        _validator = validator;
    }

    public async Task<IReadOnlyCollection<MauBaoCaoDto>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MauBaoCaos.AsQueryable();
        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var items = await query
            .OrderBy(x => x.MaMau)
            .ToListAsync(cancellationToken);

        return items.Select(MapToDto).ToList();
    }

    public Task<IReadOnlyCollection<MauBaoCaoModuleCatalogGroupDto>> GetModuleCatalogAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ModuleCatalog);

    public async Task<MauBaoCaoDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.MauBaoCaos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<MauBaoCaoDto> CreateAsync(UpsertMauBaoCaoRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedMaMau = NormalizeUpper(request.MaMau);
        var normalizedModuleList = NormalizeModuleList(request.DanhSachModule);

        var exists = await _dbContext.MauBaoCaos.CountAsync(
            x => x.MaMau == normalizedMaMau,
            cancellationToken);
        if (exists > 0)
            throw new BusinessRuleException("MAU_BAO_CAO_DUPLICATE", $"Mã mẫu '{normalizedMaMau}' đã tồn tại.");

        var entity = new MauBaoCaoEntity
        {
            MaMau = normalizedMaMau,
            TenMau = request.TenMau.Trim(),
            MoTa = request.MoTa?.Trim(),
            TanSuat = request.TanSuat,
            DanhSachModule = JsonSerializer.Serialize(normalizedModuleList),
            IsActive = request.IsActive
        };

        await _dbContext.MauBaoCaos.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<MauBaoCaoDto> UpdateAsync(long id, UpsertMauBaoCaoRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var entity = await _dbContext.MauBaoCaos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("MAU_BAO_CAO_NOT_FOUND", "Không tìm thấy mẫu báo cáo.", 404);

        var normalizedMaMau = NormalizeUpper(request.MaMau);
        var normalizedModuleList = NormalizeModuleList(request.DanhSachModule);

        var duplicate = await _dbContext.MauBaoCaos.CountAsync(
            x => x.MaMau == normalizedMaMau && x.Id != id,
            cancellationToken);
        if (duplicate > 0)
            throw new BusinessRuleException("MAU_BAO_CAO_DUPLICATE", $"Mã mẫu '{normalizedMaMau}' đã tồn tại.");

        entity.MaMau = normalizedMaMau;
        entity.TenMau = request.TenMau.Trim();
        entity.MoTa = request.MoTa?.Trim();
        entity.TanSuat = request.TanSuat;
        entity.DanhSachModule = JsonSerializer.Serialize(normalizedModuleList);
        entity.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.MauBaoCaos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("MAU_BAO_CAO_NOT_FOUND", "Không tìm thấy mẫu báo cáo.", 404);

        var hasKyBaoCao = await _dbContext.KyBaoCaos.CountAsync(
            x => x.MauBaoCaoId == id,
            cancellationToken);
        if (hasKyBaoCao > 0)
            throw new BusinessRuleException("MAU_BAO_CAO_IN_USE", "Không thể xóa mẫu báo cáo đang được sử dụng bởi kỳ báo cáo.");

        _dbContext.MauBaoCaos.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static MauBaoCaoDto MapToDto(MauBaoCaoEntity entity)
        => new()
        {
            Id = entity.Id,
            MaMau = entity.MaMau,
            TenMau = entity.TenMau,
            MoTa = entity.MoTa,
            TanSuat = entity.TanSuat,
            DanhSachModule = ParseModuleList(entity.DanhSachModule),
            IsActive = entity.IsActive
        };

    private static string NormalizeUpper(string value) => value.Trim().ToUpperInvariant();

    private static IReadOnlyCollection<string> NormalizeModuleList(IEnumerable<string> modules)
    {
        var normalized = modules
            .Select(x => x.Trim().ToUpperInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
            throw new BusinessRuleException("MAU_EMPTY_MODULE_LIST", "Danh sách module không được rỗng.");

        var unknownModules = normalized.Where(x => !KnownModuleCodes.Contains(x)).ToList();
        if (unknownModules.Count > 0)
        {
            throw new BusinessRuleException(
                "MAU_UNKNOWN_MODULE",
                $"Module không hợp lệ: {string.Join(", ", unknownModules)}.");
        }

        return normalized;
    }

    private static MauBaoCaoModuleCatalogItemDto CreateModuleItem(string code, string label, string groupKey, string groupLabel)
        => new()
        {
            Code = code,
            Label = label,
            GroupKey = groupKey,
            GroupLabel = groupLabel,
            IsActive = true
        };

    private static IReadOnlyCollection<string> ParseModuleList(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public sealed class UpsertMauBaoCaoRequestValidator : AbstractValidator<UpsertMauBaoCaoRequest>
{
    public UpsertMauBaoCaoRequestValidator()
    {
        RuleFor(x => x.MaMau).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9_]+$");
        RuleFor(x => x.TenMau).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MoTa).MaximumLength(1000).When(x => x.MoTa is not null);
        RuleFor(x => x.DanhSachModule)
            .NotNull()
            .Must(x => x.Count > 0)
            .WithMessage("Danh sách module không được rỗng.");
        RuleForEach(x => x.DanhSachModule)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[A-Za-z0-9_]+$");
    }
}