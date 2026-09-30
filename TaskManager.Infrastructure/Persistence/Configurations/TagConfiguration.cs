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

        builder.Property(t => t.Name).IsRequired().HasMaxLength(Tag.MaxNameLength);

        // Uniqueness by NormalizedName: "Работа" and "работа" are one tag.
        // The NOCASE collation in SQLite distinguishes the case of Cyrillic, so it is not suitable here.
        builder.Property(t => t.NormalizedName).IsRequired().HasMaxLength(Tag.MaxNameLength);
        builder.HasIndex(t => t.NormalizedName).IsUnique();
    }
}
