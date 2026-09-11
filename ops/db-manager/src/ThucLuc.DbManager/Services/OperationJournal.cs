using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Data.Sqlite;
using ThucLuc.DbManager.Configuration;
using ThucLuc.DbManager.Models;

namespace ThucLuc.DbManager.Services;

public interface IOperationJournal
{
    IReadOnlyList<OperationEntry> GetRecent(int count = 20);

    OperationEntry AddBlocked(string operation, string environmentKey, string summary);

    OperationEntry AddWaiting(string operation, string environmentKey, string summary);

    OperationEntry AddRunning(string operation, string environmentKey, string summary);

    void Complete(Guid id, OperationState state, string summary);

    int RecoverInterrupted();
}

public sealed class InMemoryOperationJournal : IOperationJournal
{
    private readonly object _syncRoot = new();
    private readonly List<OperationEntry> _entries = [];

    public IReadOnlyList<OperationEntry> GetRecent(int count = 20)
    {
        lock (_syncRoot)
        {
            return _entries.AsEnumerable().Reverse().Take(count).ToList();
        }
    }

    public OperationEntry AddBlocked(string operation, string environmentKey, string summary)
    {
        var entry = new OperationEntry(
            Guid.NewGuid(),
            operation,
            environmentKey,
            OperationState.Blocked,
            DateTimeOffset.Now,
            summary);
        Add(entry);
        return entry;
    }

    public OperationEntry AddWaiting(string operation, string environmentKey, string summary)
    {
        var entry = new OperationEntry(
            Guid.NewGuid(),
            operation,
            environmentKey,
            OperationState.Waiting,
            DateTimeOffset.Now,
            summary);
        Add(entry);
        return entry;
    }

    public OperationEntry AddRunning(string operation, string environmentKey, string summary)
    {
        var entry = new OperationEntry(
            Guid.NewGuid(),
            operation,
            environmentKey,
            OperationState.Running,
            DateTimeOffset.Now,
            summary);
        Add(entry);
        return entry;
    }

    public void Complete(Guid id, OperationState state, string summary)
    {
        lock (_syncRoot)
        {
            var index = _entries.FindIndex(entry => entry.Id == id);
            if (index >= 0)
            {
                _entries[index] = _entries[index] with { State = state, Summary = summary };
            }
        }
    }

    public int RecoverInterrupted()
    {
        var recovered = 0;
        lock (_syncRoot)
        {
            for (var index = 0; index < _entries.Count; index++)
            {
                if (_entries[index].State is not (OperationState.Waiting or OperationState.Running))
                {
                    continue;
                }

                _entries[index] = _entries[index] with
                {
                    State = OperationState.Failed,
                    Summary = "Ứng dụng đã khởi động lại trước khi thao tác hoàn tất. Hãy kiểm tra trạng thái thực tế trước khi chạy lại."
                };
                recovered++;
            }
        }

        return recovered;
    }

    private void Add(OperationEntry entry)
    {
        lock (_syncRoot)
        {
            _entries.Add(entry);
        }
    }
}

public sealed class SqliteOperationJournal : IOperationJournal
{
    private const string TimestampFormat = "O";
    private readonly string _connectionString;
    private readonly object _syncRoot = new();

    public SqliteOperationJournal(OpsRuntimePaths runtimePaths)
    {
        Directory.CreateDirectory(runtimePaths.StateDirectory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = runtimePaths.OperationDatabaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
        Initialize();
    }

    public IReadOnlyList<OperationEntry> GetRecent(int count = 20)
    {
        if (count <= 0)
        {
            return [];
        }

        lock (_syncRoot)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, operation, environment_key, state, created_at, summary
                FROM operation_entries
                ORDER BY created_at DESC
                LIMIT $count;
                """;
            command.Parameters.AddWithValue("$count", count);

            using var reader = command.ExecuteReader();
            var entries = new List<OperationEntry>();
            while (reader.Read())
            {
                entries.Add(new OperationEntry(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    (OperationState)reader.GetInt32(3),
                    DateTimeOffset.ParseExact(reader.GetString(4), TimestampFormat, CultureInfo.InvariantCulture),
                    reader.GetString(5)));
            }

            return entries;
        }
    }

    public OperationEntry AddBlocked(string operation, string environmentKey, string summary) =>
        Add(operation, environmentKey, OperationState.Blocked, summary);

    public OperationEntry AddWaiting(string operation, string environmentKey, string summary) =>
        Add(operation, environmentKey, OperationState.Waiting, summary);

    public OperationEntry AddRunning(string operation, string environmentKey, string summary) =>
        Add(operation, environmentKey, OperationState.Running, summary);

    public void Complete(Guid id, OperationState state, string summary)
    {
        lock (_syncRoot)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE operation_entries
                SET state = $state, summary = $summary, updated_at = $updatedAt
                WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            command.Parameters.AddWithValue("$state", (int)state);
            command.Parameters.AddWithValue("$summary", summary);
            command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString(TimestampFormat, CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
    }

    public int RecoverInterrupted()
    {
        lock (_syncRoot)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE operation_entries
                SET state = $failed,
                    summary = $summary,
                    updated_at = $updatedAt
                WHERE state IN ($waiting, $running);
                """;
            command.Parameters.AddWithValue("$failed", (int)OperationState.Failed);
            command.Parameters.AddWithValue("$waiting", (int)OperationState.Waiting);
            command.Parameters.AddWithValue("$running", (int)OperationState.Running);
            command.Parameters.AddWithValue(
                "$summary",
                "Ứng dụng đã khởi động lại trước khi thao tác hoàn tất. Hãy kiểm tra trạng thái thực tế trước khi chạy lại.");
            command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString(TimestampFormat, CultureInfo.InvariantCulture));
            return command.ExecuteNonQuery();
        }
    }

    private OperationEntry Add(
        string operation,
        string environmentKey,
        OperationState state,
        string summary)
    {
        var entry = new OperationEntry(
            Guid.NewGuid(),
            operation,
            environmentKey,
            state,
            DateTimeOffset.UtcNow,
            summary);

        lock (_syncRoot)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO operation_entries
                    (id, operation, environment_key, state, created_at, updated_at, summary)
                VALUES
                    ($id, $operation, $environmentKey, $state, $createdAt, $updatedAt, $summary);
                """;
            command.Parameters.AddWithValue("$id", entry.Id.ToString("D"));
            command.Parameters.AddWithValue("$operation", operation);
            command.Parameters.AddWithValue("$environmentKey", environmentKey);
            command.Parameters.AddWithValue("$state", (int)state);
            command.Parameters.AddWithValue("$createdAt", entry.CreatedAt.ToString(TimestampFormat, CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$updatedAt", entry.CreatedAt.ToString(TimestampFormat, CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$summary", summary);
            command.ExecuteNonQuery();
        }

        return entry;
    }

    private void Initialize()
    {
        lock (_syncRoot)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode = WAL;
                CREATE TABLE IF NOT EXISTS operation_entries
                (
                    id              TEXT PRIMARY KEY,
                    operation       TEXT NOT NULL,
                    environment_key TEXT NOT NULL,
                    state           INTEGER NOT NULL,
                    created_at      TEXT NOT NULL,
                    updated_at      TEXT NOT NULL,
                    summary         TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_operation_entries_created_at
                    ON operation_entries(created_at DESC);
                """;
            command.ExecuteNonQuery();
        }
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
