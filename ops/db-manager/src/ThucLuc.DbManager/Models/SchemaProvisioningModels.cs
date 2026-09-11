namespace ThucLuc.DbManager.Models;

public sealed record OracleSchemaInspection(
    string DatabaseName,
    string DatabaseVersion,
    string DatabaseCompatibility,
    int? TimeZoneFileVersion,
    string ContainerName,
    int ContainerId,
    bool IsContainerDatabase,
    bool SchemaExists,
    string? AccountStatus,
    string? CurrentDefaultTablespace,
    int ObjectCount,
    string RecommendedTablespace,
    string TemporaryTablespace,
    IReadOnlyList<string> AvailableTablespaces,
    string? DataPumpDirectoryName,
    string? DataPumpDirectoryPath);

public sealed record OracleSchemaProvisionResult(
    string Schema,
    string ContainerName,
    string DefaultTablespace,
    string TemporaryTablespace,
    string? DataPumpDirectoryName,
    bool LoginVerified);

public sealed record OracleRestoreTargetInspection(
    string DatabaseName,
    string DatabaseVersion,
    string DatabaseCompatibility,
    int? TimeZoneFileVersion,
    int ObjectCount,
    string DataPumpDirectoryName);
