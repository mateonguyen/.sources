using Microsoft.AspNetCore.Http;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Domain.Entities.System;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Infrastructure.Services;

public sealed class AuditLogService : IAuditLogService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AuditLogService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task WriteAsync(
        AuditActionType actionType,
        string tableName,
        long? recordId,
        string? beforeData,
        string? afterData,
        string? route,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currentUser = _currentUserService.GetCurrentUser();
            var httpContext = _httpContextAccessor.HttpContext;
            await _dbContext.SystemLogs.AddAsync(new SystemLog
            {
                UserId = currentUser.UserId > 0 ? currentUser.UserId : null,
                DonViId = currentUser.DonViId > 0 ? currentUser.DonViId : null,
                ActionType = actionType,
                TableName = tableName,
                RecordId = recordId,
                BeforeData = beforeData,
                AfterData = afterData,
                Route = route ?? httpContext?.Request.Path.Value,
                IpAddress = ipAddress ?? httpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = userAgent ?? httpContext?.Request.Headers.UserAgent.ToString(),
                LoggedAt = _dateTimeProvider.Now
            }, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Audit log failures must not block business operations like login.
        }
    }
}