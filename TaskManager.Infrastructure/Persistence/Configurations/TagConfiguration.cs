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

        // Уникальность по NormalizedName: "Работа" и "работа" это один тег.
        // Коллация NOCASE в SQLite различает регистр кириллицы, поэтому она тут не подходит.
        builder.Property(t => t.NormalizedName).IsRequired().HasMaxLength(Tag.MaxNameLength);
        builder.HasIndex(t => t.NormalizedName).IsUnique();
    }
}
