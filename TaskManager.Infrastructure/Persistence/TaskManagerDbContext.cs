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
    /// SQLite does not keep a row version itself (unlike SQL Server), so we increase it manually:
    /// for every changed task RowVersion becomes one more than the value that was read.
    /// </summary>
    private void BumpRowVersions()
    {
        ChangeTracker.DetectChanges();

        // The recurrence rule is stored in the same row, but for EF it is a separate nested object.
        // If only it changed (was added, replaced or removed), the task itself stays Unchanged,
        // so we collect the Ids of the owner tasks of the changed rules and raise their version manually.
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

        // SQLite cannot compare and sort DateTimeOffset in SQL.
        // We store the moment in time as a number (ticks in UTC): comparisons and ORDER BY work on the database side.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToUtcTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskManagerDbContext).Assembly);
    }
}
