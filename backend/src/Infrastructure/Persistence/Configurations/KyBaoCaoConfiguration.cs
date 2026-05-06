using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.Reporting;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class KyBaoCaoConfiguration : IEntityTypeConfiguration<KyBaoCao>
{
    public void Configure(EntityTypeBuilder<KyBaoCao> builder)
    {
        builder.ToTable("RPT_KY_BAO_CAO");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.KyCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.GhiChu).HasMaxLength(2000);
        builder.HasIndex(x => x.KyCode).IsUnique();
    }
}