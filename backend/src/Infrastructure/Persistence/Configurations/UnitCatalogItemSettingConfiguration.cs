using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class UnitCatalogItemSettingConfiguration : IEntityTypeConfiguration<UnitCatalogItemSetting>
{
    public void Configure(EntityTypeBuilder<UnitCatalogItemSetting> builder)
    {
        builder.ToTable("CATALOG_UNIT_SETTINGS");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DonViId).HasColumnName("DON_VI_ID").IsRequired();
        builder.Property(x => x.CatalogKey).HasColumnName("CATALOG_KEY").HasMaxLength(150).IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("ITEM_ID").IsRequired();
        builder.Property(x => x.IsVisible).HasColumnName("IS_VISIBLE").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("CREATED_BY");
        builder.Property(x => x.UpdatedBy).HasColumnName("UPDATED_BY");
        builder.HasIndex(x => new { x.DonViId, x.CatalogKey, x.ItemId })
            .IsUnique()
            .HasDatabaseName("UQ_CATALOG_UNIT_ITEM");
    }
}
