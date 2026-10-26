namespace ThucLuc.Application.Features.Codes;

public sealed class CodeDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public bool AllowUnitCustomization { get; set; }
    public IReadOnlyCollection<CodeValueDto> Values { get; set; } = [];
}

public sealed class CodeValueDto
{
    public long Id { get; set; }
    public long CodeId { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public long? DonViId { get; set; }
    public long? ReportingValueId { get; set; }
    public bool IsRequired { get; set; }
    public bool IsLocal { get; set; }
    public bool IsVisible { get; set; } = true;
}

public sealed class UpsertCodeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool AllowUnitCustomization { get; set; }
}

public sealed class UpsertCodeValueRequest
{
    public string Value { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsRequired { get; set; }
}

public sealed class UpsertUnitCodeValueRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SetCatalogItemVisibilityRequest
{
    public bool IsVisible { get; set; }
}
