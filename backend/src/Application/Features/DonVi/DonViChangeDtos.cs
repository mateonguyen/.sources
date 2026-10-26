namespace ThucLuc.Application.Features.DonVi;

public sealed class MergeDonViRequest
{
    public IReadOnlyCollection<long> SourceIds { get; set; } = Array.Empty<long>();
    public long TargetId { get; set; }
    public string? Note { get; set; }
}

public sealed class SplitDonViRequest
{
    public long SourceId { get; set; }
    public IReadOnlyCollection<MoveChildDonViItem> Children { get; set; } = Array.Empty<MoveChildDonViItem>();
    public IReadOnlyCollection<MoveUnitUserItem> Users { get; set; } = Array.Empty<MoveUnitUserItem>();
    public bool DeactivateSource { get; set; }
    public string? Note { get; set; }
}

public sealed class MoveChildDonViItem
{
    public long ChildId { get; set; }
    public long TargetId { get; set; }
}

public sealed class MoveUnitUserItem
{
    public long UserId { get; set; }
    public long TargetId { get; set; }
    public IReadOnlyCollection<long> RoleIds { get; set; } = Array.Empty<long>();
}

public sealed class DonViChangePreviewDto
{
    public int UnitCount { get; set; }
    public int ChildCount { get; set; }
    public int UserCount { get; set; }
    public int DraftSnapshotCount { get; set; }
    public IReadOnlyCollection<string> Errors { get; set; } = Array.Empty<string>();
}

public sealed class DonViChangeResultDto
{
    public long ChangeId { get; set; }
    public int ChildCount { get; set; }
    public int UserCount { get; set; }
}
