using Microsoft.EntityFrameworkCore;
using TaskManager.Core.Domain;

namespace TaskManager.Infrastructure.Persistence;

public class TaskManagerDbContext : DbContext
{
    public TaskManagerDbContext(DbContextOptions<TaskManagerDbContext> options)
        : base(options)
    {
    }

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();

    public DbSet<TaskHistoryEntry> HistoryEntries => Set<TaskHistoryEntry>();

    public DbSet<SentReminder> SentReminders => Set<SentReminder>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BumpRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BumpRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// SQLite сам не ведёт версию строки (в отличие от SQL Server), поэтому увеличиваем её вручную:
    /// у каждой изменяемой задачи RowVersion становится на единицу больше прочитанного значения.
    /// </summary>
    private void BumpRowVersions()
    {
        ChangeTracker.DetectChanges();

        // Правило повторения хранится в той же строке, но для EF это отдельный вложенный объект.
        // Если изменился (добавлен, заменён или удалён) только он, сама задача остаётся Unchanged,
        // поэтому собираем Id задач-владельцев изменённых правил и поднимаем им версию вручную.
        var ownersWithChangedRecurrence = ChangeTracker.Entries<RecurrenceRule>()
            .Where(e => e.State != EntityState.Unchanged)
            .Select(e => e.Property(e.Metadata.FindOwnership()!.Properties[0].Name).CurrentValue)
            .OfType<int>()
            .ToHashSet();

        foreach (var entry in ChangeTracker.Entries<TaskItem>().ToList())
        {
            var isModified = entry.State == EntityState.Modified;
            var isUnchangedWithNewRecurrence = entry.State == EntityState.Unchanged
                && ownersWithChangedRecurrence.Contains(entry.Entity.Id);

            if (isModified || isUnchangedWithNewRecurrence)
            {
                var version = entry.Property(t => t.RowVersion);
                version.CurrentValue = (long)version.OriginalValue! + 1;
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // SQLite не умеет сравнивать и сортировать DateTimeOffset в SQL.
        // Храним момент времени как число (тики в UTC): сравнения и ORDER BY работают на стороне БД.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToUtcTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskManagerDbContext).Assembly);
    }
}
