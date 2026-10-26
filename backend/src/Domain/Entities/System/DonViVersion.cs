namespace ThucLuc.Domain.Entities.System;

public sealed class DonViVersion
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public string MaDonVi { get; set; } = string.Empty;
    public string TenDonVi { get; set; } = string.Empty;
    public string? CapDonVi { get; set; }
    public string CheDoNhapLieu { get; set; } = "TU_NHAP";
    public long? ParentId { get; set; }
    public bool IsActive { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}

public sealed class DonViChange
{
    public long Id { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public DateTime EffectiveAt { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
}

public sealed class DonViSuccessor
{
    public long Id { get; set; }
    public long ChangeId { get; set; }
    public long SourceDvId { get; set; }
    public long TargetDvId { get; set; }
    public DateTime EffectiveAt { get; set; }
}
