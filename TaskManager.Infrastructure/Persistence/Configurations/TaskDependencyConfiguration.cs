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
    }
}
