using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManager.Infrastructure.Persistence;

/// <summary>
/// Нужна только инструменту <c>dotnet ef</c> при создании миграций: он не умеет собирать DbContext из MAUI-приложения.
/// Реальный путь к БД приложение задаёт само при запуске.
/// </summary>
public class TaskManagerDbContextFactory : IDesignTimeDbContextFactory<TaskManagerDbContext>
{
    public TaskManagerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TaskManagerDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new TaskManagerDbContext(options);
    }
}
