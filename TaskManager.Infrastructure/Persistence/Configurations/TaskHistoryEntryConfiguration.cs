using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Core.Domain;

namespace TaskManager.Infrastructure.Persistence.Configurations;

public class TaskHistoryEntryConfiguration : IEntityTypeConfiguration<TaskHistoryEntry>
{
    public void Configure(EntityTypeBuilder<TaskHistoryEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TaskHistory");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Command).IsRequired().HasMaxLength(100);
        builder.Property(h => h.FieldName).IsRequired().HasMaxLength(100);
        builder.Property(h => h.OldValue).HasMaxLength(4000);
        builder.Property(h => h.NewValue).HasMaxLength(4000);

        // Навигации в сущности нет, поэтому связь описана через дженерик-перегрузку.
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(h => h.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // История задачи читается в хронологическом порядке.
        builder.HasIndex(h => new { h.TaskId, h.ChangedAt });
    }
}
