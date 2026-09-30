using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TaskManager.Infrastructure.Persistence;

/// <summary>
/// SQLite connection settings that are not stored in the database file and apply to every new connection:
/// synchronous=NORMAL (safe in WAL mode and noticeably faster for writing), temporary tables in memory.
/// </summary>
public sealed class SqliteTuningInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Apply(connection);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Apply(connection);
        return Task.CompletedTask;
    }

    private static void Apply(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA synchronous = NORMAL; PRAGMA temp_store = MEMORY;";
        command.ExecuteNonQuery();
    }
}
