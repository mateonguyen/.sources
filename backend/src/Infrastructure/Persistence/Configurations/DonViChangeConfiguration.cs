using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class DonViVersionConfiguration : IEntityTypeConfiguration<DonViVersion>
{
    public void Configure(EntityTypeBuilder<DonViVersion> builder)
    {
        builder.ToTable("REF_DV_VERSION");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaDonVi).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TenDonVi).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CapDonVi).HasMaxLength(20);
        builder.Property(x => x.CheDoNhapLieu).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.DonViId, x.ValidFrom, x.ValidTo }).HasDatabaseName("IX_DVV_UNIT_TIME");
    }
}

public sealed class DonViChangeConfiguration : IEntityTypeConfiguration<DonViChange>
{
    public void Configure(EntityTypeBuilder<DonViChange> builder)
    {
        builder.ToTable("REF_DV_CHANGE");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ChangeType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
    }
}

public sealed class DonViSuccessorConfiguration : IEntityTypeConfiguration<DonViSuccessor>
{
    public void Configure(EntityTypeBuilder<DonViSuccessor> builder)
    {
        builder.ToTable("REF_DV_SUCCESSOR");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.SourceDvId).HasDatabaseName("IX_DVS_SOURCE");
        builder.HasIndex(x => x.TargetDvId).HasDatabaseName("IX_DVS_TARGET");
    }
}
