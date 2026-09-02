using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Core.Domain;

namespace TaskManager.Infrastructure.Persistence.Configurations;

public class TaskDependencyConfiguration : IEntityTypeConfiguration<TaskDependency>
{
    public void Configure(EntityTypeBuilder<TaskDependency> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TaskDependencies");

        // Составной ключ: одна и та же зависимость не может быть добавлена дважды.
        builder.HasKey(d => new { d.TaskId, d.DependsOnTaskId });
        builder.HasIndex(d => d.DependsOnTaskId);

        // Самосвязь через промежуточную таблицу: у задачи есть список "от чего зависит" и "кто зависит от неё".
        builder.HasOne(d => d.Task)
            .WithMany(t => t.Dependencies)
            .HasForeignKey(d => d.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.DependsOnTask)
            .WithMany(t => t.Dependents)
            .HasForeignKey(d => d.DependsOnTaskId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
