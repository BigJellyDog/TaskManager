using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Core.Domain;

namespace TaskManager.Infrastructure.Persistence.Configurations;

public class SentReminderConfiguration : IEntityTypeConfiguration<SentReminder>
{
    public void Configure(EntityTypeBuilder<SentReminder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SentReminders");

        // Одно и то же напоминание (задача, вид, дедлайн) не может быть записано дважды.
        builder.HasKey(r => new { r.TaskId, r.Kind, r.Deadline });

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(r => r.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
