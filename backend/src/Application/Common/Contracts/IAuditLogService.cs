using ThucLuc.Domain.Enums;

namespace ThucLuc.Application.Common.Contracts;

public interface IAuditLogService
{
    Task WriteAsync(
        AuditActionType actionType,
        string tableName,
        long? recordId,
        string? beforeData,
        string? afterData,
        string? route,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
}