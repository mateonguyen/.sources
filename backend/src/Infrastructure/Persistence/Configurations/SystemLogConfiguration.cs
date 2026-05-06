using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class SystemLogConfiguration : IEntityTypeConfiguration<SystemLog>
{
    public void Configure(EntityTypeBuilder<SystemLog> builder)
    {
        builder.ToTable("AUD_SYSTEM_LOG");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TableName).HasMaxLength(200);
        builder.Property(x => x.BeforeData).HasColumnType("NCLOB");
        builder.Property(x => x.AfterData).HasColumnType("NCLOB");
        builder.Property(x => x.IpAddress).HasMaxLength(50);
        builder.Property(x => x.UserAgent).HasMaxLength(1000);
        builder.Property(x => x.Route).HasMaxLength(500);
        builder.HasIndex(x => x.LoggedAt);
    }
}