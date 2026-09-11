namespace ThucLuc.DbManager.Models;

public sealed record MigrationFile(
    int Version,
    string Description,
    string FileName,
    bool Applied,
    bool Succeeded);

public sealed record MigrationInspection(
    string EnvironmentKey,
    string Schema,
    DateTimeOffset CheckedAt,
    IReadOnlyList<MigrationFile> Files,
    IReadOnlyList<string> RepeatableFiles,
    string Message)
{
    public int? CurrentVersion => Files.Where(file => file.Applied && file.Succeeded)
        .Select(file => (int?)file.Version)
        .Max();

    public IReadOnlyList<MigrationFile> Pending => Files.Where(file => !file.Applied).ToList();
}
