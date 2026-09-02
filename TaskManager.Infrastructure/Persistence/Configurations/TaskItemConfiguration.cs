using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Core.Domain;

namespace TaskManager.Infrastructure.Persistence.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).IsRequired().HasMaxLength(TaskItem.MaxTitleLength);
        builder.Property(t => t.Description).HasMaxLength(4000);
        builder.Property(t => t.Priority).IsRequired();
        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.IsDeleted).IsRequired();

        // Правило повторения лежит в тех же строках таблицы Tasks (колонки Recurrence_*).
        builder.OwnsOne(t => t.Recurrence, r =>
        {
            r.Property(x => x.Frequency).HasColumnName("Recurrence_Frequency");
            r.Property(x => x.Interval).HasColumnName("Recurrence_Interval");
            r.Property(x => x.Mode).HasColumnName("Recurrence_Mode");
            r.Property(x => x.WeekDays).HasColumnName("Recurrence_WeekDays");
        });

        // Индексы под частые фильтры и сортировки.
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.Deadline);
        builder.HasIndex(t => t.ParentId);
    }
}
