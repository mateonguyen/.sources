using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Application.Features.Codes;

public interface ICodeService
{
    Task<IReadOnlyCollection<CodeDto>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<CodeDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<CodeDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<CodeDto> CreateAsync(UpsertCodeRequest request, CancellationToken cancellationToken = default);

    Task<CodeDto> UpdateAsync(long id, UpsertCodeRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CodeValueDto>> GetValuesAsync(long codeId, bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<CodeValueDto> CreateValueAsync(long codeId, UpsertCodeValueRequest request, CancellationToken cancellationToken = default);

    Task<CodeValueDto> UpdateValueAsync(long codeId, long valueId, UpsertCodeValueRequest request, CancellationToken cancellationToken = default);

    Task DeleteValueAsync(long codeId, long valueId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CodeValueDto>> GetUnitValuesAsync(long codeId, bool includeHidden = true, CancellationToken cancellationToken = default);

    Task<CodeValueDto> CreateUnitValueAsync(long codeId, UpsertUnitCodeValueRequest request, CancellationToken cancellationToken = default);

    Task<CodeValueDto> UpdateUnitValueAsync(long codeId, long valueId, UpsertUnitCodeValueRequest request, CancellationToken cancellationToken = default);

    Task DeleteUnitValueAsync(long codeId, long valueId, CancellationToken cancellationToken = default);

    Task SetUnitValueVisibilityAsync(long codeId, long valueId, bool isVisible, CancellationToken cancellationToken = default);
}

public sealed class CodeService : ICodeService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IValidator<UpsertCodeRequest> _codeValidator;
    private readonly IValidator<UpsertCodeValueRequest> _codeValueValidator;
    private readonly ICurrentUserService _currentUserService;

    public CodeService(
        IApplicationDbContext dbContext,
        IValidator<UpsertCodeRequest> codeValidator,
        IValidator<UpsertCodeValueRequest> codeValueValidator,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _codeValidator = codeValidator;
        _codeValueValidator = codeValueValidator;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyCollection<CodeDto>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Codes.Include(x => x.Values.Where(v => v.DonViId == null)).AsQueryable();
        if (!includeInactive)
        {
            var activeFlag = true; // Oracle: không dùng bool trần trong predicate (ORA cast/literal)
            query = query.Where(x => x.IsActive == activeFlag);
        }

        var items = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        return items.Select(x => MapToDto(x)).ToList();
    }

    public async Task<CodeDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Codes
            .Include(x => x.Values.Where(v => v.DonViId == null))
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<CodeDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Codes
            .FirstOrDefaultAsync(x => x.CodeKey == code.ToUpperInvariant(), cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var values = entity.AllowUnitCustomization
            ? await GetUnitValuesAsync(entity.Id, includeHidden: false, cancellationToken)
            : await GetValuesAsync(entity.Id, includeInactive: false, cancellationToken);
        return MapToDto(entity, values);
    }

    public async Task<CodeDto> CreateAsync(UpsertCodeRequest request, CancellationToken cancellationToken = default)
    {
        await _codeValidator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _dbContext.Codes.CountAsync(x => x.CodeKey == normalizedCode, cancellationToken) > 0;
        if (exists)
            throw new BusinessRuleException("CODE_DUPLICATE", $"Code '{normalizedCode}' đã tồn tại.");

        var entity = new Code
        {
            CodeKey = normalizedCode,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            AllowUnitCustomization = request.AllowUnitCustomization
        };

        await _dbContext.Codes.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task<CodeDto> UpdateAsync(long id, UpsertCodeRequest request, CancellationToken cancellationToken = default)
    {
        await _codeValidator.ValidateAndThrowAsync(request, cancellationToken);

        var entity = await _dbContext.Codes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("CODE_NOT_FOUND", "Không tìm thấy code.", 404);

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _dbContext.Codes.CountAsync(x => x.CodeKey == normalizedCode && x.Id != id, cancellationToken) > 0;
        if (exists)
            throw new BusinessRuleException("CODE_DUPLICATE", $"Code '{normalizedCode}' đã tồn tại.");

        entity.CodeKey = normalizedCode;
        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.AllowUnitCustomization = request.AllowUnitCustomization;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Codes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("CODE_NOT_FOUND", "Không tìm thấy code.", 404);

        var hasValues = await _dbContext.CodeValues.CountAsync(x => x.CodeId == id, cancellationToken) > 0;
        if (hasValues)
            throw new BusinessRuleException("CODE_HAS_VALUES", "Không thể xóa code còn chứa giá trị.");

        _dbContext.Codes.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CodeValueDto>> GetValuesAsync(long codeId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var codeExists = await _dbContext.Codes.CountAsync(x => x.Id == codeId, cancellationToken) > 0;
        if (!codeExists)
            throw new AppException("CODE_NOT_FOUND", "Không tìm thấy code.", 404);

        var query = _dbContext.CodeValues.Where(x => x.CodeId == codeId && x.DonViId == null);
        if (!includeInactive)
        {
            var activeFlag = true;
            query = query.Where(x => x.IsActive == activeFlag);
        }

        var items = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        return items.Select(x => MapValueToDto(x, true)).ToList();
    }

    public async Task<CodeValueDto> CreateValueAsync(long codeId, UpsertCodeValueRequest request, CancellationToken cancellationToken = default)
    {
        await _codeValueValidator.ValidateAndThrowAsync(request, cancellationToken);

        var codeExists = await _dbContext.Codes.CountAsync(x => x.Id == codeId, cancellationToken) > 0;
        if (!codeExists)
            throw new AppException("CODE_NOT_FOUND", "Không tìm thấy code.", 404);

        var normalizedValue = request.Value.Trim().ToUpperInvariant();
        var exists = await _dbContext.CodeValues.CountAsync(x => x.CodeId == codeId && x.Value == normalizedValue, cancellationToken) > 0;
        if (exists)
            throw new BusinessRuleException("CODE_VALUE_DUPLICATE", $"Giá trị '{normalizedValue}' đã tồn tại trong code này.");

        var entity = new CodeValue
        {
            CodeId = codeId,
            Value = normalizedValue,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            IsRequired = request.IsRequired
        };

        await _dbContext.CodeValues.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapValueToDto(entity, true);
    }

    public async Task<CodeValueDto> UpdateValueAsync(long codeId, long valueId, UpsertCodeValueRequest request, CancellationToken cancellationToken = default)
    {
        await _codeValueValidator.ValidateAndThrowAsync(request, cancellationToken);

        var entity = await _dbContext.CodeValues.FirstOrDefaultAsync(x => x.Id == valueId && x.CodeId == codeId && x.DonViId == null, cancellationToken)
            ?? throw new AppException("CODE_VALUE_NOT_FOUND", "Không tìm thấy giá trị.", 404);

        var normalizedValue = request.Value.Trim().ToUpperInvariant();
        if (!string.Equals(entity.Value, normalizedValue, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _dbContext.CodeValues.CountAsync(x => x.CodeId == codeId && x.Value == normalizedValue && x.Id != valueId, cancellationToken) > 0;
            if (exists)
                throw new BusinessRuleException("CODE_VALUE_DUPLICATE", $"Giá trị '{normalizedValue}' đã tồn tại trong code này.");
        }

        entity.Value = normalizedValue;
        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.IsRequired = request.IsRequired;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapValueToDto(entity, true);
    }

    public async Task DeleteValueAsync(long codeId, long valueId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.CodeValues.FirstOrDefaultAsync(x => x.Id == valueId && x.CodeId == codeId && x.DonViId == null, cancellationToken)
            ?? throw new AppException("CODE_VALUE_NOT_FOUND", "Không tìm thấy giá trị.", 404);

        _dbContext.CodeValues.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CodeValueDto>> GetUnitValuesAsync(
        long codeId,
        bool includeHidden = true,
        CancellationToken cancellationToken = default)
    {
        var (code, donViId) = await GetCustomizableCodeContextAsync(codeId, cancellationToken);
        var activeFlag = true;
        var standard = await _dbContext.CodeValues
            .Where(x => x.CodeId == code.Id && x.DonViId == null && x.IsActive == activeFlag)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var local = await _dbContext.CodeValues
            .Where(x => x.CodeId == code.Id && x.DonViId == donViId && x.IsActive == activeFlag)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var catalogKey = $"CODE:{code.CodeKey}";
        var preferences = await _dbContext.UnitCatalogItemSettings
            .Where(x => x.DonViId == donViId && x.CatalogKey == catalogKey)
            .ToDictionaryAsync(x => x.ItemId, x => x.IsVisible, cancellationToken);

        var result = standard
            .Select(x => MapValueToDto(x, x.IsRequired || !preferences.TryGetValue(x.Id, out var visible) || visible))
            .Concat(local.Select(x => MapValueToDto(x, true)))
            .Where(x => includeHidden || x.IsVisible)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();
        return result;
    }

    public async Task<CodeValueDto> CreateUnitValueAsync(
        long codeId,
        UpsertUnitCodeValueRequest request,
        CancellationToken cancellationToken = default)
    {
        var (code, donViId) = await GetCustomizableCodeContextAsync(codeId, cancellationToken);
        await ValidateUnitValueRequestAsync(code, donViId, request, null, cancellationToken);
        // Một số module nghiệp vụ lưu mã danh mục trong cột NVARCHAR2(20).
        // Mã cục bộ phải ngắn, ổn định và không chứa tên đơn vị.
        var localValueCode = $"DV_{Guid.NewGuid():N}"[..19];
        var entity = new CodeValue
        {
            CodeId = code.Id,
            DonViId = donViId,
            ReportingValueId = null,
            Value = localValueCode,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            IsRequired = false
        };
        await _dbContext.CodeValues.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapValueToDto(entity, true);
    }

    public async Task<CodeValueDto> UpdateUnitValueAsync(
        long codeId,
        long valueId,
        UpsertUnitCodeValueRequest request,
        CancellationToken cancellationToken = default)
    {
        var (code, donViId) = await GetCustomizableCodeContextAsync(codeId, cancellationToken);
        var entity = await _dbContext.CodeValues
            .FirstOrDefaultAsync(x => x.Id == valueId && x.CodeId == code.Id && x.DonViId == donViId, cancellationToken)
            ?? throw new AppException("UNIT_CODE_VALUE_NOT_FOUND", "Không tìm thấy giá trị riêng của đơn vị.", 404);
        await ValidateUnitValueRequestAsync(code, donViId, request, valueId, cancellationToken);
        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.ReportingValueId = null;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapValueToDto(entity, true);
    }

    public async Task DeleteUnitValueAsync(long codeId, long valueId, CancellationToken cancellationToken = default)
    {
        var (code, donViId) = await GetCustomizableCodeContextAsync(codeId, cancellationToken);
        var entity = await _dbContext.CodeValues
            .FirstOrDefaultAsync(x => x.Id == valueId && x.CodeId == code.Id && x.DonViId == donViId, cancellationToken)
            ?? throw new AppException("UNIT_CODE_VALUE_NOT_FOUND", "Không tìm thấy giá trị riêng của đơn vị.", 404);
        if (await IsStandardValueInUseAsync(code.CodeKey, entity.Value, donViId, cancellationToken))
        {
            throw new AppException(
                "UNIT_CODE_VALUE_IN_USE",
                "Giá trị riêng đang được dữ liệu nghiệp vụ sử dụng nên chưa thể ngừng áp dụng.",
                400);
        }
        entity.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetUnitValueVisibilityAsync(
        long codeId,
        long valueId,
        bool isVisible,
        CancellationToken cancellationToken = default)
    {
        var (code, donViId) = await GetCustomizableCodeContextAsync(codeId, cancellationToken);
        var standard = await _dbContext.CodeValues
            .FirstOrDefaultAsync(x => x.Id == valueId && x.CodeId == code.Id && x.DonViId == null, cancellationToken)
            ?? throw new AppException("CODE_VALUE_NOT_FOUND", "Không tìm thấy giá trị danh mục chuẩn.", 404);
        if (standard.IsRequired && !isVisible)
        {
            throw new AppException("CODE_VALUE_REQUIRED", "Giá trị bắt buộc không thể ẩn khỏi danh mục đơn vị.", 400);
        }
        if (!isVisible && await IsStandardValueInUseAsync(code.CodeKey, standard.Value, donViId, cancellationToken))
        {
            throw new AppException(
                "CODE_VALUE_IN_USE",
                "Đơn vị đang có dữ liệu sử dụng giá trị này. Hãy chuyển dữ liệu sang mục khác trước khi ẩn.",
                400);
        }

        var catalogKey = $"CODE:{code.CodeKey}";
        var preference = await _dbContext.UnitCatalogItemSettings.FirstOrDefaultAsync(
            x => x.DonViId == donViId && x.CatalogKey == catalogKey && x.ItemId == valueId,
            cancellationToken);
        if (preference is null)
        {
            preference = new UnitCatalogItemSetting
            {
                DonViId = donViId,
                CatalogKey = catalogKey,
                ItemId = valueId,
                IsVisible = isVisible
            };
            await _dbContext.UnitCatalogItemSettings.AddAsync(preference, cancellationToken);
        }
        else
        {
            preference.IsVisible = isVisible;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<(Code Code, long DonViId)> GetCustomizableCodeContextAsync(long codeId, CancellationToken cancellationToken)
    {
        var code = await _dbContext.Codes.FirstOrDefaultAsync(x => x.Id == codeId, cancellationToken)
            ?? throw new AppException("CODE_NOT_FOUND", "Không tìm thấy danh mục.", 404);
        if (!code.AllowUnitCustomization)
        {
            throw new AppException("CODE_NOT_CUSTOMIZABLE", "Danh mục này không cho phép tùy chỉnh theo đơn vị.", 400);
        }
        var donViId = _currentUserService.GetCurrentUser().DonViId;
        if (donViId <= 0)
        {
            throw new AppException("UNIT_CONTEXT_REQUIRED", "Không xác định được đơn vị đang sử dụng.", 400);
        }
        return (code, donViId);
    }

    private async Task ValidateUnitValueRequestAsync(
        Code code,
        long donViId,
        UpsertUnitCodeValueRequest request,
        long? selfId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
        {
            throw new AppException("UNIT_CODE_VALUE_NAME_INVALID", "Tên giá trị phải có từ 1 đến 200 ký tự.", 400);
        }
        var normalizedName = request.Name.Trim().ToUpperInvariant();
        var duplicated = await _dbContext.CodeValues.AnyAsync(
            x => x.CodeId == code.Id
                && (x.DonViId == null || x.DonViId == donViId)
                && x.Id != (selfId ?? 0)
                && x.Name.ToUpper() == normalizedName,
            cancellationToken);
        if (duplicated)
        {
            throw new AppException("UNIT_CODE_VALUE_DUPLICATE", "Danh mục đang áp dụng tại đơn vị đã có giá trị cùng tên.", 409);
        }
    }

    private Task<bool> IsStandardValueInUseAsync(
        string codeKey,
        string value,
        long donViId,
        CancellationToken cancellationToken)
        => codeKey.ToUpperInvariant() switch
        {
            "GIAI_PHAP_ATTT" => _dbContext.GiaiPhapAttts.AnyAsync(
                x => x.DonViId == donViId && x.TenGiaiPhap == value,
                cancellationToken),
            "NOI_DUNG_DAO_TAO_HV" => _dbContext.DaoTaoHocViens.AnyAsync(
                x => x.DonViId == donViId && x.NoiDungDaoTao == value,
                cancellationToken),
            "NHOM_NANG_LUC_SO" => _dbContext.NangLucSos.AnyAsync(
                x => x.DonViId == donViId && x.NhomViTri == value,
                cancellationToken),
            "LOAI_VAN_BAN" => _dbContext.VanBanQppls.AnyAsync(
                x => x.DonViId == donViId && x.LoaiVanBan == value,
                cancellationToken),
            "CO_QUAN_BAN_HANH" => _dbContext.VanBanQppls.AnyAsync(
                x => x.DonViId == donViId && x.CoQuanBanHanh == value,
                cancellationToken),
            "LINH_VUC_VAN_BAN" => _dbContext.VanBanQppls.AnyAsync(
                x => x.DonViId == donViId && x.LinhVuc == value,
                cancellationToken),
            _ => Task.FromResult(false)
        };

    private static CodeDto MapToDto(Code entity, IReadOnlyCollection<CodeValueDto>? values = null) => new()
    {
        Id = entity.Id,
        Code = entity.CodeKey,
        Name = entity.Name,
        Description = entity.Description,
        SortOrder = entity.SortOrder,
        IsActive = entity.IsActive,
        AllowUnitCustomization = entity.AllowUnitCustomization,
        Values = values ?? entity.Values.OrderBy(v => v.SortOrder).ThenBy(v => v.Name).Select(x => MapValueToDto(x, true)).ToList()
    };

    private static CodeValueDto MapValueToDto(CodeValue entity, bool isVisible = true) => new()
    {
        Id = entity.Id,
        CodeId = entity.CodeId,
        Value = entity.Value,
        Name = entity.Name,
        Description = entity.Description,
        SortOrder = entity.SortOrder,
        IsActive = entity.IsActive,
        DonViId = entity.DonViId,
        ReportingValueId = entity.ReportingValueId,
        IsRequired = entity.IsRequired,
        IsLocal = entity.DonViId.HasValue,
        IsVisible = isVisible
    };
}

public sealed class UpsertCodeRequestValidator : AbstractValidator<UpsertCodeRequest>
{
    public UpsertCodeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}

public sealed class UpsertCodeValueRequestValidator : AbstractValidator<UpsertCodeValueRequest>
{
    public UpsertCodeValueRequestValidator()
    {
        RuleFor(x => x.Value).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}
