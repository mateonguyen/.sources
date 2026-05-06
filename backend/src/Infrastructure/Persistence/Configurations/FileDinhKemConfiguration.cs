using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class FileDinhKemConfiguration : IEntityTypeConfiguration<FileDinhKem>
{
    public void Configure(EntityTypeBuilder<FileDinhKem> builder)
    {
        builder.ToTable("DOC_FILE_DINH_KEM");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityType).HasMaxLength(150).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.MimeType).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}