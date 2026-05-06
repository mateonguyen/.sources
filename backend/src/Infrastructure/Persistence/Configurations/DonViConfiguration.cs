using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class DonViConfiguration : IEntityTypeConfiguration<DonVi>
{
    public void Configure(EntityTypeBuilder<DonVi> builder)
    {
        builder.ToTable("REF_DON_VI");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaDonVi).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TenDonVi).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TenVietTat).HasMaxLength(50);
        builder.Property(x => x.DiaChi).HasMaxLength(500);
        builder.Property(x => x.CapDonVi).HasMaxLength(20);
        builder.Property(x => x.WebsiteNoiBo).HasMaxLength(200);
        builder.Property(x => x.WebsiteInternet).HasMaxLength(200);
        builder.Property(x => x.CheDoNhapLieu).HasMaxLength(20).HasColumnName("CHE_DO_NHAP_LIEU").HasDefaultValue("TU_NHAP");
        builder.HasIndex(x => x.MaDonVi).IsUnique();
        builder.HasIndex(x => x.CapDonVi).HasDatabaseName("IX_REF_DV_CAP");
        builder.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}