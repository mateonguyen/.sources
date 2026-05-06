namespace ThucLuc.Domain.Common.Interfaces;

public interface ISnapshotInPlaceEntity
{
    long DonViId { get; set; }
    string? KyBaoCaoCode { get; set; }
    bool IsLatest { get; set; }
    int SnapshotVersion { get; set; }
    DateTime? SnapshotCreatedAt { get; set; }
    long? SnapshotBatchId { get; set; }
}