using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class CodeValueConfiguration : IEntityTypeConfiguration<CodeValue>
{
    public void Configure(EntityTypeBuilder<CodeValue> builder)
    {
        builder.ToTable("CODE_VALUES");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CodeId).HasColumnName("CODE_ID").IsRequired();
        builder.Property(x => x.Value).HasColumnName("VALUE").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasColumnName("NAME").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnName("DESCRIPTION").HasMaxLength(500);
        builder.Property(x => x.SortOrder).HasColumnName("SORT_ORDER").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("CREATED_BY");
        builder.Property(x => x.UpdatedBy).HasColumnName("UPDATED_BY");
        builder.HasIndex(x => new { x.CodeId, x.Value }).IsUnique().HasDatabaseName("UQ_CODE_VALUES_CODE_VALUE");
    }
}
