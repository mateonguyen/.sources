using Microsoft.AspNetCore.Identity;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Domain.Entities.Identity;

public sealed class ApplicationUser : IdentityUser<long>
{
    public string HoTen { get; set; } = string.Empty;

    public long DonViId { get; set; }

    public DonVi? DonVi { get; set; }

    public bool MustChangePassword { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    public string? RefreshTokenHash { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public long? UpdatedBy { get; set; }

    public string? SoDienThoai { get; set; }
}