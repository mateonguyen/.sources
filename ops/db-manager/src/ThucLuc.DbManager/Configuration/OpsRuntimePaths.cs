namespace ThucLuc.DbManager.Configuration;

public sealed record OpsRuntimePaths(
    string ConfigurationFile,
    string StateDirectory,
    string? ReleaseRoot = null)
{
    public string OperationDatabaseFile => Path.Combine(StateDirectory, "operations.db");

    public string ReleaseDirectory => string.IsNullOrWhiteSpace(ReleaseRoot)
        ? Path.Combine(StateDirectory, "releases")
        : Path.GetFullPath(ReleaseRoot);
}
