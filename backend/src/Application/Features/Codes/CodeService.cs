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
}

public sealed class CodeService : ICodeService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IValidator<UpsertCodeRequest> _codeValidator;
    private readonly IValidator<UpsertCodeValueRequest> _codeValueValidator;

    public CodeService(
        IApplicationDbContext dbContext,
        IValidator<UpsertCodeRequest> codeValidator,
        IValidator<UpsertCodeValueRequest> codeValueValidator)
    {
        _dbContext = dbContext;
        _codeValidator = codeValidator;
        _codeValueValidator = codeValueValidator;
    }

    public async Task<IReadOnlyCollection<CodeDto>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Codes.Include(x => x.Values).AsQueryable();
        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var items = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        return items.Select(MapToDto).ToList();
    }

    public async Task<CodeDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Codes.Include(x => x.Values).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<CodeDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Codes
            .Include(x => x.Values.Where(v => v.IsActive).OrderBy(v => v.SortOrder))
            .FirstOrDefaultAsync(x => x.CodeKey == code.ToUpperInvariant(), cancellationToken);
        return entity is null ? null : MapToDto(entity);
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
            IsActive = request.IsActive
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

        var query = _dbContext.CodeValues.Where(x => x.CodeId == codeId);
        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var items = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        return items.Select(MapValueToDto).ToList();
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
            IsActive = request.IsActive
        };

        await _dbContext.CodeValues.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapValueToDto(entity);
    }

    public async Task<CodeValueDto> UpdateValueAsync(long codeId, long valueId, UpsertCodeValueRequest request, CancellationToken cancellationToken = default)
    {
        await _codeValueValidator.ValidateAndThrowAsync(request, cancellationToken);

        var entity = await _dbContext.CodeValues.FirstOrDefaultAsync(x => x.Id == valueId && x.CodeId == codeId, cancellationToken)
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

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapValueToDto(entity);
    }

    public async Task DeleteValueAsync(long codeId, long valueId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.CodeValues.FirstOrDefaultAsync(x => x.Id == valueId && x.CodeId == codeId, cancellationToken)
            ?? throw new AppException("CODE_VALUE_NOT_FOUND", "Không tìm thấy giá trị.", 404);

        _dbContext.CodeValues.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CodeDto MapToDto(Code entity) => new()
    {
        Id = entity.Id,
        Code = entity.CodeKey,
        Name = entity.Name,
        Description = entity.Description,
        SortOrder = entity.SortOrder,
        IsActive = entity.IsActive,
        Values = entity.Values.OrderBy(v => v.SortOrder).ThenBy(v => v.Name).Select(MapValueToDto).ToList()
    };

    private static CodeValueDto MapValueToDto(CodeValue entity) => new()
    {
        Id = entity.Id,
        CodeId = entity.CodeId,
        Value = entity.Value,
        Name = entity.Name,
        Description = entity.Description,
        SortOrder = entity.SortOrder,
        IsActive = entity.IsActive
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
