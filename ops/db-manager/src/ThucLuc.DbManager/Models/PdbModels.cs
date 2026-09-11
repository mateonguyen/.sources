namespace ThucLuc.DbManager.Models;

public sealed record OraclePdbInspection(
    bool IsContainerDatabase,
    string ContainerName,
    string? DefaultFileDestination,
    IReadOnlyList<OraclePdbChoice> Pdbs);

public sealed record OraclePdbChoice(string Name, string ServiceName, string OpenMode);

public sealed record OraclePdbSelection(string PdbName, string ServiceName, string Message);
