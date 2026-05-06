using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.Reporting;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class BaoCaoFileConfiguration : IEntityTypeConfiguration<BaoCaoFile>
{
    public void Configure(EntityTypeBuilder<BaoCaoFile> builder)
    {
        builder.ToTable("RPT_BAO_CAO_FILE");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.MimeType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LoaiFile).HasMaxLength(50).IsRequired();
    }
}