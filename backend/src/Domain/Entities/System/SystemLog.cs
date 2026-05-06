using ThucLuc.Domain.Common.Base;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Domain.Entities.System;

public sealed class SystemLog : EntityBase
{
    public long? UserId { get; set; }

    public long? DonViId { get; set; }

    public AuditActionType ActionType { get; set; }

    public string? TableName { get; set; }

    public long? RecordId { get; set; }

    public string? BeforeData { get; set; }

    public string? AfterData { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? Route { get; set; }

    public DateTime LoggedAt { get; set; }
}