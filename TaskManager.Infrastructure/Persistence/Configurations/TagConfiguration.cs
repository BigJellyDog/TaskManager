using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Core.Domain;

namespace TaskManager.Infrastructure.Persistence.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Tags");
        builder.HasKey(t => t.Id);

        // NOCASE: "Работа" и "работа" для SQLite один и тот же тег.
        builder.Property(t => t.Name).IsRequired().HasMaxLength(Tag.MaxNameLength).UseCollation("NOCASE");
        builder.HasIndex(t => t.Name).IsUnique();
    }
}
