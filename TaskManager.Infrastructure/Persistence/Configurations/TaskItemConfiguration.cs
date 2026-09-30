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

        // The global soft-delete filter: deleted tasks do not appear in any query.
        // To see them (trash, restore) .IgnoreQueryFilters() is needed.
        builder.HasQueryFilter(t => !t.IsDeleted);

        // A concurrency token: EF adds "WHERE RowVersion = <the value read>" to every UPDATE and DELETE.
        // If the row has already been changed, 0 rows are updated and EF throws DbUpdateConcurrencyException.
        builder.Property(t => t.RowVersion).IsConcurrencyToken();

        builder.Property(t => t.Title).IsRequired().HasMaxLength(TaskItem.MaxTitleLength);
        builder.Property(t => t.Description).HasMaxLength(4000);
        builder.Property(t => t.SearchText).IsRequired().HasMaxLength(TaskItem.MaxTitleLength + 1 + 4000);
        builder.Property(t => t.Priority).IsRequired();
        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.IsDeleted).IsRequired();
        builder.Property(t => t.SuccessorCreated).IsRequired();

        // The recurrence rule lives in the same rows of the Tasks table (the Recurrence_* columns).
        builder.OwnsOne(t => t.Recurrence, r =>
        {
            r.Property(x => x.Frequency).HasColumnName("Recurrence_Frequency");
            r.Property(x => x.Interval).HasColumnName("Recurrence_Interval");
            r.Property(x => x.Mode).HasColumnName("Recurrence_Mode");
            r.Property(x => x.WeekDays).HasColumnName("Recurrence_WeekDays");
            r.Property(x => x.Anchor).HasColumnName("Recurrence_Anchor");
        });

        // Subtasks: a self-relation. Restrict: a parent cannot be physically deleted while it has subtasks
        // (tasks are deleted softly anyway, with the IsDeleted flag).
        builder.HasOne(t => t.Parent)
            .WithMany(t => t.Children)
            .HasForeignKey(t => t.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tags: many-to-many, EF creates the TaskTags join table itself.
        builder.HasMany(t => t.Tags)
            .WithMany(tag => tag.Tasks)
            .UsingEntity(j => j.ToTable("TaskTags"));

        // Indexes for frequent filters and sorts.
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.Deadline);
        builder.HasIndex(t => t.ParentId);
    }
}
