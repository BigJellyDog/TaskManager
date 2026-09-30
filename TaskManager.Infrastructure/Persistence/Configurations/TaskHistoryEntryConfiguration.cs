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

        // There is no navigation in the entity, so the relation is described with the generic overload.
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(h => h.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // The task history is read in chronological order.
        builder.HasIndex(h => new { h.TaskId, h.ChangedAt });
    }
}
