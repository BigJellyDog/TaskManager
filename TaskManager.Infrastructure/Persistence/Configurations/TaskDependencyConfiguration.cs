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

        // A dependency in which either task is deleted is hidden together with it (otherwise a deleted task would keep
        // blocking live ones). After the task is restored the dependency appears again.
        builder.HasQueryFilter(d => !d.Task!.IsDeleted && !d.DependsOnTask!.IsDeleted);

        // A composite key: the same dependency cannot be added twice.
        builder.HasKey(d => new { d.TaskId, d.DependsOnTaskId });
        builder.HasIndex(d => d.DependsOnTaskId);

        // A self-relation through a join table: a task has the lists "what it depends on" and "who depends on it".
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
