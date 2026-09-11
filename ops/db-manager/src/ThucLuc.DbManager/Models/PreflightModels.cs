namespace ThucLuc.DbManager.Models;

public enum CheckStatus
{
    Success,
    Warning,
    Error
}

public sealed record PreflightCheck(
    string Name,
    CheckStatus Status,
    string Message,
    string? Detail = null);

public sealed record PreflightReport(
    string EnvironmentKey,
    DateTimeOffset CheckedAt,
    IReadOnlyList<PreflightCheck> Checks)
{
    public CheckStatus Status => Checks.Any(check => check.Status == CheckStatus.Error)
        ? CheckStatus.Error
        : Checks.Any(check => check.Status == CheckStatus.Warning)
            ? CheckStatus.Warning
            : CheckStatus.Success;

    public int SuccessCount => Checks.Count(check => check.Status == CheckStatus.Success);
}

public enum OperationState
{
    Waiting,
    Running,
    Succeeded,
    Failed,
    Blocked
}

public sealed record OperationEntry(
    Guid Id,
    string Operation,
    string EnvironmentKey,
    OperationState State,
    DateTimeOffset CreatedAt,
    string Summary);
