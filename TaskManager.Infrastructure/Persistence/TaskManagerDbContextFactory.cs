using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManager.Infrastructure.Persistence;

/// <summary>
/// Needed only by the <c>dotnet ef</c> tool when creating migrations: it cannot build a DbContext from the MAUI application.
/// The application sets the real database path itself at start-up.
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
