using Microsoft.AspNetCore.Identity;

namespace ThucLuc.Domain.Entities.Identity;

public sealed class ApplicationRole : IdentityRole<long>
{
    public string TenRole { get; set; } = string.Empty;

    public string? MoTa { get; set; }

    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? CreatedBy { get; set; }
}