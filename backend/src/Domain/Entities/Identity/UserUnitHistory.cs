namespace ThucLuc.Domain.Entities.Identity;

public sealed class UserUnitHistory
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long SourceDvId { get; set; }
    public long TargetDvId { get; set; }
    public long? ChangeId { get; set; }
    public DateTime TransferredAt { get; set; }
    public long? TransferredBy { get; set; }
}
