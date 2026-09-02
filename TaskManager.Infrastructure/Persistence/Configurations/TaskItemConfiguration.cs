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

        // Подзадачи: самосвязь. Restrict: родителя нельзя физически удалить, пока у него есть подзадачи
        // (задачи и так удаляются мягко, флагом IsDeleted).
        builder.HasOne(t => t.Parent)
            .WithMany(t => t.Children)
            .HasForeignKey(t => t.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Теги: многие-ко-многим, EF сам создаст таблицу связей TaskTags.
        builder.HasMany(t => t.Tags)
            .WithMany(tag => tag.Tasks)
            .UsingEntity(j => j.ToTable("TaskTags"));

        // Индексы под частые фильтры и сортировки.
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.Deadline);
        builder.HasIndex(t => t.ParentId);
    }
}
