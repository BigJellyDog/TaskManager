using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TaskManager.Infrastructure.Persistence;

/// <summary>
/// Настройки соединения SQLite, которые не хранятся в файле базы и действуют на каждое новое соединение:
/// synchronous=NORMAL (в режиме WAL безопасно и заметно быстрее на записи), временные таблицы в памяти.
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
